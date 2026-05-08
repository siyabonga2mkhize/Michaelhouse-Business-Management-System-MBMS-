using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;

namespace Michaelhouse.Controllers
{
    [InventoryManagerOnly]
    public class SupplierController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        // ─── List ─────────────────────────────────────────────────────────────────

        public ActionResult Index()
        {
            var suppliers = db.Suppliers
                .Include("SupplierProducts")
                .Include("PurchaseOrders")
                .OrderBy(s => s.Name)
                .ToList();

            return View(suppliers);
        }

        // ─── Create ───────────────────────────────────────────────────────────────

        public ActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Supplier model)
        {
            if (!ModelState.IsValid) return View(model);

            db.Suppliers.Add(model);
            db.SaveChanges();

            TempData["Success"] = $"Supplier '{model.Name}' created.";
            return RedirectToAction("Details", new { id = model.SupplierId });
        }

        // ─── Details ──────────────────────────────────────────────────────────────

        public ActionResult Details(int id)
        {
            var supplier = db.Suppliers
                .Include("SupplierProducts")
                .Include("SupplierProducts.Product")
                .Include("SupplierProducts.Product.Category")
                .Include("PurchaseOrders")
                .FirstOrDefault(s => s.SupplierId == id);

            if (supplier == null) return HttpNotFound();

            // Products not yet linked to this supplier (for the "link product" form)
            var linkedProductIds = supplier.SupplierProducts
                .Select(sp => sp.ProductId).ToList();

            ViewBag.UnlinkedProducts = db.Products
                .Include("Category")
                .Where(p => p.IsActive && !linkedProductIds.Contains(p.Id))
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToList();

            return View(supplier);
        }

        // ─── Edit ─────────────────────────────────────────────────────────────────

        public ActionResult Edit(int id)
        {
            var supplier = db.Suppliers.Find(id);
            if (supplier == null) return HttpNotFound();
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Supplier model)
        {
            if (!ModelState.IsValid) return View(model);

            var supplier = db.Suppliers.Find(model.SupplierId);
            if (supplier == null) return HttpNotFound();

            supplier.Name = model.Name;
            supplier.ContactPerson = model.ContactPerson;
            supplier.Email = model.Email;
            supplier.Phone = model.Phone;
            supplier.Address = model.Address;
            supplier.Website = model.Website;
            supplier.PaymentTermsDays = model.PaymentTermsDays;
            supplier.IsActive = model.IsActive;

            db.SaveChanges();
            TempData["Success"] = "Supplier updated.";
            return RedirectToAction("Details", new { id = model.SupplierId });
        }

        // ─── Link Product to Supplier ─────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkProduct(int supplierId, int productId,
            decimal unitCost, string supplierSku,
            int minOrderQty, int leadTimeDays, bool isPreferred)
        {
            // If setting as preferred, unset other preferred suppliers for this product
            if (isPreferred)
            {
                var others = db.SupplierProducts
                    .Where(sp => sp.ProductId == productId && sp.IsPreferred);
                foreach (var sp in others)
                    sp.IsPreferred = false;
            }

            // Check not already linked
            var existing = db.SupplierProducts
                .FirstOrDefault(sp => sp.SupplierId == supplierId &&
                                       sp.ProductId == productId);

            if (existing != null)
            {
                // Update existing
                existing.UnitCost = unitCost;
                existing.SupplierSku = supplierSku;
                existing.MinOrderQty = minOrderQty;
                existing.LeadTimeDays = leadTimeDays;
                existing.IsPreferred = isPreferred;
            }
            else
            {
                db.SupplierProducts.Add(new SupplierProduct
                {
                    SupplierId = supplierId,
                    ProductId = productId,
                    UnitCost = unitCost,
                    SupplierSku = supplierSku,
                    MinOrderQty = minOrderQty,
                    LeadTimeDays = leadTimeDays,
                    IsPreferred = isPreferred
                });
            }

            db.SaveChanges();
            TempData["Success"] = "Product linked to supplier.";
            return RedirectToAction("Details", new { id = supplierId });
        }

        // ─── Remove Product Link ──────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UnlinkProduct(int supplierProductId, int supplierId)
        {
            var sp = db.SupplierProducts.Find(supplierProductId);
            if (sp != null)
            {
                db.SupplierProducts.Remove(sp);
                db.SaveChanges();
                TempData["Success"] = "Product unlinked.";
            }
            return RedirectToAction("Details", new { id = supplierId });
        }

        // ─── Toggle Active ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleActive(int id)
        {
            var supplier = db.Suppliers.Find(id);
            if (supplier == null) return HttpNotFound();
            supplier.IsActive = !supplier.IsActive;
            db.SaveChanges();
            TempData["Success"] = $"Supplier {(supplier.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}