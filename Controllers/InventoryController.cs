using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    [AdminOnly]
    public class InventoryController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private readonly InventoryService _inventoryService = new InventoryService();

        // ─── Dashboard ────────────────────────────────────────────────────────────

        public ActionResult Index()
        {
            var products = db.Products
                .Include("Category")
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToList();

            var lowStock = _inventoryService.GetAllLowStockProducts();
            var openOrders = db.PurchaseOrders
                .Include("Supplier")
                .Where(po => po.Status == PurchaseOrderStatus.Draft ||
                             po.Status == PurchaseOrderStatus.Sent)
                .OrderByDescending(po => po.CreatedAt)
                .ToList();

            ViewBag.LowStockProducts = lowStock;
            ViewBag.OpenOrders = openOrders;
            ViewBag.TotalProducts = products.Count;
            ViewBag.LowStockCount = lowStock.Count;
            ViewBag.TotalStock = products.Sum(p => p.QuantityInStock);

            return View(products);
        }

        // ─── Stock History for a product ─────────────────────────────────────────

        public ActionResult StockHistory(int id)
        {
            var product = db.Products.Include("Category").FirstOrDefault(p => p.Id == id);
            if (product == null) return HttpNotFound();

            var history = _inventoryService.GetStockHistory(id);
            var suppliers = db.SupplierProducts
                .Include("Supplier")
                .Where(sp => sp.ProductId == id)
                .ToList();

            ViewBag.Product = product;
            ViewBag.Suppliers = suppliers;
            return View(history);
        }

        // ─── Adjust Stock ─────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AdjustStock(int productId, int newQuantity, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please provide a reason for the stock adjustment.";
                return RedirectToAction("StockHistory", new { id = productId });
            }

            _inventoryService.AdjustStock(productId, newQuantity, reason);
            TempData["Success"] = "Stock adjusted successfully.";
            return RedirectToAction("StockHistory", new { id = productId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}