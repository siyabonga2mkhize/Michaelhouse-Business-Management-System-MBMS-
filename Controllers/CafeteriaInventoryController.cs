using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // Cafeteria Inventory — stock, ingredients and suppliers
    //
    // Chef: can look (dashboard, stock, ingredient history).
    // Cafeteria Manager / Admin: also adjust stock, record farm
    // produce, set levels and manage cafeteria suppliers.
    // Stock itself only changes through IngredientInventoryService.
    // ============================================================
    [Authorize(Roles = "CafeteriaManager, Admin, Chef")]
    public class CafeteriaInventoryController : Controller
    {
        private const string ManagerRoles = "CafeteriaManager, Admin";

        private readonly DBContextClass _db;
        private readonly IngredientInventoryService _inventory;

        public CafeteriaInventoryController()
        {
            _db = new DBContextClass();
            _inventory = new IngredientInventoryService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: CafeteriaInventory  — dashboard
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            var lines = _inventory.GetStockLines();
            var upcoming = _inventory.UpcomingRequirements(14);

            ViewBag.Summary = _inventory.Summary(lines, upcoming);
            ViewBag.LowStock = lines.Where(l => l.IsLowStock || l.IsOutOfStock).OrderBy(l => l.Available - l.ReorderLevel).ToList();
            ViewBag.Upcoming = upcoming.Where(u => u.Shortfall > 0m).OrderByDescending(u => u.ShortfallAfterOrders).ToList();
            ViewBag.Orders = _db.IngredientPurchaseOrders
                .Include(o => o.Supplier)
                .Where(o => o.Status == IngredientOrderStatus.Draft || o.Status == IngredientOrderStatus.Pending
                            || o.Status == IngredientOrderStatus.Confirmed || o.Status == IngredientOrderStatus.PartiallyFulfilled)
                .OrderBy(o => o.RequestedDeliveryDate ?? o.OrderDate)
                .Take(15)
                .ToList();
            ViewBag.IsManager = IsManager();

            return View();
        }

        // ============================================================
        // GET: CafeteriaInventory/Stock?filter=low
        // ============================================================

        [HttpGet]
        public ActionResult Stock(string filter)
        {
            var lines = _inventory.GetStockLines();

            switch ((filter ?? "").ToLowerInvariant())
            {
                case "low": lines = lines.Where(l => l.IsLowStock || l.IsOutOfStock).ToList(); break;
                case "out": lines = lines.Where(l => l.IsOutOfStock).ToList(); break;
                case "inactive": lines = lines.Where(l => !l.Ingredient.IsActive).ToList(); break;
                default: lines = lines.Where(l => l.Ingredient.IsActive).ToList(); filter = "all"; break;
            }

            ViewBag.Filter = filter;
            ViewBag.IsManager = IsManager();
            return View(lines);
        }

        // ============================================================
        // GET: CafeteriaInventory/Ingredient/5
        // ============================================================

        [HttpGet]
        public ActionResult Ingredient(int id)
        {
            var line = _inventory.GetStockLine(id);
            if (line == null) return HttpNotFound();

            ViewBag.Links = _db.IngredientSuppliers
                .Include(x => x.Supplier)
                .Where(x => x.IngredientId == id)
                .OrderByDescending(x => x.IsPreferred)
                .ThenBy(x => x.Supplier.Name)
                .ToList();
            ViewBag.Suppliers = _db.Suppliers.Where(s => s.SuppliesCafeteria && s.IsActive).OrderBy(s => s.Name).ToList();
            ViewBag.History = _inventory.History(id);
            ViewBag.Upcoming = _inventory.UpcomingRequirements(14).FirstOrDefault(u => u.Ingredient.Id == id);
            ViewBag.IsManager = IsManager();

            var userIds = ((System.Collections.Generic.List<IngredientStockTransaction>)ViewBag.History)
                .Where(t => t.CreatedByUserId.HasValue).Select(t => t.CreatedByUserId.Value).Distinct().ToList();
            ViewBag.UserNames = _db.Users.Where(u => userIds.Contains(u.UserId)).ToDictionary(u => u.UserId, u => u.Name);

            return View(line);
        }

        // ============================================================
        // POST: stock changes (manager)
        // Quantities typed in kg / L; converted with the ingredient's
        // own unit from the database.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult Adjust(int id, string source, string countedQuantity, string reason)
        {
            return Run(id, ingredient =>
            {
                StockSource src = source == "Farm" ? StockSource.Farm : StockSource.External;
                var qty = Quantity(countedQuantity, ingredient, "counted quantity");
                _inventory.Adjust(id, src, qty, reason, ResolveUserId());
                return string.Format("{0} {1} stock set to {2}.", ingredient.Name, src == StockSource.Farm ? "farm" : "bought-in", IngredientUnits.Format(qty, ingredient.Unit));
            });
        }

        // ============================================================
        // GET / POST: CafeteriaInventory/AdjustStock/5
        // Damaged, spoiled, lost, count correction… Add or remove an
        // amount (or, for a count, enter what was counted). Goes
        // through IngredientInventoryService.AdjustBy → Apply, so the
        // transaction records the reason, who and when.
        // ============================================================

        [HttpGet]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult AdjustStock(int id)
        {
            var line = _inventory.GetStockLine(id);
            if (line == null) return HttpNotFound();

            ViewBag.Recent = _inventory.History(id, 10)
                .Where(t => t.Type == StockTransactionType.Adjustment)
                .ToList();
            return View(line);
        }

        // mode = "change" (direction + quantity) | "count" (countedQuantity)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult AdjustStock(int id, string source, string mode, string direction,
            string quantity, string countedQuantity, string reason, string notes)
        {
            var ingredient = _db.Ingredients.Find(id);
            if (ingredient == null) return HttpNotFound();

            try
            {
                StockSource src = source == "Farm" ? StockSource.Farm : StockSource.External;

                StockAdjustmentReason why;
                if (!Enum.TryParse(reason, out why) || !Enum.IsDefined(typeof(StockAdjustmentReason), why))
                    throw new InventoryException("Choose a reason for the adjustment.");

                decimal before = src == StockSource.Farm ? ingredient.FarmAvailableQuantity : ingredient.ExternalAvailableQuantity;
                decimal change;

                if (mode == "count")
                {
                    var counted = Quantity(countedQuantity, ingredient, "counted quantity", allowZero: true);
                    change = Math.Round(counted, 2) - before;
                    if (change == 0m) throw new InventoryException("The counted quantity is the same as the recorded stock.");
                }
                else
                {
                    var amount = Quantity(quantity, ingredient, "quantity to " + (direction == "remove" ? "remove" : "add"));
                    change = direction == "remove" ? -amount : amount;
                }

                var tx = _inventory.AdjustBy(id, src, change, why, Clip(notes, 400), ResolveUserId());

                TempData["Success"] = string.Format(
                    "{0}: {1}{2} ({3}). {4} stock {5} → {6}.",
                    ingredient.Name,
                    change > 0m ? "+" : "−",
                    IngredientUnits.Format(Math.Abs(change), ingredient.Unit),
                    IngredientInventoryService.ReasonLabel(why),
                    src == StockSource.Farm ? "Farm" : "Bought-in",
                    IngredientUnits.Format(before, ingredient.Unit),
                    IngredientUnits.Format(tx.SourceQuantityAfter, ingredient.Unit));

                return RedirectToAction("Ingredient", new { id });
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("AdjustStock", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult FarmProduce(int id, string quantity, string notes)
        {
            return Run(id, ingredient =>
            {
                var qty = Quantity(quantity, ingredient, "quantity");
                _inventory.AddFarmProduce(id, qty, notes, ResolveUserId());
                return string.Format("{0} farm produce added to {1}.", IngredientUnits.Format(qty, ingredient.Unit), ingredient.Name);
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult Levels(int id, string reorderLevel, string targetLevel)
        {
            return Run(id, ingredient =>
            {
                var reorder = Quantity(reorderLevel, ingredient, "reorder level", allowZero: true);
                decimal? target = string.IsNullOrWhiteSpace(targetLevel) ? (decimal?)null : Quantity(targetLevel, ingredient, "target level", allowZero: true);
                _inventory.SetLevels(id, reorder, target);
                return "Stock levels saved for " + ingredient.Name + ".";
            });
        }

        // ============================================================
        // POST: ingredient ↔ supplier links (manager)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult LinkSupplier(int id, int supplierId, bool isPreferred = false, string supplierItemName = null, string unitCost = null, int leadTimeDays = 3)
        {
            return Run(id, ingredient =>
            {
                var supplier = _db.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId && s.SuppliesCafeteria && s.IsActive);
                if (supplier == null) throw new InventoryException("Choose an active cafeteria supplier.");
                if (leadTimeDays < 0 || leadTimeDays > 90) throw new InventoryException("Lead time must be between 0 and 90 days.");

                decimal? cost = string.IsNullOrWhiteSpace(unitCost) ? (decimal?)null : IngredientUnits.ParseQuantity(unitCost);
                if (!string.IsNullOrWhiteSpace(unitCost) && (!cost.HasValue || cost.Value < 0m))
                    throw new InventoryException("Enter the unit cost as a number.");

                var link = _db.IngredientSuppliers.FirstOrDefault(x => x.IngredientId == id && x.SupplierId == supplierId);
                if (link == null)
                {
                    link = new IngredientSupplier { IngredientId = id, SupplierId = supplierId };
                    _db.IngredientSuppliers.Add(link);
                }

                if (isPreferred)
                {
                    foreach (var other in _db.IngredientSuppliers.Where(x => x.IngredientId == id && x.SupplierId != supplierId))
                        other.IsPreferred = false;
                }

                link.IsPreferred = isPreferred;
                link.IsActive = true;
                link.SupplierItemName = string.IsNullOrWhiteSpace(supplierItemName) ? null : Clip(supplierItemName, 150);
                link.UnitCost = cost;
                link.LeadTimeDays = leadTimeDays;
                _db.SaveChanges();

                return supplier.Name + " can now supply " + ingredient.Name + ".";
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult UnlinkSupplier(int id, int linkId)
        {
            return Run(id, ingredient =>
            {
                var link = _db.IngredientSuppliers.FirstOrDefault(x => x.Id == linkId && x.IngredientId == id);
                if (link == null) throw new InventoryException("That supplier isn't linked to this ingredient.");
                _db.IngredientSuppliers.Remove(link);
                _db.SaveChanges();
                return "Supplier removed from " + ingredient.Name + ".";
            });
        }

        // ============================================================
        // Suppliers (the shared Supplier records, flagged for the cafeteria)
        // ============================================================

        [HttpGet]
        public ActionResult Suppliers()
        {
            var cafeteria = _db.Suppliers.Where(s => s.SuppliesCafeteria).OrderBy(s => s.Name).ToList();
            var ids = cafeteria.Select(s => s.SupplierId).ToList();

            ViewBag.IngredientCounts = _db.IngredientSuppliers
                .Where(x => ids.Contains(x.SupplierId))
                .GroupBy(x => x.SupplierId)
                .ToDictionary(g => g.Key, g => g.Count());
            ViewBag.OtherSuppliers = _db.Suppliers.Where(s => !s.SuppliesCafeteria && s.IsActive).OrderBy(s => s.Name).ToList();
            ViewBag.IsManager = IsManager();

            return View(cafeteria);
        }

        [HttpGet]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult SupplierEdit(int? id)
        {
            var supplier = id.HasValue
                ? _db.Suppliers.FirstOrDefault(s => s.SupplierId == id.Value && s.SuppliesCafeteria)
                : new Supplier { IsActive = true, SuppliesCafeteria = true };

            if (supplier == null) return HttpNotFound();
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult SupplierEdit(int? id, string name, string contactPerson, string email, string phone, string address, bool isActive = false)
        {
            var supplier = id.HasValue
                ? _db.Suppliers.FirstOrDefault(s => s.SupplierId == id.Value && s.SuppliesCafeteria)
                : new Supplier { SuppliesCafeteria = true };
            if (supplier == null) return HttpNotFound();

            supplier.Name = (name ?? "").Trim();
            supplier.ContactPerson = Clip(contactPerson, 100);
            supplier.Email = Clip(email, 200);
            supplier.Phone = Clip(phone, 20);
            supplier.Address = Clip(address, 500);
            supplier.IsActive = isActive || !id.HasValue;

            if (supplier.Name.Length == 0 || supplier.Name.Length > 200)
                ModelState.AddModelError("Name", "Enter the supplier's name (200 characters at most).");
            if (supplier.Email != null && !Regex.IsMatch(supplier.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                ModelState.AddModelError("Email", "Enter a valid email address.");
            if (_db.Suppliers.Any(s => s.Name == supplier.Name && s.SupplierId != supplier.SupplierId))
                ModelState.AddModelError("Name", "A supplier with this name already exists. Add the existing one to the cafeteria instead.");

            if (!ModelState.IsValid) return View(supplier);

            if (!id.HasValue) _db.Suppliers.Add(supplier);
            _db.SaveChanges();

            TempData["Success"] = "Supplier " + supplier.Name + " saved.";
            return RedirectToAction("Suppliers");
        }

        // A supplier already used by the Store also supplies the cafeteria
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult AddSupplier(int supplierId)
        {
            var supplier = _db.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId && s.IsActive);
            if (supplier == null) return HttpNotFound();

            supplier.SuppliesCafeteria = true;
            _db.SaveChanges();
            TempData["Success"] = supplier.Name + " now supplies the cafeteria.";
            return RedirectToAction("Suppliers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ManagerRoles)]
        public ActionResult RemoveSupplier(int supplierId)
        {
            var supplier = _db.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId && s.SuppliesCafeteria);
            if (supplier == null) return HttpNotFound();

            bool openOrders = _db.IngredientPurchaseOrders.Any(o => o.SupplierId == supplierId
                && (o.Status == IngredientOrderStatus.Draft || o.Status == IngredientOrderStatus.Pending
                    || o.Status == IngredientOrderStatus.Confirmed || o.Status == IngredientOrderStatus.PartiallyFulfilled));
            if (openOrders)
            {
                TempData["Error"] = supplier.Name + " has open purchase orders. Complete or cancel them first.";
                return RedirectToAction("Suppliers");
            }

            supplier.SuppliesCafeteria = false;
            foreach (var link in _db.IngredientSuppliers.Where(x => x.SupplierId == supplierId)) link.IsActive = false;
            _db.SaveChanges();

            TempData["Success"] = supplier.Name + " no longer supplies the cafeteria.";
            return RedirectToAction("Suppliers");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private ActionResult Run(int ingredientId, Func<Ingredient, string> action)
        {
            var ingredient = _db.Ingredients.Find(ingredientId);
            if (ingredient == null) return HttpNotFound();

            try
            {
                TempData["Success"] = action(ingredient);
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Ingredient", new { id = ingredientId });
        }

        // kg / L typed → ingredient's own unit
        private static decimal Quantity(string raw, Ingredient ingredient, string what, bool allowZero = false)
        {
            var value = IngredientUnits.ParseQuantity(raw);
            if (!value.HasValue || value.Value < 0m || (!allowZero && value.Value == 0m))
                throw new InventoryException("Enter the " + what + " in " + IngredientUnits.DisplayUnit(ingredient.Unit) + ".");
            return IngredientUnits.ToBase(value.Value, ingredient.Unit);
        }

        private bool IsManager()
        {
            return User.IsInRole("CafeteriaManager") || User.IsInRole("Admin");
        }

        private int ResolveUserId()
        {
            if (Session["UserId"] == null) return 0;
            int id;
            int.TryParse(Session["UserId"].ToString(), out id);
            return id;
        }

        private static string Clip(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
