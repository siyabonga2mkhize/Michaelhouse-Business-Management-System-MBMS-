using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Mobile — cafeteria inventory (Record Stock Delivery)
    //
    //   GET  /api/cafeteria-inventory/stock         stock + low / out
    //   GET  /api/cafeteria-inventory/open-orders   orders awaiting delivery
    //   GET  /api/cafeteria-inventory/delivery-form?orderId=5
    //   POST /api/cafeteria-inventory/scan          multipart: invoice (+ orderId)
    //   POST /api/cafeteria-inventory/deliveries    JSON delivery (kg / L)
    //
    // Same services and checks as the web pages. Scanning only reads
    // the invoice; stock changes when the delivery is posted.
    // ============================================================
    [RoutePrefix("api/cafeteria-inventory")]
    [Authorize(Roles = "CafeteriaManager, Admin")]
    public class CafeteriaInventoryApiController : Controller
    {
        private readonly DBContextClass _db;

        public CafeteriaInventoryApiController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        [Route("stock")]
        public JsonResult Stock()
        {
            var lines = new IngredientInventoryService(_db).GetStockLines().Where(l => l.Ingredient.IsActive);

            return Json(new
            {
                ok = true,
                ingredients = lines.Select(l => new
                {
                    id = l.Ingredient.Id,
                    name = l.Ingredient.Name,
                    unit = IngredientUnits.DisplayUnit(l.Ingredient.Unit),
                    available = IngredientUnits.ToDisplay(l.Available, l.Ingredient.Unit),
                    farm = IngredientUnits.ToDisplay(l.Farm, l.Ingredient.Unit),
                    external = IngredientUnits.ToDisplay(l.External, l.Ingredient.Unit),
                    reorderLevel = IngredientUnits.ToDisplay(l.ReorderLevel, l.Ingredient.Unit),
                    onOrder = IngredientUnits.ToDisplay(l.OnOrder, l.Ingredient.Unit),
                    status = l.Status
                })
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [Route("open-orders")]
        public JsonResult OpenOrders()
        {
            var orders = new IngredientDeliveryService(_db).OpenOrders();

            return Json(new
            {
                ok = true,
                orders = orders.Select(o => new
                {
                    id = o.Id,
                    poNumber = o.PoNumber,
                    supplier = o.Supplier != null ? o.Supplier.Name : "",
                    status = o.Status.ToString(),
                    requestedDelivery = o.RequestedDeliveryDate.HasValue ? o.RequestedDeliveryDate.Value.ToString("yyyy-MM-dd") : null,
                    lines = o.Lines.Select(l => new
                    {
                        lineId = l.Id,
                        ingredientId = l.IngredientId,
                        name = l.Ingredient.Name,
                        unit = IngredientUnits.DisplayUnit(l.Ingredient.Unit),
                        expected = IngredientUnits.ToDisplay(l.ExpectedQuantity, l.Ingredient.Unit),
                        outstanding = IngredientUnits.ToDisplay(l.Outstanding, l.Ingredient.Unit)
                    })
                })
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [Route("delivery-form")]
        public JsonResult DeliveryForm(int? orderId)
        {
            try
            {
                return Json(FormJson(new IngredientDeliveryService(_db).PrepareFromOrder(orderId)), JsonRequestBehavior.AllowGet);
            }
            catch (InventoryException ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Multipart: "invoice" file, optional "orderId"
        [HttpPost]
        [Route("scan")]
        public JsonResult Scan()
        {
            var file = Request.Files["invoice"];
            int orderIdValue;
            int? orderId = int.TryParse(Request.Form["orderId"], out orderIdValue) ? orderIdValue : (int?)null;

            var scanner = new InvoiceScanService();
            string error;
            var path = scanner.Save(file, out error);
            if (path == null) return Json(new { ok = false, error });

            try
            {
                var scan = scanner.Analyse(path);
                var vm = new IngredientDeliveryService(_db).PrepareFromInvoice(orderId, path, Path.GetFileName(file.FileName), scan);
                return Json(FormJson(vm));
            }
            catch (InventoryException ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // Body: DeliveryInput (quantities in kg / L, as on the form)
        [HttpPost]
        [Route("deliveries")]
        public JsonResult RecordDelivery()
        {
            DeliveryInput input;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    input = JsonConvert.DeserializeObject<DeliveryInput>(reader.ReadToEnd());
                }
            }
            catch (JsonException)
            {
                return Json(new { ok = false, error = "The delivery could not be read." });
            }

            try
            {
                int userId;
                int.TryParse(Convert.ToString(Session["UserId"]), out userId);

                var delivery = new IngredientDeliveryService(_db).Record(input, userId);
                return Json(new { ok = true, deliveryId = delivery.Id, message = "Delivery recorded and stock updated." });
            }
            catch (InventoryException ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        private static object FormJson(DeliveryFormViewModel vm)
        {
            return new
            {
                ok = true,
                token = vm.Token,
                purchaseOrderId = vm.PurchaseOrderId,
                poNumber = vm.PoNumber,
                supplierId = vm.SupplierId,
                supplier = vm.SupplierName,
                invoiceNumber = vm.InvoiceNumber,
                invoiceDate = vm.InvoiceDate.HasValue ? vm.InvoiceDate.Value.ToString("yyyy-MM-dd") : null,
                invoiceFilePath = vm.InvoiceFilePath,
                invoiceFileName = vm.InvoiceFileName,
                detectedSupplier = vm.DetectedSupplier,
                messages = vm.Messages,
                lines = vm.Lines.Select(l => new
                {
                    purchaseOrderLineId = l.PurchaseOrderLineId,
                    ingredientId = l.IngredientId,
                    ingredient = l.IngredientName,
                    unit = l.DisplayUnit,
                    ordered = l.Ordered,
                    outstanding = l.Outstanding,
                    quantity = l.Quantity,
                    include = l.Include,
                    matched = l.Matched,
                    matchMessage = l.MatchMessage,
                    unitMessage = l.UnitMessage,
                    invoiceDescription = l.InvoiceDescription,
                    invoiceQuantity = l.InvoiceQuantity,
                    invoiceUnit = l.InvoiceUnit
                })
            };
        }
    }
}
