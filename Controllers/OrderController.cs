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
        private readonly EmailService _emailService; //Added for future use in sending order confirmation emails (Second Increment)

        public OrderController()
        {
            _cartService = new SessionCartService(new HttpContextWrapper(System.Web.HttpContext.Current));
            _emailService = new EmailService(); // Initialize email service (Second Increment)
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

            // Prepare view model and auto‑fill from session (Increment 2)
            var model = new CheckoutViewModel();
            string userId = Session["UserId"]?.ToString();
            string userRole = Session["UserRole"]?.ToString();

            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out int uid))
            {
                if (userRole == "Parent")
                {
                    var parent = db.Parents.Find(uid);
                    if (parent != null)
                    {
                        model.CustomerName = parent.Name;
                        model.CustomerEmail = parent.User?.Email ?? "";
                    }
                }
                else if (userRole == "Student")
                {
                    var student = db.Students.Find(uid);
                    if (student != null)
                    {
                        model.CustomerName = student.Name;
                        model.CustomerEmail = student.User?.Email ?? "";
                    }
                }
                else if (userRole == "Teacher")
                {
                    var teacher = db.Users.Find(uid);
                    if (teacher != null)
                    {
                        model.CustomerName = teacher.Name;
                        model.CustomerEmail = teacher.Email;
                    }
                }
                else // Admin or other
                {
                    var user = db.Users.Find(uid);
                    if (user != null)
                    {
                        model.CustomerName = user.Name;
                        model.CustomerEmail = user.Email;
                    }
                }
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

            // Ensure name/email were filled (should have been from session) (Increment 2)
            if (string.IsNullOrWhiteSpace(model.CustomerName) || string.IsNullOrWhiteSpace(model.CustomerEmail))
            {
                ModelState.AddModelError("", "Customer information is missing. Please log out and log in again.");
                ViewBag.Cart = cart;
                ViewBag.CartItemCount = cart.TotalItems;
                return View("Checkout", model);
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

                    // Send order confirmation email (non‑blocking, log errors) (Increment 2)
                    try
                    {
                        _emailService.SendOrderConfirmation(
                            order.CustomerEmail,
                            order.CustomerName,
                            order.OrderNumber,
                            order.TotalAmount,
                            order.OrderItems.ToList()
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Order confirmation email failed: {ex.Message}");
                    }

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
                catch (Exception)
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

        //===================== Tracking and Cancelling Orders (Second Increment) =====================

        //Track Order Page (Get)
        public ActionResult TrackOrder()
        {
            return View(new TrackOrderViewModel());
        }

        // Track Order Page (Post)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TrackOrder(TrackOrderViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var order = db.Orders
                .Include("OrderItems")
                .Include("OrderItems.Product")
                .FirstOrDefault(o => o.OrderNumber == model.OrderNumber);

            if (order == null)
            {
                ModelState.AddModelError("", "Order not found. Please check your order number.");
                return View(model);
            }

            // Map to OrderDetailsViewModel
            var detailsVm = new OrderDetailsViewModel
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                StatusClass = GetStatusClass(order.Status),
                Notes = order.Notes,
                CanCancel = (order.Status == "Pending"),
                Items = order.OrderItems.Select(item => new OrderItemViewModel
                {
                    ProductName = item.Product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                }).ToList()
            };

            return View("OrderDetails", detailsVm);
        }

        // ─── Order Details (GET) - Allows direct access via URL ───────────────────
        public ActionResult OrderDetails(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
                return RedirectToAction("TrackOrder");

            var order = db.Orders
                .Include("OrderItems")
                .Include("OrderItems.Product")
                .FirstOrDefault(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("TrackOrder");
            }

            var detailsVm = new OrderDetailsViewModel
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                StatusClass = GetStatusClass(order.Status),
                Notes = order.Notes,
                CanCancel = (order.Status == "Pending"),
                Items = order.OrderItems.Select(item => new OrderItemViewModel
                {
                    ProductName = item.Product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                }).ToList()
            };

            return View(detailsVm);
        }

        // Cancel Order (Post)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOrder(int id)
        {
            var order = db.Orders.Find(id);
            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction("TrackOrder");
            }

            if (order.Status != "Pending")
            {
                TempData["Error"] = "This order cannot be cancelled because it is already " + order.Status.ToLower() + ".";
                return RedirectToAction("TrackOrder");
            }

            // Restore stock for each item
            foreach (var item in order.OrderItems)
            {
                var product = db.Products.Find(item.ProductId);
                if (product != null)
                    product.QuantityInStock += item.Quantity;
            }

            order.Status = "Cancelled";
            db.SaveChanges();

            TempData["Success"] = $"Order #{order.OrderNumber} has been cancelled successfully.";
            return RedirectToAction("TrackOrder");
        }



        // ─── Helper Methods ───────────────────────────────────────────────────────

        private string GenerateOrderNumber()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"SS-{timestamp}-{random}";
        }

        // This method can be used in the view to apply CSS classes based on order status (Second Increment)
        private string GetStatusClass(string status)
        {
            switch (status)
            {
                case "Pending": return "bg-warning text-dark";
                case "Cancelled": return "bg-danger";
                case "Completed": return "bg-success";
                default: return "bg-secondary";
            }
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
