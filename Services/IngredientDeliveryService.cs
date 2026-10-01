using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Record Stock Delivery
    //
    //   open order (or scan the invoice) → prepared form →
    //   manager checks / corrects → Record → stock + order updated
    //
    // Prepare* only builds the form. Record re-checks everything on
    // the server: the order is open, each ingredient is active and on
    // the order (or confirmed as extra), quantities don't exceed
    // what's outstanding unless the manager accepts the extra, the
    // same form / invoice isn't recorded twice. Then, in one database
    // transaction, it saves the delivery, adds the quantities to
    // external stock (IngredientInventoryService.Apply) and updates
    // the order's received quantities and status.
    //
    // Quantities on the form are in kg / L; converted here using the
    // ingredient loaded from the database.
    // ============================================================

    public class DeliveryLineInput
    {
        public int? PurchaseOrderLineId { get; set; }
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }          // display unit (kg / L)
        public bool AcceptExtra { get; set; }
        public bool NotOnOrderConfirmed { get; set; }
        public string Note { get; set; }
        public string InvoiceDescription { get; set; }
        public decimal? InvoiceQuantity { get; set; }
        public string InvoiceUnit { get; set; }
    }

    public class DeliveryInput
    {
        public DeliveryInput()
        {
            Lines = new List<DeliveryLineInput>();
        }

        public string Token { get; set; }
        public int? PurchaseOrderId { get; set; }
        public int? SupplierId { get; set; }        // only when there's no order
        public bool NoOrderConfirmed { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string InvoiceFilePath { get; set; }
        public string InvoiceFileName { get; set; }
        public string Notes { get; set; }
        public List<DeliveryLineInput> Lines { get; set; }
    }

    public class IngredientDeliveryService
    {
        public static readonly IngredientOrderStatus[] ReceivableStatuses = IngredientInventoryService.OpenStatuses;

        private readonly DBContextClass _db;
        private readonly Func<DateTime> _now;

        public IngredientDeliveryService(DBContextClass db)
            : this(db, null)
        {
        }

        public IngredientDeliveryService(DBContextClass db, Func<DateTime> now)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _now = now ?? (() => SchoolClock.Now);
        }

        public List<IngredientPurchaseOrder> OpenOrders()
        {
            return _db.IngredientPurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Lines.Select(l => l.Ingredient))
                .Where(o => (o.Status == IngredientOrderStatus.Pending || o.Status == IngredientOrderStatus.Confirmed || o.Status == IngredientOrderStatus.PartiallyFulfilled))
                .OrderBy(o => o.RequestedDeliveryDate ?? o.OrderDate)
                .ToList();
        }

        // ============================================================
        // PREPARE — from an order
        // ============================================================

        public DeliveryFormViewModel PrepareFromOrder(int? orderId)
        {
            var vm = NewForm();

            if (!orderId.HasValue) return vm;

            var order = LoadOpenOrder(orderId.Value);
            SetOrder(vm, order);

            foreach (var line in order.Lines.OrderBy(l => l.Ingredient.Name))
            {
                vm.Lines.Add(OrderLine(line, line.Outstanding));
            }

            return vm;
        }

        // ============================================================
        // PREPARE — from a scanned invoice
        // Nothing is saved; every line is shown for confirmation.
        // ============================================================

        public DeliveryFormViewModel PrepareFromInvoice(int? orderId, string invoiceFilePath, string invoiceFileName, InvoiceScanResult scan)
        {
            var vm = NewForm();
            vm.FromScan = true;
            vm.InvoiceFilePath = invoiceFilePath;
            vm.InvoiceFileName = invoiceFileName;

            if (scan != null)
            {
                vm.DetectedSupplier = scan.VendorName;
                vm.DetectedPoReference = scan.PurchaseOrderReference;
                vm.InvoiceNumber = scan.InvoiceNumber;
                vm.InvoiceDate = scan.InvoiceDate;
                if (!scan.Success && !string.IsNullOrEmpty(scan.Error)) vm.Messages.Add(scan.Error);
            }

            // Which order? The one chosen, else our PO number on the invoice
            IngredientPurchaseOrder order = null;
            if (orderId.HasValue)
            {
                order = LoadOpenOrder(orderId.Value);
            }
            else if (scan != null)
            {
                order = FindOrderForInvoice(scan);
                if (order != null) vm.Messages.Add("Matched to purchase order " + order.PoNumber + " from the invoice.");
                else vm.Messages.Add("No open purchase order was found for this invoice — choose one, or record it without an order.");
            }

            if (order != null) SetOrder(vm, order);

            var matcher = new IngredientMatchingService(_db);
            var usedOrderLines = new HashSet<int>();

            foreach (var item in scan != null ? scan.Items : new List<InvoiceScanItem>())
            {
                var line = new DeliveryFormLine
                {
                    InvoiceDescription = item.Description,
                    InvoiceQuantity = item.Quantity,
                    InvoiceUnit = item.Unit,
                    Include = true
                };

                var match = matcher.Match(item.Description, item.ProductCode, order != null ? order.SupplierId : (int?)null);

                if (match != null && match.Confident)
                {
                    line.IngredientId = match.Ingredient.Id;
                    line.IngredientName = match.Ingredient.Name;
                    line.Matched = true;
                    line.MatchMessage = "Matched by " + match.How;
                }
                else
                {
                    line.MatchMessage = "Could not match invoice item to an existing ingredient."
                        + (match != null ? " Possible match: " + match.Ingredient.Name + "." : "");
                    if (match != null) line.IngredientId = match.Ingredient.Id;
                    line.Include = false;
                }

                if (line.IngredientId.HasValue)
                {
                    var ingredient = _db.Ingredients.Find(line.IngredientId.Value);
                    line.Unit = ingredient.Unit;
                    line.DisplayUnit = IngredientUnits.DisplayUnit(ingredient.Unit);
                    line.IngredientName = ingredient.Name;

                    // Quantity in kg / L
                    if (item.Quantity.HasValue)
                    {
                        decimal? inBase = string.IsNullOrWhiteSpace(item.Unit)
                            ? (decimal?)null
                            : IngredientUnits.Convert(item.Quantity.Value, item.Unit, ingredient.Unit);

                        if (inBase.HasValue)
                        {
                            line.Quantity = IngredientUnits.ToDisplay(inBase.Value, ingredient.Unit);
                        }
                        else
                        {
                            line.Quantity = item.Quantity.Value;
                            line.UnitMessage = string.IsNullOrWhiteSpace(item.Unit)
                                ? "No unit on the invoice — assumed " + line.DisplayUnit + ". Please check."
                                : "Invoice unit \"" + item.Unit + "\" can't be converted to " + line.DisplayUnit + ". Please enter the quantity in " + line.DisplayUnit + ".";
                            line.Include = false;
                        }
                    }
                    else
                    {
                        line.UnitMessage = "No quantity found on the invoice.";
                        line.Include = false;
                    }

                    // Link to the order line for that ingredient
                    var orderLine = order != null
                        ? order.Lines.FirstOrDefault(l => l.IngredientId == ingredient.Id && !usedOrderLines.Contains(l.Id))
                        : null;

                    if (orderLine != null)
                    {
                        usedOrderLines.Add(orderLine.Id);
                        line.PurchaseOrderLineId = orderLine.Id;
                        line.Ordered = IngredientUnits.ToDisplay(orderLine.ExpectedQuantity, ingredient.Unit);
                        line.Outstanding = IngredientUnits.ToDisplay(orderLine.Outstanding, ingredient.Unit);
                    }
                    else if (order != null)
                    {
                        line.MatchMessage += " Not on order " + order.PoNumber + ".";
                    }
                }

                vm.Lines.Add(line);
            }

            // Order items that weren't on the invoice. If the invoice
            // couldn't be read at all, fall back to the order's
            // outstanding quantities for the manager to check.
            bool invoiceRead = scan != null && scan.Success && scan.Items.Count > 0;
            if (order != null)
            {
                foreach (var orderLine in order.Lines.Where(l => !usedOrderLines.Contains(l.Id) && l.Outstanding > 0m).OrderBy(l => l.Ingredient.Name))
                {
                    var line = OrderLine(orderLine, invoiceRead ? 0m : orderLine.Outstanding);
                    if (invoiceRead)
                    {
                        line.Include = false;
                        line.MatchMessage = "Ordered, but not found on the invoice.";
                    }
                    vm.Lines.Add(line);
                }
            }

            if (scan != null && scan.Success && scan.Items.Count == 0)
                vm.Messages.Add("No item lines were found on the invoice. Enter the quantities received.");

            return vm;
        }

        // ============================================================
        // RECORD
        // ============================================================

        public IngredientDelivery Record(DeliveryInput input, int userId)
        {
            if (input == null) throw new InventoryException("No delivery was received.");

            Guid token;
            if (!Guid.TryParse(input.Token ?? "", out token))
                throw new InventoryException("This delivery form has expired. Please open it again.");

            string tokenText = token.ToString("N");
            var existing = _db.IngredientDeliveries.FirstOrDefault(d => d.SubmissionToken == tokenText);
            if (existing != null)
                throw new InventoryException("This delivery has already been recorded (delivery #" + existing.Id + ").");

            // ── Order / supplier ──
            IngredientPurchaseOrder order = null;
            int supplierId;

            if (input.PurchaseOrderId.HasValue)
            {
                order = LoadOpenOrder(input.PurchaseOrderId.Value);
                supplierId = order.SupplierId;
            }
            else
            {
                if (!input.NoOrderConfirmed)
                    throw new InventoryException("Choose the purchase order, or confirm this delivery has no order.");

                var supplier = input.SupplierId.HasValue ? _db.Suppliers.FirstOrDefault(s => s.SupplierId == input.SupplierId.Value) : null;
                if (supplier == null || !supplier.SuppliesCafeteria)
                    throw new InventoryException("Choose the cafeteria supplier who delivered.");
                supplierId = supplier.SupplierId;
            }

            // ── Invoice ──
            string invoiceNumber = Clip(input.InvoiceNumber, 60);
            if (invoiceNumber != null)
            {
                var dup = _db.IngredientDeliveries.FirstOrDefault(d => d.SupplierId == supplierId && d.InvoiceNumber == invoiceNumber);
                if (dup != null)
                    throw new InventoryException("Invoice " + invoiceNumber + " from this supplier was already recorded on delivery #" + dup.Id + ".");
            }

            if (!string.IsNullOrEmpty(input.InvoiceFilePath) && !InvoiceScanService.IsStoredInvoicePath(input.InvoiceFilePath))
                throw new InventoryException("The invoice file reference isn't valid.");

            if (input.InvoiceDate.HasValue && input.InvoiceDate.Value.Date > _now().Date.AddDays(1))
                throw new InventoryException("The invoice date can't be in the future.");

            // ── Lines ──
            var lines = (input.Lines ?? new List<DeliveryLineInput>()).Where(l => l != null && l.Quantity > 0m).ToList();
            if (lines.Count == 0)
                throw new InventoryException("Enter at least one item received.");

            var prepared = new List<PreparedLine>();
            var receivedByOrderLine = new Dictionary<int, decimal>();

            foreach (var l in lines)
            {
                var ingredient = _db.Ingredients.Find(l.IngredientId);
                if (ingredient == null || !ingredient.IsActive)
                    throw new InventoryException("One of the items isn't an active ingredient. Choose the correct ingredient.");

                decimal qty = IngredientUnits.ToBase(l.Quantity, ingredient.Unit);
                var p = new PreparedLine { Input = l, Ingredient = ingredient, Quantity = qty };

                IngredientPurchaseOrderLine orderLine = null;
                if (l.PurchaseOrderLineId.HasValue)
                {
                    if (order == null) throw new InventoryException("An order item was given without its order.");
                    orderLine = order.Lines.FirstOrDefault(x => x.Id == l.PurchaseOrderLineId.Value);
                    if (orderLine == null || orderLine.IngredientId != ingredient.Id)
                        throw new InventoryException(ingredient.Name + " doesn't match the item on order " + order.PoNumber + ".");
                }
                else if (order != null)
                {
                    // An ingredient that is on the order counts against it
                    orderLine = order.Lines.FirstOrDefault(x => x.IngredientId == ingredient.Id);
                }

                if (orderLine != null)
                {
                    decimal already;
                    receivedByOrderLine.TryGetValue(orderLine.Id, out already);
                    decimal outstanding = Math.Max(0m, orderLine.Outstanding - already);
                    receivedByOrderLine[orderLine.Id] = already + qty;

                    p.OrderLine = orderLine;
                    p.Extra = Math.Max(0m, qty - outstanding);

                    if (p.Extra > 0m && !(l.AcceptExtra && !string.IsNullOrWhiteSpace(l.Note)))
                        throw new InventoryException(string.Format(
                            "{0}: {1} received but only {2} outstanding. Tick 'accept extra' and add a note to record more.",
                            ingredient.Name, IngredientUnits.Format(qty, ingredient.Unit), IngredientUnits.Format(outstanding, ingredient.Unit)));
                }
                else
                {
                    // Not on the order (or no order at all)
                    if (order != null && !l.NotOnOrderConfirmed)
                        throw new InventoryException(ingredient.Name + " isn't on order " + order.PoNumber + ". Confirm it was delivered to record it.");
                    p.Extra = qty;
                }

                prepared.Add(p);
            }

            // ── Save: delivery, stock, order ──
            var inventory = new IngredientInventoryService(_db, _now);
            var purchasing = new IngredientPurchasingService(_db, _now);

            using (var tx = _db.Database.BeginTransaction())
            {
                try
                {
                    var delivery = new IngredientDelivery
                    {
                        PurchaseOrderId = order != null ? (int?)order.Id : null,
                        SupplierId = supplierId,
                        ReceivedAt = DateTime.UtcNow,
                        ReceivedByUserId = userId,
                        InvoiceNumber = invoiceNumber,
                        InvoiceDate = input.InvoiceDate.HasValue ? input.InvoiceDate.Value.Date : (DateTime?)null,
                        InvoiceFilePath = string.IsNullOrEmpty(input.InvoiceFilePath) ? null : input.InvoiceFilePath,
                        InvoiceFileName = Clip(input.InvoiceFileName, 200),
                        Notes = Clip(input.Notes, 1000),
                        SubmissionToken = tokenText
                    };

                    foreach (var p in prepared)
                    {
                        delivery.Lines.Add(new IngredientDeliveryLine
                        {
                            IngredientId = p.Ingredient.Id,
                            PurchaseOrderLineId = p.OrderLine != null ? (int?)p.OrderLine.Id : null,
                            QuantityReceived = p.Quantity,
                            ExtraQuantity = p.Extra,
                            Note = Clip(p.Input.Note, 300),
                            InvoiceDescription = Clip(p.Input.InvoiceDescription, 200),
                            InvoiceQuantity = p.Input.InvoiceQuantity,
                            InvoiceUnit = Clip(p.Input.InvoiceUnit, 20)
                        });

                        if (p.OrderLine != null) p.OrderLine.QuantityReceived += p.Quantity;
                    }

                    _db.IngredientDeliveries.Add(delivery);
                    _db.SaveChanges();

                    var supplierName = _db.Suppliers.Where(s => s.SupplierId == supplierId).Select(s => s.Name).FirstOrDefault();
                    string reference = order != null ? order.PoNumber : "DEL-" + delivery.Id;

                    foreach (var p in prepared)
                    {
                        inventory.Apply(p.Ingredient.Id, StockTransactionType.Delivery, StockSource.External, p.Quantity,
                            reference, "Delivery from " + supplierName + (invoiceNumber != null ? ", invoice " + invoiceNumber : ""),
                            userId, order != null ? (int?)order.Id : null, delivery.Id);
                    }

                    if (order != null)
                    {
                        purchasing.UpdateStatusFromReceipts(order);
                        _db.SaveChanges();
                    }

                    tx.Commit();
                    return delivery;
                }
                catch (DbUpdateException)
                {
                    tx.Rollback();
                    throw new InventoryException("This delivery has already been recorded.");
                }
            }
        }

        public IngredientDelivery GetDelivery(int id)
        {
            return _db.IngredientDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.PurchaseOrder)
                .Include(d => d.Lines.Select(l => l.Ingredient))
                .FirstOrDefault(d => d.Id == id);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private class PreparedLine
        {
            public DeliveryLineInput Input { get; set; }
            public Ingredient Ingredient { get; set; }
            public decimal Quantity { get; set; }
            public decimal Extra { get; set; }
            public IngredientPurchaseOrderLine OrderLine { get; set; }
        }

        private DeliveryFormViewModel NewForm()
        {
            var vm = new DeliveryFormViewModel
            {
                Token = Guid.NewGuid().ToString("N"),
                OpenOrders = OpenOrders(),
                ScanAvailable = new InvoiceScanService().IsConfigured
            };

            vm.IngredientOptions = _db.Ingredients
                .Where(i => i.IsActive)
                .OrderBy(i => i.Name)
                .ToList()
                .Select(i => new IngredientOption { Id = i.Id, Name = i.Name, DisplayUnit = IngredientUnits.DisplayUnit(i.Unit) })
                .ToList();

            return vm;
        }

        private IngredientPurchaseOrder LoadOpenOrder(int orderId)
        {
            var order = _db.IngredientPurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Lines.Select(l => l.Ingredient))
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null) throw new InventoryException("Purchase order not found.");
            if (!ReceivableStatuses.Contains(order.Status))
                throw new InventoryException("Order " + order.PoNumber + " is " + order.Status + " — deliveries can only be recorded on orders that are sent and not yet complete.");

            return order;
        }

        private static void SetOrder(DeliveryFormViewModel vm, IngredientPurchaseOrder order)
        {
            vm.PurchaseOrderId = order.Id;
            vm.PoNumber = order.PoNumber;
            vm.SupplierId = order.SupplierId;
            vm.SupplierName = order.Supplier != null ? order.Supplier.Name : "";
        }

        private static DeliveryFormLine OrderLine(IngredientPurchaseOrderLine line, decimal defaultQuantity)
        {
            return new DeliveryFormLine
            {
                PurchaseOrderLineId = line.Id,
                IngredientId = line.IngredientId,
                IngredientName = line.Ingredient.Name,
                Unit = line.Ingredient.Unit,
                DisplayUnit = IngredientUnits.DisplayUnit(line.Ingredient.Unit),
                Ordered = IngredientUnits.ToDisplay(line.ExpectedQuantity, line.Ingredient.Unit),
                Outstanding = IngredientUnits.ToDisplay(line.Outstanding, line.Ingredient.Unit),
                Quantity = IngredientUnits.ToDisplay(defaultQuantity, line.Ingredient.Unit),
                Include = defaultQuantity > 0m,
                Matched = true
            };
        }

        // Our PO number anywhere on the invoice, else the supplier's only open order
        private IngredientPurchaseOrder FindOrderForInvoice(InvoiceScanResult scan)
        {
            var open = OpenOrders();
            string text = ((scan.PurchaseOrderReference ?? "") + " " + (scan.Content ?? "")).ToUpperInvariant();

            var byNumber = open.FirstOrDefault(o => text.Contains(o.PoNumber.ToUpperInvariant()));
            if (byNumber != null) return byNumber;

            if (!string.IsNullOrWhiteSpace(scan.VendorName))
            {
                var vendor = IngredientMatchingService.Normalise(scan.VendorName);
                var bySupplier = open
                    .Where(o => o.Supplier != null && IngredientMatchingService.Normalise(o.Supplier.Name) is string n
                                && n.Length > 0 && (vendor.Contains(n) || n.Contains(vendor)))
                    .ToList();

                if (bySupplier.Count == 1) return bySupplier[0];
            }

            return null;
        }

        private static string Clip(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
