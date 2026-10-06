using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // Cafeteria ingredient inventory
    //
    // The stock itself lives on the existing Ingredient:
    //   FarmAvailableQuantity      produce from the school farm
    //   ExternalAvailableQuantity  stock bought from suppliers
    // in the ingredient's own unit (g / ml), so recipes
    // (RecipeIngredient.QuantityPerStandardPortion) keep working.
    //
    // Stock only changes through IngredientInventoryService, which
    // writes an IngredientStockTransaction for every change:
    //   delivery received  → + external
    //   farm produce       → + farm
    //   kitchen issue      → − farm first, then external
    //   adjustment         → ± either, with a reason
    //
    // Suppliers are the shared Supplier records (Store + cafeteria),
    // flagged with Supplier.SuppliesCafeteria. Separate from the
    // Store's own PurchaseOrder / StockMovement tables.
    // ============================================================

    // Which supplier can supply which ingredient
    public class IngredientSupplier
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }
        public virtual Ingredient Ingredient { get; set; }

        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        // Tried first when ordering or re-ordering a shortfall
        public bool IsPreferred { get; set; }

        // What the supplier calls it on invoices, e.g. "Chkn Brst Fillet"
        [StringLength(150)]
        public string SupplierItemName { get; set; }

        // Price per kg / litre / unit (display unit) — information only
        public decimal? UnitCost { get; set; }

        public int LeadTimeDays { get; set; }

        public bool IsActive { get; set; }

        public IngredientSupplier()
        {
            IsActive = true;
            LeadTimeDays = 3;
        }
    }

    public enum IngredientOrderStatus
    {
        Draft = 0,               // being prepared
        Pending = 1,             // sent to the supplier
        Confirmed = 2,           // supplier confirmed what they can supply
        PartiallyFulfilled = 3,  // some received, some outstanding
        Fulfilled = 4,           // everything received (or the rest closed)
        Cancelled = 5
    }

    public class IngredientPurchaseOrder
    {
        [Key]
        public int Id { get; set; }

        // e.g. CAF-PO-2026-00001
        [Required]
        [StringLength(30)]
        [Index(IsUnique = true)]
        public string PoNumber { get; set; }

        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public IngredientOrderStatus Status { get; set; }

        public DateTime OrderDate { get; set; }
        public DateTime? RequestedDeliveryDate { get; set; }

        public DateTime? SentAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        public int CreatedByUserId { get; set; }

        // Set on an order created for another order's shortfall
        [ForeignKey("ShortfallOf")]
        public int? ShortfallOfOrderId { get; set; }
        public virtual IngredientPurchaseOrder ShortfallOf { get; set; }

        // ── Test supplier invoice (TestInvoiceService) ──
        // Generated when the supplier confirms the order and kept in
        // step with it, so the Record Delivery scan can be tested end
        // to end. Not an accounting record.
        [StringLength(40)]
        public string TestInvoiceNumber { get; set; }

        // Relative to DocumentStorage:UploadRoot (like delivery invoices)
        [StringLength(300)]
        public string TestInvoiceFilePath { get; set; }

        public DateTime? TestInvoiceGeneratedAt { get; set; }

        public virtual ICollection<IngredientPurchaseOrderLine> Lines { get; set; }

        public IngredientPurchaseOrder()
        {
            Status = IngredientOrderStatus.Draft;
            Lines = new HashSet<IngredientPurchaseOrderLine>();
        }
    }

    public enum ShortfallAction
    {
        None = 0,       // nothing decided yet (or no shortfall)
        Accepted = 1,   // manager accepted the smaller quantity
        Reordered = 2   // ordered from another supplier
    }

    // Quantities are in the ingredient's own unit (g / ml)
    public class IngredientPurchaseOrderLine
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PurchaseOrder")]
        public int PurchaseOrderId { get; set; }
        public virtual IngredientPurchaseOrder PurchaseOrder { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }
        public virtual Ingredient Ingredient { get; set; }

        public decimal QuantityOrdered { get; set; }

        // What the supplier said they can supply (null = not yet confirmed)
        public decimal? QuantityConfirmed { get; set; }

        public decimal QuantityReceived { get; set; }

        public decimal? UnitCost { get; set; }

        public ShortfallAction ShortfallAction { get; set; }

        // The order the shortfall was re-ordered on
        public int? ShortfallReorderedOnOrderId { get; set; }

        [StringLength(300)]
        public string ShortfallNote { get; set; }

        // What we still expect from this supplier
        [NotMapped]
        public decimal ExpectedQuantity { get { return QuantityConfirmed ?? QuantityOrdered; } }

        [NotMapped]
        public decimal Outstanding { get { return Math.Max(0m, ExpectedQuantity - QuantityReceived); } }

        // Ordered − confirmed
        [NotMapped]
        public decimal Shortfall { get { return QuantityConfirmed.HasValue ? Math.Max(0m, QuantityOrdered - QuantityConfirmed.Value) : 0m; } }
    }

    public class IngredientDelivery
    {
        [Key]
        public int Id { get; set; }

        // Null only for a delivery recorded without an order (confirmed by the manager)
        [ForeignKey("PurchaseOrder")]
        public int? PurchaseOrderId { get; set; }
        public virtual IngredientPurchaseOrder PurchaseOrder { get; set; }

        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public DateTime ReceivedAt { get; set; }
        public int ReceivedByUserId { get; set; }

        [StringLength(60)]
        public string InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }

        // Scanned / uploaded invoice, relative to DocumentStorage:UploadRoot
        [StringLength(300)]
        public string InvoiceFilePath { get; set; }

        [StringLength(200)]
        public string InvoiceFileName { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        // One per delivery form — stops the same delivery being saved twice
        [Required]
        [StringLength(40)]
        [Index(IsUnique = true)]
        public string SubmissionToken { get; set; }

        public virtual ICollection<IngredientDeliveryLine> Lines { get; set; }

        public IngredientDelivery()
        {
            Lines = new HashSet<IngredientDeliveryLine>();
        }
    }

    public class IngredientDeliveryLine
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Delivery")]
        public int DeliveryId { get; set; }
        public virtual IngredientDelivery Delivery { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }
        public virtual Ingredient Ingredient { get; set; }

        // The order line it was received against (null = not on the order)
        [ForeignKey("PurchaseOrderLine")]
        public int? PurchaseOrderLineId { get; set; }
        public virtual IngredientPurchaseOrderLine PurchaseOrderLine { get; set; }

        // In the ingredient's own unit
        public decimal QuantityReceived { get; set; }

        // Received beyond what was outstanding (accepted by the manager)
        public decimal ExtraQuantity { get; set; }

        [StringLength(300)]
        public string Note { get; set; }

        // As read from the invoice (for checking OCR later)
        [StringLength(200)]
        public string InvoiceDescription { get; set; }
        public decimal? InvoiceQuantity { get; set; }

        [StringLength(20)]
        public string InvoiceUnit { get; set; }
    }

    public enum StockTransactionType
    {
        Delivery = 1,
        FarmProduce = 2,
        KitchenIssue = 3,
        Adjustment = 4
    }

    public enum StockSource
    {
        Farm = 1,
        External = 2
    }

    // Why the Cafeteria Manager adjusted stock by hand
    public enum StockAdjustmentReason
    {
        Damaged = 1,
        Spoiled = 2,           // spoiled / expired
        Lost = 3,
        StockCountCorrection = 4,
        Other = 5
    }

    // The audit trail: why stock changed
    public class IngredientStockTransaction
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }
        public virtual Ingredient Ingredient { get; set; }

        public StockTransactionType Type { get; set; }
        public StockSource Source { get; set; }

        // Adjustments only: why (details in Notes)
        public StockAdjustmentReason? AdjustmentReason { get; set; }

        // + received, − used (ingredient's own unit)
        public decimal Quantity { get; set; }

        // Stock in that source, and in total, after the change
        public decimal SourceQuantityAfter { get; set; }
        public decimal TotalAfter { get; set; }

        // e.g. CAF-PO-2026-00012, KP-14-20261007, EV-3
        [StringLength(60)]
        public string Reference { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public int? PurchaseOrderId { get; set; }
        public int? DeliveryId { get; set; }
        public int? KitchenIssueId { get; set; }

        public int? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // One per kitchen day (weekly menu + date) or per event, so the
    // same food can't be issued from stock twice
    public class KitchenIngredientIssue
    {
        [Key]
        public int Id { get; set; }

        // MENU-{menuId}-{yyyyMMdd} or EVENT-{eventId}
        [Required]
        [StringLength(40)]
        [Index(IsUnique = true)]
        public string IssueKey { get; set; }

        public int? MealMenuId { get; set; }
        public DateTime? Date { get; set; }
        public int? CafeteriaEventId { get; set; }

        public int IssuedByUserId { get; set; }
        public DateTime IssuedAt { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }
}
