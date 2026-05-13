using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class CartController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private ICartService _cartService;
        

        public CartController()
        {
            _cartService = new SessionCartService(new HttpContextWrapper(System.Web.HttpContext.Current));
        }

        // ─── View Cart ────────────────────────────────────────────────────────────

        public ActionResult Index()
        {
            var cart = _cartService.GetCart();
            ViewBag.CartItemCount = cart.TotalItems;
            return View(cart);
        }

        // ─── Add Item to Cart ─────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(int productId, int quantity = 1)
        {
            if (quantity <= 0)
            {
                TempData["Error"] = "Quantity must be at least 1.";
                return RedirectToAction("Index", "Store");
            }

            var product = db.Products.Find(productId);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Index", "Store");
            }

            if (!product.IsInStock)
            {
                TempData["Error"] = $"'{product.Name}' is currently out of stock.";
                return RedirectToAction("Index", "Store");
            }

            try
            {
                _cartService.AddItem(product, quantity);
                TempData["Success"] = $"'{product.Name}' added to cart.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", "Store");
        }

        // ─── Update Cart Item Quantity ────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(int productId, int quantity)
        {
            try
            {
                _cartService.UpdateItemQuantity(productId, quantity);
                TempData["Success"] = "Cart updated.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ─── Remove Item from Cart ────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Remove(int productId)
        {
            _cartService.RemoveItem(productId);
            TempData["Success"] = "Item removed from cart.";
            return RedirectToAction("Index");
        }

        // ─── Clear Entire Cart ────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Clear()
        {
            _cartService.ClearCart();
            TempData["Success"] = "Cart cleared.";
            return RedirectToAction("Index");
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