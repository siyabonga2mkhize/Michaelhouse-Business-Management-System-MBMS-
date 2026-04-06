using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class OrderController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private ICartService _cartService;

        public OrderController()
        {
            _cartService = new SessionCartService(new HttpContextWrapper(System.Web.HttpContext.Current));
        }

        // ─── Checkout Page ────────────────────────────────────────────────────────

        public ActionResult Checkout()
        {
            var cart = _cartService.GetCart();
            if (cart.IsEmpty)
            {
                TempData["Error"] = "Your cart is empty. Please add items before checking out.";
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.Cart = cart;
            ViewBag.CartItemCount = cart.TotalItems;
            return View(new CheckoutViewModel());
        }

        // ─── Place Order ──────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceOrder(CheckoutViewModel model)
        {
            var cart = _cartService.GetCart();
            if (cart.IsEmpty)
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Cart = cart;
                ViewBag.CartItemCount = cart.TotalItems;
                return View("Checkout", model);
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Validate stock before processing
                    var productIds = cart.Items.Select(i => i.ProductId).ToList();
                    var dbProducts = db.Products.Where(p => productIds.Contains(p.Id)).ToList();

                    foreach (var item in cart.Items)
                    {
                        var product = dbProducts.FirstOrDefault(p => p.Id == item.ProductId);
                        if (product == null)
                        {
                            throw new InvalidOperationException($"Product '{item.ProductName}' no longer exists.");
                        }

                        if (!product.CanFulfil(item.Quantity))
                        {
                            throw new InvalidOperationException(
                                $"Insufficient stock for '{product.Name}'. Requested: {item.Quantity}, Available: {product.QuantityInStock}.");
                        }
                    }

                    // Create the order
                    var order = new Order
                    {
                        OrderNumber = GenerateOrderNumber(),
                        CustomerName = model.CustomerName.Trim(),
                        CustomerEmail = model.CustomerEmail.Trim().ToLowerInvariant(),
                        OrderDate = DateTime.UtcNow,
                        TotalAmount = (double)cart.TotalAmount,
                        Status = "Pending",
                        Notes = model.Notes?.Trim(),
                        OrderItems = cart.Items.Select(item => new OrderItem
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                        }).ToList()
                    };

                    db.Orders.Add(order);

                    // Deduct stock
                    foreach (var item in cart.Items)
                    {
                        var product = dbProducts.First(p => p.Id == item.ProductId);
                        product.QuantityInStock -= item.Quantity;
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    // Clear the cart after successful order
                    _cartService.ClearCart();

                    TempData["Success"] = "Order placed successfully!";
                    return RedirectToAction("Success", new { orderNumber = order.OrderNumber });
                }
                catch (InvalidOperationException ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", ex.Message);
                    ViewBag.Cart = cart;
                    ViewBag.CartItemCount = cart.TotalItems;
                    return View("Checkout", model);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "An error occurred while processing your order. Please try again.");
                    ViewBag.Cart = cart;
                    ViewBag.CartItemCount = cart.TotalItems;
                    return View("Checkout", model);
                }
            }
        }

        // ─── Order Success Page ───────────────────────────────────────────────────

        public ActionResult Success(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                return RedirectToAction("Index", "Store");
            }

            var order = db.Orders
                .Include("OrderItems")
                .Include("OrderItems.Product")
                .FirstOrDefault(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("Index", "Store");
            }

            ViewBag.CartItemCount = 0;
            return View(order);
        }

        // ─── Helper Methods ───────────────────────────────────────────────────────

        private string GenerateOrderNumber()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"SS-{timestamp}-{random}";
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