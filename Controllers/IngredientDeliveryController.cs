using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // Record Stock Delivery (web + phone)
    //
    //   Record → choose the order → (optional) photograph / upload
    //   the invoice → Scan reads it and matches the items → the
    //   manager checks every line → Record → stock goes up.
    //
    // Scanning never changes stock. Recording re-checks everything
    // in IngredientDeliveryService.
    // ============================================================
    [Authorize(Roles = "CafeteriaManager, Admin")]
    public class IngredientDeliveryController : Controller
    {
        private readonly DBContextClass _db;
        private readonly IngredientDeliveryService _deliveries;

        public IngredientDeliveryController()
        {
            _db = new DBContextClass();
            _deliveries = new IngredientDeliveryService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: IngredientDelivery — open orders + recent deliveries
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.OpenOrders = _deliveries.OpenOrders();
            return View(_db.IngredientDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.PurchaseOrder)
                .Include(d => d.Lines)
                .OrderByDescending(d => d.ReceivedAt)
                .Take(30)
                .ToList());
        }

        // ============================================================
        // GET: IngredientDelivery/Record?orderId=5
        // ============================================================

        [HttpGet]
        public ActionResult Record(int? orderId, bool noOrder = false)
        {
            try
            {
                var vm = _deliveries.PrepareFromOrder(orderId);
                vm.NoOrder = noOrder && !orderId.HasValue;
                PopulateSuppliers();
                return View(vm);
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // POST: IngredientDelivery/Scan — read the invoice (no saving
        // of the delivery, no stock change)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Scan(int? orderId, HttpPostedFileBase invoice)
        {
            var scanner = new InvoiceScanService();

            string error;
            var path = scanner.Save(invoice, out error);
            if (path == null)
            {
                TempData["Error"] = error;
                return RedirectToAction("Record", new { orderId });
            }

            try
            {
                var scan = scanner.Analyse(path);
                var vm = _deliveries.PrepareFromInvoice(orderId, path, Path.GetFileName(invoice.FileName), scan);
                PopulateSuppliers();
                return View("Record", vm);
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Record", new { orderId });
            }
        }

        // ============================================================
        // POST: IngredientDelivery/Record — the manager confirms
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Record")]
        public ActionResult RecordPost()
        {
            var input = ReadInput();

            try
            {
                var delivery = _deliveries.Record(input, ResolveUserId());
                TempData["Success"] = "Delivery recorded and stock updated.";
                return RedirectToAction("Details", new { id = delivery.Id });
            }
            catch (InventoryException ex)
            {
                // Show the form again with what was entered
                ViewBag.FormError = ex.Message;
                var vm = Redisplay(input);
                PopulateSuppliers();
                return View("Record", vm);
            }
        }

        // ============================================================
        // GET: IngredientDelivery/Details/5
        // ============================================================

        [HttpGet]
        public ActionResult Details(int id)
        {
            var delivery = _deliveries.GetDelivery(id);
            if (delivery == null) return HttpNotFound();

            ViewBag.ReceivedBy = _db.Users.Where(u => u.UserId == delivery.ReceivedByUserId).Select(u => u.Name).FirstOrDefault();
            return View(delivery);
        }

        // The stored invoice file (only for a recorded delivery)
        [HttpGet]
        public ActionResult Invoice(int id)
        {
            var delivery = _db.IngredientDeliveries.FirstOrDefault(d => d.Id == id);
            if (delivery == null || string.IsNullOrEmpty(delivery.InvoiceFilePath)) return HttpNotFound();

            var full = new InvoiceScanService().FullPath(delivery.InvoiceFilePath);
            if (full == null || !System.IO.File.Exists(full)) return HttpNotFound();

            return File(full, MimeMapping.GetMimeMapping(full), delivery.InvoiceFileName ?? Path.GetFileName(full));
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // Lines[n].Field from the review form
        private DeliveryInput ReadInput()
        {
            var f = Request.Form;

            var input = new DeliveryInput
            {
                Token = f["Token"],
                PurchaseOrderId = ParseInt(f["PurchaseOrderId"]),
                SupplierId = ParseInt(f["SupplierId"]),
                NoOrderConfirmed = IsTrue(f["NoOrderConfirmed"]),
                InvoiceNumber = f["InvoiceNumber"],
                InvoiceDate = ParseDate(f["InvoiceDate"]),
                InvoiceFilePath = f["InvoiceFilePath"],
                InvoiceFileName = f["InvoiceFileName"],
                Notes = f["Notes"]
            };

            int count = Math.Min(ParseInt(f["LineCount"]) ?? 0, 200);
            for (int n = 0; n < count; n++)
            {
                string p = "Lines[" + n + "].";
                var ingredientId = ParseInt(f[p + "IngredientId"]);
                var qty = IngredientUnits.ParseQuantity(f[p + "Quantity"]);

                var line = new DeliveryLineInput
                {
                    PurchaseOrderLineId = ParseInt(f[p + "PurchaseOrderLineId"]),
                    IngredientId = ingredientId ?? 0,
                    Quantity = IsTrue(f[p + "Include"]) && qty.HasValue ? qty.Value : 0m,
                    AcceptExtra = IsTrue(f[p + "AcceptExtra"]),
                    NotOnOrderConfirmed = IsTrue(f[p + "NotOnOrderConfirmed"]),
                    Note = f[p + "Note"],
                    InvoiceDescription = f[p + "InvoiceDescription"],
                    InvoiceQuantity = IngredientUnits.ParseQuantity(f[p + "InvoiceQuantity"]),
                    InvoiceUnit = f[p + "InvoiceUnit"]
                };

                // Every line is remembered so the form can be shown again;
                // excluded lines carry quantity 0 and aren't recorded
                input.Lines.Add(line);
                _redisplay.Add(Tuple.Create(line, qty, IsTrue(f[p + "Include"]), f[p + "MatchMessage"], f[p + "UnitMessage"]));
            }

            // Lines that aren't ingredients yet (unmatched, excluded) are skipped
            input.Lines = input.Lines.Where(l => l.IngredientId > 0).ToList();
            return input;
        }

        private readonly List<Tuple<DeliveryLineInput, decimal?, bool, string, string>> _redisplay =
            new List<Tuple<DeliveryLineInput, decimal?, bool, string, string>>();

        // Rebuild the review form from what was posted
        private DeliveryFormViewModel Redisplay(DeliveryInput input)
        {
            var vm = input.PurchaseOrderId.HasValue
                ? SafePrepare(input.PurchaseOrderId)
                : _deliveries.PrepareFromOrder(null);

            vm.Token = input.Token;
            vm.NoOrder = !input.PurchaseOrderId.HasValue;
            vm.SupplierId = input.SupplierId ?? vm.SupplierId;
            vm.InvoiceNumber = input.InvoiceNumber;
            vm.InvoiceDate = input.InvoiceDate;
            vm.InvoiceFilePath = InvoiceScanService.IsStoredInvoicePath(input.InvoiceFilePath) ? input.InvoiceFilePath : null;
            vm.InvoiceFileName = input.InvoiceFileName;
            vm.Notes = input.Notes;
            vm.FromScan = vm.InvoiceFilePath != null;

            var orderLines = vm.Lines.Where(l => l.PurchaseOrderLineId.HasValue).ToDictionary(l => l.PurchaseOrderLineId.Value);
            var ingredients = _db.Ingredients.ToList().ToDictionary(i => i.Id);
            vm.Lines = new List<DeliveryFormLine>();

            foreach (var r in _redisplay)
            {
                var l = r.Item1;
                Ingredient ing = null;
                if (l.IngredientId > 0) ingredients.TryGetValue(l.IngredientId, out ing);

                DeliveryFormLine fromOrder = null;
                if (l.PurchaseOrderLineId.HasValue) orderLines.TryGetValue(l.PurchaseOrderLineId.Value, out fromOrder);

                vm.Lines.Add(new DeliveryFormLine
                {
                    PurchaseOrderLineId = l.PurchaseOrderLineId,
                    IngredientId = ing != null ? (int?)ing.Id : null,
                    IngredientName = ing != null ? ing.Name : null,
                    Unit = ing != null ? ing.Unit : null,
                    DisplayUnit = ing != null ? IngredientUnits.DisplayUnit(ing.Unit) : null,
                    Ordered = fromOrder != null ? fromOrder.Ordered : null,
                    Outstanding = fromOrder != null ? fromOrder.Outstanding : null,
                    Quantity = r.Item2,
                    Include = r.Item3,
                    AcceptExtra = l.AcceptExtra,
                    NotOnOrderConfirmed = l.NotOnOrderConfirmed,
                    Note = l.Note,
                    InvoiceDescription = l.InvoiceDescription,
                    InvoiceQuantity = l.InvoiceQuantity,
                    InvoiceUnit = l.InvoiceUnit,
                    Matched = ing != null,
                    MatchMessage = r.Item4,
                    UnitMessage = r.Item5
                });
            }

            return vm;
        }

        private DeliveryFormViewModel SafePrepare(int? orderId)
        {
            try { return _deliveries.PrepareFromOrder(orderId); }
            catch (InventoryException) { return _deliveries.PrepareFromOrder(null); }
        }

        private void PopulateSuppliers()
        {
            ViewBag.Suppliers = _db.Suppliers.Where(s => s.SuppliesCafeteria && s.IsActive).OrderBy(s => s.Name).ToList();
        }

        private static int? ParseInt(string raw)
        {
            int v;
            return int.TryParse(raw, out v) ? (int?)v : null;
        }

        private static DateTime? ParseDate(string raw)
        {
            DateTime d;
            return DateTime.TryParseExact(raw ?? "", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d) ? d : (DateTime?)null;
        }

        // Checkboxes post "true" (and MVC's hidden "false")
        private static bool IsTrue(string raw)
        {
            return !string.IsNullOrEmpty(raw) && raw.Split(',').Contains("true");
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
