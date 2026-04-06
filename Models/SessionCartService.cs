using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Michaelhouse.Models;

namespace Michaelhouse.Services
{
    public class SessionCartService : ICartService
    {
        private const string CartKey = "SchoolStore_Cart";
        private readonly HttpContextBase _httpContext;

        public SessionCartService(HttpContextBase httpContext)
        {
            _httpContext = httpContext;
        }

        private HttpSessionStateBase Session => _httpContext.Session;

        public CartDto GetCart()
        {
            var cartJson = Session[CartKey] as string;
            return string.IsNullOrEmpty(cartJson)
                ? new CartDto()
                : JsonConvert.DeserializeObject<CartDto>(cartJson) ?? new CartDto();
        }

        public void AddItem(Product product, int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

            var cart = GetCart();
            var existing = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);

            if (existing != null)
            {
                var newQty = existing.Quantity + quantity;
                if (newQty > product.QuantityInStock)
                    throw new InvalidOperationException(
                        $"Cannot add {quantity} more units. Only {product.QuantityInStock - existing.Quantity} units left.");
                existing.Quantity = newQty;
            }
            else
            {
                if (quantity > product.QuantityInStock)
                    throw new InvalidOperationException(
                        $"Only {product.QuantityInStock} units of '{product.Name}' are in stock.");

                cart.Items.Add(new CartItemDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ImageUrl = product.ImageUrl,
                    UnitPrice = (double)product.Price,
                    Quantity = quantity,
                    MaxStock = product.QuantityInStock,
                });
            }

            SaveCart(cart);
        }

        public void UpdateItemQuantity(int productId, int quantity)
        {
            var cart = GetCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) return;

            if (quantity <= 0)
            {
                cart.Items.Remove(item);
            }
            else
            {
                if (quantity > item.MaxStock)
                    throw new InvalidOperationException($"Only {item.MaxStock} units are available.");
                item.Quantity = quantity;
            }

            SaveCart(cart);
        }

        public void RemoveItem(int productId)
        {
            var cart = GetCart();
            cart.Items.RemoveAll(i => i.ProductId == productId);
            SaveCart(cart);
        }

        public void ClearCart()
        {
            Session.Remove(CartKey);
        }

        private void SaveCart(CartDto cart)
        {
            Session[CartKey] = JsonConvert.SerializeObject(cart);
        }
    }
}