using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Michaelhouse.Models;

namespace Michaelhouse.Services
{
    public interface ICartService
    {
        CartDto GetCart();
        void AddItem(Product product, int quantity);
        void UpdateItemQuantity(int productId, int quantity);
        void RemoveItem(int productId);
        void ClearCart();
    }
}