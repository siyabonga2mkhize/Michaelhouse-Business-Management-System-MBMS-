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

    // Automatic ordering: one group (→ one purchase order) per supplier
    public class AutoOrderPlan
    {
        public AutoOrderPlan()
        {
            Groups = new List<AutoOrderGroup>();
            NoSupplier = new List<AutoOrderItem>();
        }

        public List<AutoOrderGroup> Groups { get; set; }

        // Shown as "No supplier available" — not ordered
        public List<AutoOrderItem> NoSupplier { get; set; }

        // Suggested, but already on a draft order that hasn't been sent
        // (drafts don't count as "on order" yet) — not ordered again
        public List<AutoOrderItem> OnDraft { get; set; } = new List<AutoOrderItem>();
    }

    public class AutoOrderGroup
    {
        public AutoOrderGroup()
        {
            Items = new List<AutoOrderItem>();
        }

        public Supplier Supplier { get; set; }
        public int LeadTimeDays { get; set; }
        public List<AutoOrderItem> Items { get; set; }
    }

    public class AutoOrderItem
    {
        public Ingredient Ingredient { get; set; }
        public decimal Quantity { get; set; }       // ingredient's own unit
        public decimal Available { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal OnOrder { get; set; }
        public decimal? UnitCost { get; set; }      // per kg / L
        public bool IsPreferredSupplier { get; set; }
        public string Why { get; set; }

        // OnDraft only: the draft it's already on
        public string DraftPoNumber { get; set; }
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

        // What happened to the order's test invoice in the last call
        // (TestInvoiceService), for the manager; null if nothing
        public string InvoiceMessage { get; private set; }

        // Keep the test invoice in step after a change to a sent order
        private void RefreshTestInvoice(int orderId)
        {
            InvoiceMessage = new TestInvoiceService(_db, _now).Refresh(orderId);
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
        // SUPPLIER CHOICE — one rule for automatic orders and shortfall
        // re-orders: the ingredient's active links to active cafeteria
        // suppliers, preferred first, then the shortest lead time.
        // Null when none is available.
        // ============================================================

        public IngredientSupplier ChooseSupplier(int ingredientId, int? excludeSupplierId = null)
        {
            return _db.IngredientSuppliers
                .Include(x => x.Supplier)
                .Where(x => x.IngredientId == ingredientId && x.IsActive
                            && (!excludeSupplierId.HasValue || x.SupplierId != excludeSupplierId.Value)
                            && x.Supplier.IsActive && x.Supplier.SuppliesCafeteria)
                .OrderByDescending(x => x.IsPreferred)
                .ThenBy(x => x.LeadTimeDays)
                .ThenBy(x => x.Supplier.Name)
                .FirstOrDefault();
        }

        // ============================================================
        // SUGGESTED ORDER — unchanged rules (moved from the order form):
        // low / out of stock → top up to the target level (twice the
        // reorder level when no target), allowing for what's on order;
        // upcoming kitchen / event shortage → the shortfall after
        // orders. The larger of the two. Quantities in the
        // ingredient's own unit.
        // ============================================================

        public Dictionary<int, decimal> SuggestedQuantities(out List<IngredientStockLine> lines,
            out Dictionary<int, UpcomingRequirement> upcoming)
        {
            var inventory = new IngredientInventoryService(_db);
            lines = inventory.GetStockLines().Where(l => l.Ingredient.IsActive).ToList();
            upcoming = inventory.UpcomingRequirements(14).ToDictionary(u => u.Ingredient.Id);

            var suggested = new Dictionary<int, decimal>();
            foreach (var l in lines)
            {
                decimal qty = 0m;
                if (l.IsLowStock || l.IsOutOfStock) qty = l.SuggestedOrder;

                UpcomingRequirement up;
                if (upcoming.TryGetValue(l.Ingredient.Id, out up) && up.ShortfallAfterOrders > 0m)
                    qty = Math.Max(qty, up.ShortfallAfterOrders);

                if (qty > 0m) suggested[l.Ingredient.Id] = qty;
            }

            return suggested;
        }

        // ============================================================
        // AUTOMATIC ORDERS — grouped by supplier
        // Every suggested ingredient goes to its supplier (ChooseSupplier);
        // one draft order per supplier. Ingredients without a supplier
        // are listed for the manager, never put on an order.
        // quantities: ingredient → quantity to order (own unit); null =
        // all current suggestions.
        // ============================================================

        public AutoOrderPlan PlanAutomaticOrders(IDictionary<int, decimal> quantities = null)
        {
            List<IngredientStockLine> lines;
            Dictionary<int, UpcomingRequirement> upcoming;
            var suggested = SuggestedQuantities(out lines, out upcoming);
            var wanted = quantities ?? suggested;

            var byId = lines.ToDictionary(l => l.Ingredient.Id);
            var plan = new AutoOrderPlan();

            // Ingredient → a draft (unsent) order it's already on. Only
            // the automatic suggestions skip these; ingredients the
            // manager picked on purpose are ordered as asked.
            var onDraft = quantities != null ? new Dictionary<int, string>() : _db.IngredientPurchaseOrderLines
                .Where(l => l.PurchaseOrder.Status == IngredientOrderStatus.Draft)
                .Select(l => new { l.IngredientId, l.PurchaseOrder.PoNumber })
                .ToList()
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.First().PoNumber);

            foreach (var kv in wanted.Where(kv => kv.Value > 0m))
            {
                IngredientStockLine line;
                if (!byId.TryGetValue(kv.Key, out line)) continue;     // inactive / unknown

                UpcomingRequirement up;
                upcoming.TryGetValue(kv.Key, out up);

                var item = new AutoOrderItem
                {
                    Ingredient = line.Ingredient,
                    Quantity = Math.Round(kv.Value, 2),
                    Available = line.Available,
                    ReorderLevel = line.ReorderLevel,
                    OnOrder = line.OnOrder,
                    Why = line.IsOutOfStock ? "Out of stock"
                        : line.IsLowStock ? "Low stock"
                        : up != null && up.ShortfallAfterOrders > 0m ? "Needed for " + string.Join(", ", up.NeededFor.Take(2))
                        : "Selected"
                };

                string draft;
                if (onDraft.TryGetValue(kv.Key, out draft))
                {
                    item.DraftPoNumber = draft;
                    plan.OnDraft.Add(item);
                    continue;
                }

                var link = ChooseSupplier(kv.Key);
                if (link == null)
                {
                    plan.NoSupplier.Add(item);
                    continue;
                }

                item.UnitCost = link.UnitCost;
                item.IsPreferredSupplier = link.IsPreferred;

                var group = plan.Groups.FirstOrDefault(g => g.Supplier.SupplierId == link.SupplierId);
                if (group == null)
                {
                    group = new AutoOrderGroup { Supplier = link.Supplier };
                    plan.Groups.Add(group);
                }

                group.LeadTimeDays = Math.Max(group.LeadTimeDays, link.LeadTimeDays);
                group.Items.Add(item);
            }

            plan.Groups = plan.Groups.OrderBy(g => g.Supplier.Name).ToList();
            foreach (var g in plan.Groups) g.Items = g.Items.OrderBy(i => i.Ingredient.Name).ToList();
            plan.NoSupplier = plan.NoSupplier.OrderBy(i => i.Ingredient.Name).ToList();
            return plan;
        }

        // Creates one draft per supplier (in one transaction) and returns them
        public List<IngredientPurchaseOrder> CreateAutomaticOrders(IDictionary<int, decimal> quantities, int userId)
        {
            var plan = PlanAutomaticOrders(quantities);
            if (plan.Groups.Count == 0)
                throw new InventoryException(plan.NoSupplier.Count > 0
                    ? "None of the selected ingredients has an active supplier. Link a supplier to each ingredient first."
                    : "Nothing to order — choose at least one ingredient with a quantity.");

            var created = new List<IngredientPurchaseOrder>();
            using (var tx = _db.Database.BeginTransaction())
            {
                foreach (var group in plan.Groups)
                {
                    created.Add(CreateDraft(
                        group.Supplier.SupplierId,
                        group.Items.Select(i => new OrderLineInput { IngredientId = i.Ingredient.Id, Quantity = i.Quantity, UnitCost = i.UnitCost }),
                        _now().Date.AddDays(Math.Max(1, group.LeadTimeDays)),
                        "Automatic order (low stock / upcoming shortages)",
                        userId));
                }

                tx.Commit();
            }

            return created;
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

            // Confirmed → the supplier's (test) invoice for what's coming
            RefreshTestInvoice(order.Id);
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

            RefreshTestInvoice(order.Id);
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

            RefreshTestInvoice(order.Id);
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
                    var link = ChooseSupplier(line.IngredientId, order.SupplierId);
                    chosen = link != null ? link.SupplierId : (int?)null;

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
