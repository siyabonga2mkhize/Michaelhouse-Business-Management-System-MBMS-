using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Place / Amend Stock Order (cafeteria ingredients)
    //
    //   Draft ──send──▶ Pending ──supplier confirms──▶ Confirmed
    //     │                │                              │
    //     └── cancel       └──── deliveries ─────▶ Partially Fulfilled ─▶ Fulfilled
    //
    // Quantities are in the ingredient's own unit (g / ml).
    //
    // Supplier shortfall: when the supplier confirms less than was
    // ordered, Shortfall = Ordered − Confirmed. The manager can
    // accept it, amend the line, or re-order it from another supplier
    // (chosen, or picked automatically from the ingredient's other
    // suppliers). A re-ordered line remembers the new order, so the
    // same shortfall can't be ordered twice.
    //
    // Stock never changes here — only when a delivery is recorded.
    // ============================================================

    public class OrderLineInput
    {
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }       // ingredient's own unit
        public decimal? UnitCost { get; set; }
    }

    public class IngredientPurchasingService
    {
        private readonly DBContextClass _db;
        private readonly Func<DateTime> _now;

        public IngredientPurchasingService(DBContextClass db)
            : this(db, null)
        {
        }

        public IngredientPurchasingService(DBContextClass db, Func<DateTime> now)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _now = now ?? (() => SchoolClock.Now);
        }

        public IngredientPurchaseOrder GetOrder(int orderId)
        {
            return _db.IngredientPurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Lines.Select(l => l.Ingredient))
                .FirstOrDefault(o => o.Id == orderId);
        }

        // Active cafeteria suppliers
        public List<Supplier> CafeteriaSuppliers()
        {
            return _db.Suppliers.Where(s => s.SuppliesCafeteria && s.IsActive).OrderBy(s => s.Name).ToList();
        }

        // ============================================================
        // PLACE ORDER
        // ============================================================

        public IngredientPurchaseOrder CreateDraft(int supplierId, IEnumerable<OrderLineInput> lines,
            DateTime? requestedDelivery, string notes, int userId, int? shortfallOfOrderId = null)
        {
            var supplier = _db.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId);
            if (supplier == null || !supplier.IsActive || !supplier.SuppliesCafeteria)
                throw new InventoryException("Choose an active cafeteria supplier.");

            var clean = CleanLines(lines);
            if (clean.Count == 0)
                throw new InventoryException("Add at least one ingredient with a quantity.");

            if (requestedDelivery.HasValue && requestedDelivery.Value.Date < _now().Date)
                throw new InventoryException("The requested delivery date can't be in the past.");

            var order = new IngredientPurchaseOrder
            {
                PoNumber = NextPoNumber(),
                SupplierId = supplierId,
                Status = IngredientOrderStatus.Draft,
                OrderDate = _now(),
                RequestedDeliveryDate = requestedDelivery.HasValue ? requestedDelivery.Value.Date : (DateTime?)null,
                Notes = Clip(notes, 1000),
                CreatedByUserId = userId,
                ShortfallOfOrderId = shortfallOfOrderId
            };

            foreach (var l in clean)
            {
                order.Lines.Add(new IngredientPurchaseOrderLine
                {
                    IngredientId = l.IngredientId,
                    QuantityOrdered = l.Quantity,
                    UnitCost = l.UnitCost
                });
            }

            _db.IngredientPurchaseOrders.Add(order);
            _db.SaveChanges();
            return order;
        }

        // Draft only: replace the lines / details
        public void UpdateDraft(int orderId, int supplierId, IEnumerable<OrderLineInput> lines, DateTime? requestedDelivery, string notes)
        {
            var order = RequireOrder(orderId);
            if (order.Status != IngredientOrderStatus.Draft)
                throw new InventoryException("Only draft orders can be edited. Use Amend for orders already sent.");

            var supplier = _db.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId);
            if (supplier == null || !supplier.IsActive || !supplier.SuppliesCafeteria)
                throw new InventoryException("Choose an active cafeteria supplier.");

            var clean = CleanLines(lines);
            if (clean.Count == 0)
                throw new InventoryException("Add at least one ingredient with a quantity.");

            foreach (var old in order.Lines.ToList()) _db.IngredientPurchaseOrderLines.Remove(old);
            foreach (var l in clean)
            {
                order.Lines.Add(new IngredientPurchaseOrderLine { IngredientId = l.IngredientId, QuantityOrdered = l.Quantity, UnitCost = l.UnitCost });
            }

            order.SupplierId = supplierId;
            order.RequestedDeliveryDate = requestedDelivery.HasValue ? requestedDelivery.Value.Date : (DateTime?)null;
            order.Notes = Clip(notes, 1000);
            _db.SaveChanges();
        }

        // Draft → Pending; emails the supplier when an address is on file
        public bool Send(int orderId)
        {
            var order = RequireOrder(orderId);
            if (order.Status != IngredientOrderStatus.Draft)
                throw new InventoryException("Only draft orders can be sent.");
            if (!order.Lines.Any())
                throw new InventoryException("The order has no ingredients.");

            order.Status = IngredientOrderStatus.Pending;
            order.SentAt = DateTime.UtcNow;
            _db.SaveChanges();

            return TryEmailSupplier(order);
        }

        // ============================================================
        // AMEND
        // ============================================================

        // The supplier says what they can supply. Quantities per line,
        // 0..ordered. Lines not given keep their current confirmation.
        public void RecordSupplierConfirmation(int orderId, IDictionary<int, decimal> confirmedByLine)
        {
            var order = RequireOrder(orderId);
            if (order.Status != IngredientOrderStatus.Pending && order.Status != IngredientOrderStatus.Confirmed
                && order.Status != IngredientOrderStatus.PartiallyFulfilled)
                throw new InventoryException("Supplier confirmation can only be recorded on orders that have been sent.");

            foreach (var kv in confirmedByLine ?? new Dictionary<int, decimal>())
            {
                var line = order.Lines.FirstOrDefault(l => l.Id == kv.Key);
                if (line == null) throw new InventoryException("That item isn't on this order.");

                decimal confirmed = Math.Round(kv.Value, 2);
                if (confirmed < 0m || confirmed > line.QuantityOrdered)
                    throw new InventoryException(string.Format(
                        "{0}: the confirmed quantity must be between 0 and the {1} ordered.",
                        line.Ingredient.Name, IngredientUnits.Format(line.QuantityOrdered, line.Ingredient.Unit)));

                if (confirmed < line.QuantityReceived)
                    throw new InventoryException(string.Format(
                        "{0}: {1} has already been received.", line.Ingredient.Name, IngredientUnits.Format(line.QuantityReceived, line.Ingredient.Unit)));

                if (line.ShortfallAction == ShortfallAction.Reordered && confirmed != line.QuantityConfirmed)
                    throw new InventoryException(line.Ingredient.Name + ": the shortfall has already been re-ordered from another supplier.");

                line.QuantityConfirmed = confirmed;
                if (line.Shortfall == 0m)
                {
                    line.ShortfallAction = ShortfallAction.None;
                    line.ShortfallNote = null;
                }
            }

            if (order.Status == IngredientOrderStatus.Pending && order.Lines.All(l => l.QuantityConfirmed.HasValue))
            {
                order.Status = IngredientOrderStatus.Confirmed;
                order.ConfirmedAt = DateTime.UtcNow;
            }

            UpdateStatusFromReceipts(order);
            _db.SaveChanges();
        }

        // Manual amendment of a sent order's line: change what's ordered
        // (e.g. agreed by phone). Can't go below what has arrived.
        public void AmendLine(int orderId, int lineId, decimal newQuantity, string note)
        {
            var order = RequireOrder(orderId);
            if (order.Status == IngredientOrderStatus.Draft)
                throw new InventoryException("Edit the draft instead.");
            if (order.Status == IngredientOrderStatus.Fulfilled || order.Status == IngredientOrderStatus.Cancelled)
                throw new InventoryException("This order is " + order.Status.ToString().ToLower() + " and can't be amended.");

            var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
            if (line == null) throw new InventoryException("That item isn't on this order.");
            if (line.ShortfallAction == ShortfallAction.Reordered)
                throw new InventoryException(line.Ingredient.Name + ": the shortfall has already been re-ordered from another supplier.");

            newQuantity = Math.Round(newQuantity, 2);
            if (newQuantity < line.QuantityReceived)
                throw new InventoryException(string.Format(
                    "{0}: can't amend below the {1} already received.", line.Ingredient.Name, IngredientUnits.Format(line.QuantityReceived, line.Ingredient.Unit)));
            if (newQuantity <= 0m)
                throw new InventoryException("Enter a quantity above zero, or cancel the order.");

            line.QuantityOrdered = newQuantity;
            if (line.QuantityConfirmed.HasValue && line.QuantityConfirmed.Value > newQuantity) line.QuantityConfirmed = newQuantity;
            if (line.Shortfall == 0m) { line.ShortfallAction = ShortfallAction.None; }
            line.ShortfallNote = Clip(note, 300) ?? line.ShortfallNote;

            UpdateStatusFromReceipts(order);
            _db.SaveChanges();
        }

        // The manager accepts the smaller quantity for these lines
        public void AcceptShortfall(int orderId, IEnumerable<int> lineIds, string note)
        {
            var order = RequireOrder(orderId);
            foreach (var line in ShortfallLines(order, lineIds))
            {
                line.ShortfallAction = ShortfallAction.Accepted;
                line.ShortfallNote = Clip(note, 300);
            }

            UpdateStatusFromReceipts(order);
            _db.SaveChanges();
        }

        // Re-order the shortfall of these lines from another supplier.
        // supplierId null = pick automatically: for each ingredient, its
        // preferred other active supplier. Creates one draft order per
        // supplier and returns them.
        public List<IngredientPurchaseOrder> ReorderShortfall(int orderId, IEnumerable<int> lineIds, int? supplierId, int userId)
        {
            var order = RequireOrder(orderId);
            var lines = ShortfallLines(order, lineIds);

            var plan = new Dictionary<int, List<IngredientPurchaseOrderLine>>();
            var noSupplier = new List<string>();

            foreach (var line in lines)
            {
                int? chosen = supplierId;

                if (chosen.HasValue)
                {
                    if (chosen.Value == order.SupplierId)
                        throw new InventoryException("Choose a different supplier for the shortfall.");
                }
                else
                {
                    chosen = _db.IngredientSuppliers
                        .Where(x => x.IngredientId == line.IngredientId && x.IsActive
                                    && x.SupplierId != order.SupplierId
                                    && x.Supplier.IsActive && x.Supplier.SuppliesCafeteria)
                        .OrderByDescending(x => x.IsPreferred)
                        .ThenBy(x => x.LeadTimeDays)
                        .Select(x => (int?)x.SupplierId)
                        .FirstOrDefault();

                    if (!chosen.HasValue)
                    {
                        noSupplier.Add(line.Ingredient.Name);
                        continue;
                    }
                }

                if (!plan.ContainsKey(chosen.Value)) plan[chosen.Value] = new List<IngredientPurchaseOrderLine>();
                plan[chosen.Value].Add(line);
            }

            if (noSupplier.Count > 0)
                throw new InventoryException("No other active supplier is linked to: " + string.Join(", ", noSupplier)
                    + ". Link one under the ingredient, or choose a supplier.");

            var created = new List<IngredientPurchaseOrder>();

            using (var tx = _db.Database.BeginTransaction())
            {
                foreach (var kv in plan)
                {
                    var newOrder = CreateDraft(kv.Key,
                        kv.Value.Select(l => new OrderLineInput { IngredientId = l.IngredientId, Quantity = l.Shortfall }),
                        order.RequestedDeliveryDate.HasValue && order.RequestedDeliveryDate.Value.Date >= _now().Date ? order.RequestedDeliveryDate : null,
                        "Shortfall from " + order.PoNumber,
                        userId,
                        order.Id);

                    foreach (var line in kv.Value)
                    {
                        line.ShortfallAction = ShortfallAction.Reordered;
                        line.ShortfallReorderedOnOrderId = newOrder.Id;
                        line.ShortfallNote = "Re-ordered on " + newOrder.PoNumber;
                    }

                    created.Add(newOrder);
                }

                UpdateStatusFromReceipts(order);
                _db.SaveChanges();
                tx.Commit();
            }

            return created;
        }

        public void Cancel(int orderId, string reason)
        {
            var order = RequireOrder(orderId);
            if (order.Status == IngredientOrderStatus.Cancelled)
                throw new InventoryException("This order is already cancelled.");
            if (order.Status == IngredientOrderStatus.Fulfilled || order.Lines.Any(l => l.QuantityReceived > 0m))
                throw new InventoryException("Stock has already been received on this order, so it can't be cancelled. Close it instead.");

            order.Status = IngredientOrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.Notes = AppendNote(order.Notes, "Cancelled: " + (string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason.Trim()));
            _db.SaveChanges();
        }

        // Nothing more is coming: close a partly received order
        public void Close(int orderId, string reason)
        {
            var order = RequireOrder(orderId);
            if (order.Status != IngredientOrderStatus.PartiallyFulfilled && order.Status != IngredientOrderStatus.Confirmed
                && order.Status != IngredientOrderStatus.Pending)
                throw new InventoryException("Only open orders can be closed.");
            if (!order.Lines.Any(l => l.QuantityReceived > 0m))
                throw new InventoryException("Nothing has been received on this order — cancel it instead.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new InventoryException("Please say why the rest won't be delivered.");

            order.Status = IngredientOrderStatus.Fulfilled;
            order.CompletedAt = DateTime.UtcNow;
            order.Notes = AppendNote(order.Notes, "Closed with items outstanding: " + reason.Trim());
            _db.SaveChanges();
        }

        // After deliveries / amendments: Partially Fulfilled or Fulfilled
        public void UpdateStatusFromReceipts(IngredientPurchaseOrder order)
        {
            if (order.Status == IngredientOrderStatus.Draft || order.Status == IngredientOrderStatus.Cancelled) return;

            bool anyReceived = order.Lines.Any(l => l.QuantityReceived > 0m);

            // Complete when every line has arrived, or its remainder is a
            // shortfall that's been accepted / re-ordered
            bool allDone = order.Lines.All(l => l.Outstanding <= 0m
                || (l.Shortfall > 0m && l.ShortfallAction != ShortfallAction.None && l.QuantityReceived >= (l.QuantityConfirmed ?? 0m)));

            if (allDone && anyReceived)
            {
                if (order.Status != IngredientOrderStatus.Fulfilled)
                {
                    order.Status = IngredientOrderStatus.Fulfilled;
                    order.CompletedAt = DateTime.UtcNow;
                }
            }
            else if (anyReceived)
            {
                order.Status = IngredientOrderStatus.PartiallyFulfilled;
                order.CompletedAt = null;
            }
            else if (order.Lines.All(l => l.QuantityConfirmed.HasValue))
            {
                order.Status = IngredientOrderStatus.Confirmed;
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private IngredientPurchaseOrder RequireOrder(int orderId)
        {
            var order = GetOrder(orderId);
            if (order == null) throw new InventoryException("Purchase order not found.");
            return order;
        }

        private List<IngredientPurchaseOrderLine> ShortfallLines(IngredientPurchaseOrder order, IEnumerable<int> lineIds)
        {
            // A fulfilled order can still have an undecided shortfall (the
            // supplier delivered everything they confirmed)
            if (order.Status == IngredientOrderStatus.Draft || order.Status == IngredientOrderStatus.Cancelled)
                throw new InventoryException("Shortfalls can only be handled on orders that have been sent.");

            var ids = (lineIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0) throw new InventoryException("Choose the items to handle.");

            var lines = order.Lines.Where(l => ids.Contains(l.Id)).ToList();
            if (lines.Count != ids.Count) throw new InventoryException("That item isn't on this order.");

            foreach (var line in lines)
            {
                if (line.Shortfall <= 0m)
                    throw new InventoryException(line.Ingredient.Name + " has no supplier shortfall. Record the supplier's confirmed quantity first.");
                if (line.ShortfallAction == ShortfallAction.Reordered)
                    throw new InventoryException(line.Ingredient.Name + ": the shortfall has already been re-ordered.");
            }

            return lines;
        }

        // Active ingredients, positive quantities, one line per ingredient
        private List<OrderLineInput> CleanLines(IEnumerable<OrderLineInput> lines)
        {
            var given = (lines ?? Enumerable.Empty<OrderLineInput>()).Where(l => l != null && l.Quantity > 0m).ToList();
            var ids = given.Select(l => l.IngredientId).Distinct().ToList();

            var active = _db.Ingredients.Where(i => ids.Contains(i.Id) && i.IsActive).Select(i => i.Id).ToList();
            var invalid = ids.Except(active).ToList();
            if (invalid.Count > 0) throw new InventoryException("One of the ingredients doesn't exist or is no longer active.");

            if (given.Any(l => l.UnitCost.HasValue && l.UnitCost.Value < 0m))
                throw new InventoryException("Unit costs can't be negative.");

            return given
                .GroupBy(l => l.IngredientId)
                .Select(g => new OrderLineInput
                {
                    IngredientId = g.Key,
                    Quantity = Math.Round(g.Sum(l => l.Quantity), 2),
                    UnitCost = g.Select(l => l.UnitCost).FirstOrDefault(c => c.HasValue)
                })
                .ToList();
        }

        // CAF-PO-2026-00001
        private string NextPoNumber()
        {
            string prefix = "CAF-PO-" + _now().Year + "-";
            var last = _db.IngredientPurchaseOrders
                .Where(o => o.PoNumber.StartsWith(prefix))
                .OrderByDescending(o => o.PoNumber)
                .Select(o => o.PoNumber)
                .FirstOrDefault();

            int next = 1;
            int n;
            if (last != null && int.TryParse(last.Substring(prefix.Length), out n)) next = n + 1;

            // Also count unsaved orders in this context (several shortfall orders at once)
            int pending = _db.IngredientPurchaseOrders.Local.Count(o => o.Id == 0 && o.PoNumber != null && o.PoNumber.StartsWith(prefix));
            return prefix + (next + pending).ToString("00000");
        }

        private bool TryEmailSupplier(IngredientPurchaseOrder order)
        {
            if (order.Supplier == null || string.IsNullOrWhiteSpace(order.Supplier.Email)) return false;

            try
            {
                var rows = string.Join("", order.Lines.Select(l => string.Format(
                    "<tr><td style='padding:6px;border-bottom:1px solid #eee;'>{0}</td><td style='padding:6px;border-bottom:1px solid #eee;text-align:right;'>{1}</td></tr>",
                    System.Web.HttpUtility.HtmlEncode(l.Ingredient.Name),
                    IngredientUnits.Format(l.QuantityOrdered, l.Ingredient.Unit))));

                var body = string.Format(
                    "<p>Dear {0},</p><p>Please supply the following for the Michaelhouse cafeteria (order <strong>{1}</strong>){2}.</p>"
                    + "<table style='border-collapse:collapse;'><tr><th style='text-align:left;padding:6px;'>Item</th><th style='text-align:right;padding:6px;'>Quantity</th></tr>{3}</table>"
                    + "<p>Please confirm the quantities you can supply, quoting the order number on your invoice.</p>",
                    System.Web.HttpUtility.HtmlEncode(order.Supplier.ContactPerson ?? order.Supplier.Name),
                    order.PoNumber,
                    order.RequestedDeliveryDate.HasValue ? ", for delivery by " + order.RequestedDeliveryDate.Value.ToString("dd MMMM yyyy") : "",
                    rows);

                new EmailService().SendPlain(order.Supplier.Email, "Purchase order " + order.PoNumber + " — Michaelhouse Cafeteria", body);
                return true;
            }
            catch (Exception ex)
            {
                // Non-fatal, as for Store orders: the order is still sent
                System.Diagnostics.Trace.TraceWarning("Cafeteria PO email failed: " + ex.Message);
                return false;
            }
        }

        private static string Clip(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }

        private static string AppendNote(string notes, string add)
        {
            var combined = string.IsNullOrWhiteSpace(notes) ? add : notes + "\n" + add;
            return combined.Length > 1000 ? combined.Substring(combined.Length - 1000) : combined;
        }
    }
}
