using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Order
    {
        public int OrderId { get; set; }

        [Display(Name = "Order Number")]
        public string OrderNumber { get; set; }

        [Display(Name = "Order Date")]
        [DataType(DataType.Date)]
        public DateTime OrderDate { get; set; }

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Pending, Processing, Ready, Collected, Cancelled

        [Display(Name = "Ordered By")]
        public string OrderedBy { get; set; }

        public int? StudentId { get; set; }
        public virtual Student Student { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }

    public class OrderItem
    {
        public int OrderItemId { get; set; }

        public int Quantity { get; set; }

        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [DataType(DataType.Currency)]
        public decimal LineTotal => Quantity * UnitPrice;

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        public int InventoryItemId { get; set; }
        public virtual InventoryItem InventoryItem { get; set; }
    }
}