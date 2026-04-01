using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class InventoryItem
	{
        public int InventoryItemId { get; set; }

        [Required, Display(Name = "Item Name")]
        [StringLength(100)]
        public string ItemName { get; set; }

        [Display(Name = "Category")]
        public string Category { get; set; } // Uniform, Books, Stationery, Food

        [Display(Name = "Quantity In Stock")]
        public int QuantityInStock { get; set; }

        [Display(Name = "Reorder Level")]
        public int ReorderLevel { get; set; }

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Supplier")]
        public string Supplier { get; set; }

        [Display(Name = "SKU")]
        public string SKU { get; set; }

        [Display(Name = "Low Stock")]
        public bool IsLowStock => QuantityInStock <= ReorderLevel;
    }
}