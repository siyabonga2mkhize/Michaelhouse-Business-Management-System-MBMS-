using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    public class Recipe
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [StringLength(2000)] public string Description { get; set; }
        [StringLength(4000)] public string PreparationInstructions { get; set; }
        public int ServingYield { get; set; } = 1;
        [StringLength(40)] public string YieldUnit { get; set; }
        [StringLength(500)] public string AllergenNotes { get; set; }
        public decimal? CostPerServing { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    }

    public class RecipeIngredient
    {
        [Key] public int Id { get; set; }
        public int RecipeId { get; set; }
        [ForeignKey("RecipeId")] public virtual Recipe Recipe { get; set; }
        public int InventoryItemId { get; set; }
        [ForeignKey("InventoryItemId")] public virtual InventoryItem InventoryItem { get; set; }
        public decimal Quantity { get; set; }
        [StringLength(20)] public string Unit { get; set; }
        public bool IsOptional { get; set; }
        [StringLength(300)] public string Notes { get; set; }
    }

    public class InventoryItem
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(120)] public string Name { get; set; }
        [StringLength(80)] public string Category { get; set; }
        [StringLength(20)] public string Unit { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal ReservedStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal? UnitCost { get; set; }
        public DateTime? ExpiryDate { get; set; }
        [StringLength(120)] public string StorageLocation { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public decimal AvailableStock { get { return CurrentStock - ReservedStock; } }
    }

    public class StockOrder
    {
        [Key] public int Id { get; set; }
        [StringLength(50)] public string OrderNumber { get; set; }
        public int RequestedByUserId { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public StockOrderStatus Status { get; set; } = StockOrderStatus.Requested;
        public StockOrderPriority Priority { get; set; } = StockOrderPriority.Normal;
        public DateTime? RequiredByDate { get; set; }
        public int? RelatedWeeklyMenuId { get; set; }
        [StringLength(1000)] public string Notes { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public virtual ICollection<StockOrderLine> Lines { get; set; } = new List<StockOrderLine>();
    }

    public class StockOrderLine
    {
        [Key] public int Id { get; set; }
        public int StockOrderId { get; set; }
        [ForeignKey("StockOrderId")] public virtual StockOrder StockOrder { get; set; }
        public int InventoryItemId { get; set; }
        [ForeignKey("InventoryItemId")] public virtual InventoryItem InventoryItem { get; set; }
        public decimal Quantity { get; set; }
        [StringLength(20)] public string Unit { get; set; }
        public decimal ReceivedQuantity { get; set; }
        [StringLength(300)] public string Notes { get; set; }
    }
}