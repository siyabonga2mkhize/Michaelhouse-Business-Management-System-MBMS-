using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class StoreController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private ICartService _cartService;

        public StoreController()
        {
            _cartService = new SessionCartService(new HttpContextWrapper(System.Web.HttpContext.Current));
        }

        public ActionResult Index(int? categoryId)
        {
            // Start with base query
            var query = db.Products
                .Include("Category")
                .Where(p => p.IsActive);

            // Apply category filter if specified
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            // Apply ordering
            var products = query
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Name)
                .ToList();

            var categories = db.Categories.OrderBy(c => c.Name).ToList();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategory = categoryId;
            ViewBag.CartItemCount = _cartService.GetCart().TotalItems;

            return View(products);
        }

        public ActionResult Details(int id)
        {
            var product = db.Products
                .Include("Category")
                .FirstOrDefault(p => p.Id == id && p.IsActive);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Index");
            }

            ViewBag.CartItemCount = _cartService.GetCart().TotalItems;
            return View(product);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}