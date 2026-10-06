using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // Place Stock Order / Amend Stock Order (cafeteria ingredients)
    //
    //   Create (from low stock / upcoming shortages) → Draft (review)
    //   → Send → supplier confirms quantities → shortfall: accept,
    //   amend, or re-order from another supplier → deliveries.
    //
    // Every rule lives in IngredientPurchasingService; this controller
    // reads the form (kg / L, converted with each ingredient's own unit
    // from the database) and shows the result.
    // ============================================================
    [Authorize(Roles = "CafeteriaManager, Admin")]
    public class IngredientOrderController : Controller
    {
        private readonly DBContextClass _db;
        private readonly IngredientPurchasingService _purchasing;
        private readonly IngredientInventoryService _inventory;

        public IngredientOrderController()
        {
            _db = new DBContextClass();
            _purchasing = new IngredientPurchasingService(_db);
            _inventory = new IngredientInventoryService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: IngredientOrder?status=open
        // ============================================================

        [HttpGet]
        public ActionResult Index(string status)
        {
            var query = _db.IngredientPurchaseOrders.Include(o => o.Supplier).Include(o => o.Lines);

            switch ((status ?? "open").ToLowerInvariant())
            {
                case "draft": query = query.Where(o => o.Status == IngredientOrderStatus.Draft); break;
                case "done": query = query.Where(o => o.Status == IngredientOrderStatus.Fulfilled || o.Status == IngredientOrderStatus.Cancelled); break;
                case "all": break;
                default:
                    status = "open";
                    query = query.Where(o => o.Status == IngredientOrderStatus.Draft || o.Status == IngredientOrderStatus.Pending
                                             || o.Status == IngredientOrderStatus.Confirmed || o.Status == IngredientOrderStatus.PartiallyFulfilled);
                    break;
            }

            ViewBag.Status = status;
            return View(query.OrderByDescending(o => o.OrderDate).Take(200).ToList());
        }

        // ============================================================
        // GET: IngredientOrder/Create?supplierId=3&from=low
        // Pre-fills the ingredients that need ordering: low stock and
        // upcoming kitchen shortages (or the ones ticked on the stock
        // page), for the chosen supplier where linked.
        // ============================================================

        [HttpGet]
        public ActionResult Create(int? supplierId, string from, int[] ingredientIds)
        {
            List<IngredientStockLine> lines;
            Dictionary<int, UpcomingRequirement> upcoming;
            var suggested = _purchasing.SuggestedQuantities(out lines, out upcoming);

            List<int> preselect;
            if (ingredientIds != null && ingredientIds.Length > 0)
            {
                preselect = ingredientIds.Distinct().ToList();
            }
            else if (supplierId.HasValue)
            {
                // Only what this supplier supplies. (Everything that needs
                // ordering, across suppliers, is Automatic orders.)
                var supplied = _db.IngredientSuppliers.Where(x => x.SupplierId == supplierId.Value && x.IsActive).Select(x => x.IngredientId).ToList();
                preselect = suggested.Keys.Where(supplied.Contains).ToList();
            }
            else
            {
                preselect = new List<int>();
            }

            ViewBag.Suggested = suggested;
            ViewBag.Preselect = preselect;
            ViewBag.Upcoming = upcoming;
            PopulateForm(supplierId, lines);

            return View(new IngredientPurchaseOrder { SupplierId = supplierId ?? 0, RequestedDeliveryDate = SchoolClock.Today.AddDays(3) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(int supplierId, DateTime? requestedDeliveryDate, string notes)
        {
            try
            {
                var order = _purchasing.CreateDraft(supplierId, ReadLines(), requestedDeliveryDate, notes, ResolveUserId());
                TempData["Success"] = "Draft order " + order.PoNumber + " created. Check it, then send it to the supplier.";
                return RedirectToAction("Details", new { id = order.Id });
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Create", new { supplierId });
            }
        }

        // ============================================================
        // GET: IngredientOrder/Auto?ingredientIds=1&ingredientIds=2
        // Automatic orders: every ingredient that needs ordering (or the
        // ones ticked on the stock page) goes to its supplier — one
        // purchase order per supplier. Shown for review first;
        // ingredients with no supplier are listed, not ordered.
        // POST: creates the drafts (quantities may be changed / unticked).
        // ============================================================

        [HttpGet]
        public ActionResult Auto(int[] ingredientIds)
        {
            IDictionary<int, decimal> quantities = null;

            if (ingredientIds != null && ingredientIds.Length > 0)
            {
                // Ticked on the stock page: their suggestion, else the
                // amount needed to reach the target level
                List<IngredientStockLine> lines;
                Dictionary<int, UpcomingRequirement> upcoming;
                var suggested = _purchasing.SuggestedQuantities(out lines, out upcoming);
                var byId = lines.ToDictionary(l => l.Ingredient.Id);

                quantities = ingredientIds.Distinct()
                    .Where(byId.ContainsKey)
                    .ToDictionary(id => id, id => suggested.ContainsKey(id) ? suggested[id] : byId[id].SuggestedOrder);
            }

            return View(_purchasing.PlanAutomaticOrders(quantities));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Auto")]
        public ActionResult AutoCreate()
        {
            try
            {
                // include_{ingredientId}=true + qty_{ingredientId} (kg / L)
                var ids = Request.Form.AllKeys
                    .Where(k => k != null && k.StartsWith("include_") && Request.Form[k].Split(',').Contains("true"))
                    .Select(k => { int i; return int.TryParse(k.Substring(8), out i) ? i : 0; })
                    .Where(i => i > 0)
                    .Distinct()
                    .ToList();

                var ingredients = _db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionary(i => i.Id);
                var quantities = new Dictionary<int, decimal>();

                foreach (var id in ids.Where(ingredients.ContainsKey))
                {
                    var qty = IngredientUnits.ParseQuantity(Request.Form["qty_" + id]);
                    if (!qty.HasValue || qty.Value <= 0m)
                        throw new InventoryException(ingredients[id].Name + ": enter the quantity to order in " + IngredientUnits.DisplayUnit(ingredients[id].Unit) + ".");
                    quantities[id] = IngredientUnits.ToBase(qty.Value, ingredients[id].Unit);
                }

                var created = _purchasing.CreateAutomaticOrders(quantities, ResolveUserId());

                TempData["Success"] = string.Format(
                    "{0} draft order{1} created, one per supplier: {2}. Check each, then send it to the supplier.",
                    created.Count, created.Count == 1 ? "" : "s",
                    string.Join(", ", created.Select(o => o.PoNumber)));

                return created.Count == 1
                    ? RedirectToAction("Details", new { id = created[0].Id })
                    : RedirectToAction("Index", new { status = "draft" });
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Auto");
            }
        }

        // ============================================================
        // GET: IngredientOrder/TestInvoice/5 — the order's generated
        // test invoice (PDF). Use it on Record Delivery to test the scan.
        // ============================================================

        [HttpGet]
        public ActionResult TestInvoice(int id)
        {
            var order = _db.IngredientPurchaseOrders.FirstOrDefault(o => o.Id == id);
            if (order == null || string.IsNullOrEmpty(order.TestInvoiceFilePath)) return HttpNotFound();

            var full = new InvoiceScanService().FullPath(order.TestInvoiceFilePath);
            if (full == null || !System.IO.File.Exists(full)) return HttpNotFound();

            return File(full, "application/pdf", order.TestInvoiceNumber + ".pdf");
        }

        // ============================================================
        // Draft editing
        // ============================================================

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var order = _purchasing.GetOrder(id);
            if (order == null) return HttpNotFound();
            if (order.Status != IngredientOrderStatus.Draft)
            {
                TempData["Error"] = "Only draft orders can be edited. Use the amend options instead.";
                return RedirectToAction("Details", new { id });
            }

            var lines = _inventory.GetStockLines().Where(l => l.Ingredient.IsActive).ToList();
            ViewBag.Suggested = order.Lines.ToDictionary(l => l.IngredientId, l => l.QuantityOrdered);
            ViewBag.Preselect = order.Lines.Select(l => l.IngredientId).ToList();
            ViewBag.Upcoming = _inventory.UpcomingRequirements(14).ToDictionary(u => u.Ingredient.Id);
            PopulateForm(order.SupplierId, lines);

            return View("Create", order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, int supplierId, DateTime? requestedDeliveryDate, string notes)
        {
            try
            {
                _purchasing.UpdateDraft(id, supplierId, ReadLines(), requestedDeliveryDate, notes);
                TempData["Success"] = "Draft order updated.";
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Edit", new { id });
            }

            return RedirectToAction("Details", new { id });
        }

        // ============================================================
        // GET: IngredientOrder/Details/5
        // ============================================================

        [HttpGet]
        public ActionResult Details(int id)
        {
            var order = _purchasing.GetOrder(id);
            if (order == null) return HttpNotFound();

            ViewBag.Deliveries = _db.IngredientDeliveries
                .Include(d => d.Lines)
                .Where(d => d.PurchaseOrderId == id)
                .OrderByDescending(d => d.ReceivedAt)
                .ToList();
            ViewBag.ShortfallOrders = _db.IngredientPurchaseOrders
                .Include(o => o.Supplier)
                .Where(o => o.ShortfallOfOrderId == id)
                .ToList();
            ViewBag.OtherSuppliers = _db.Suppliers
                .Where(s => s.SuppliesCafeteria && s.IsActive && s.SupplierId != order.SupplierId)
                .OrderBy(s => s.Name)
                .ToList();
            ViewBag.CreatedBy = _db.Users.Where(u => u.UserId == order.CreatedByUserId).Select(u => u.Name).FirstOrDefault();

            return View(order);
        }

        // ============================================================
        // Status actions
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Send(int id)
        {
            return Run(id, () =>
            {
                bool emailed = _purchasing.Send(id);
                return emailed ? "Order sent and emailed to the supplier." : "Order marked as sent. No supplier email on file — send it to them directly.";
            });
        }

        // Supplier's confirmed quantities: confirmed_{lineId} in kg / L
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm(int id)
        {
            return Run(id, () =>
            {
                var order = RequireOrder(id);
                var confirmed = new Dictionary<int, decimal>();

                foreach (var line in order.Lines)
                {
                    var raw = Request.Form["confirmed_" + line.Id];
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    var qty = IngredientUnits.ParseQuantity(raw);
                    if (!qty.HasValue) throw new InventoryException(line.Ingredient.Name + ": enter the confirmed quantity as a number.");
                    confirmed[line.Id] = IngredientUnits.ToBase(qty.Value, line.Ingredient.Unit);
                }

                if (confirmed.Count == 0) throw new InventoryException("Enter the quantities the supplier confirmed.");

                _purchasing.RecordSupplierConfirmation(id, confirmed);

                var updated = RequireOrder(id);
                var shortfalls = updated.Lines.Where(l => l.Shortfall > 0m && l.ShortfallAction == ShortfallAction.None).ToList();
                var message = shortfalls.Count == 0
                    ? "Supplier confirmation recorded."
                    : string.Format("Supplier confirmation recorded. {0} item(s) are short — choose what to do with the shortfall below.", shortfalls.Count);
                return WithInvoice(message);
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Amend(int id, int lineId, string quantity, string note)
        {
            return Run(id, () =>
            {
                var line = RequireOrder(id).Lines.FirstOrDefault(l => l.Id == lineId);
                if (line == null) throw new InventoryException("That item isn't on this order.");

                var qty = IngredientUnits.ParseQuantity(quantity);
                if (!qty.HasValue) throw new InventoryException("Enter the new quantity in " + IngredientUnits.DisplayUnit(line.Ingredient.Unit) + ".");

                _purchasing.AmendLine(id, lineId, IngredientUnits.ToBase(qty.Value, line.Ingredient.Unit), note);
                return WithInvoice(line.Ingredient.Name + " amended.");
            });
        }

        // action = "accept" | "reorder"; supplierId empty = automatic
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Shortfall(int id, int[] lineIds, string shortfallAction, int? supplierId, string note)
        {
            return Run(id, () =>
            {
                if (shortfallAction == "accept")
                {
                    _purchasing.AcceptShortfall(id, lineIds, note);
                    return WithInvoice("Shortfall accepted.");
                }

                var created = _purchasing.ReorderShortfall(id, lineIds, supplierId, ResolveUserId());
                return "Shortfall re-ordered on " + string.Join(", ", created.Select(o => o.PoNumber))
                    + " (draft — check and send it). That order gets its own test invoice once its supplier confirms it.";
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason)
        {
            return Run(id, () =>
            {
                bool wasSent = RequireOrder(id).Status != IngredientOrderStatus.Draft;
                _purchasing.Cancel(id, reason);
                return wasSent ? "Order cancelled. It was already sent — let the supplier know." : "Draft order cancelled.";
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Close(int id, string reason)
        {
            return Run(id, () =>
            {
                _purchasing.Close(id, reason);
                return "Order closed — the remaining items won't be delivered.";
            });
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // ingredientId[] + quantity[] (kg / L) from the order form
        private List<OrderLineInput> ReadLines()
        {
            var ids = Request.Form.GetValues("ingredientId") ?? new string[0];
            var qtys = Request.Form.GetValues("quantity") ?? new string[0];
            var costs = Request.Form.GetValues("unitCost") ?? new string[0];

            var parsedIds = ids.Select(x => { int i; return int.TryParse(x, out i) ? i : 0; }).ToList();
            var units = _db.Ingredients.Where(i => parsedIds.Contains(i.Id)).ToDictionary(i => i.Id, i => i.Unit);

            var result = new List<OrderLineInput>();
            for (int n = 0; n < parsedIds.Count && n < qtys.Length; n++)
            {
                int ingredientId = parsedIds[n];
                if (ingredientId == 0 || string.IsNullOrWhiteSpace(qtys[n])) continue;

                var qty = IngredientUnits.ParseQuantity(qtys[n]);
                if (!qty.HasValue || qty.Value < 0m) throw new InventoryException("Enter quantities as numbers.");
                if (!units.ContainsKey(ingredientId)) throw new InventoryException("One of the ingredients doesn't exist.");

                decimal? cost = n < costs.Length ? IngredientUnits.ParseQuantity(costs[n]) : null;

                result.Add(new OrderLineInput
                {
                    IngredientId = ingredientId,
                    Quantity = IngredientUnits.ToBase(qty.Value, units[ingredientId]),
                    UnitCost = cost
                });
            }

            return result;
        }

        private void PopulateForm(int? supplierId, List<IngredientStockLine> lines)
        {
            ViewBag.Suppliers = _purchasing.CafeteriaSuppliers();
            ViewBag.StockLines = lines;
            ViewBag.SupplierLinks = _db.IngredientSuppliers
                .Where(x => x.IsActive)
                .ToList()
                .GroupBy(x => x.SupplierId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.IngredientId).ToList());
            ViewBag.SupplierId = supplierId;
        }

        private IngredientPurchaseOrder RequireOrder(int id)
        {
            var order = _purchasing.GetOrder(id);
            if (order == null) throw new InventoryException("Purchase order not found.");
            return order;
        }

        private ActionResult Run(int id, Func<string> action)
        {
            try
            {
                TempData["Success"] = action();
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        // "… Test invoice INV-TEST-… updated to match the order."
        private string WithInvoice(string message)
        {
            return string.IsNullOrEmpty(_purchasing.InvoiceMessage) ? message : message + " " + _purchasing.InvoiceMessage;
        }

        private int ResolveUserId()
        {
            if (Session["UserId"] == null) return 0;
            int id;
            int.TryParse(Session["UserId"].ToString(), out id);
            return id;
        }
    }
}
