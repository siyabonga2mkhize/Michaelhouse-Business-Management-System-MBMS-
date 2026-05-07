
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;

namespace Michaelhouse.Models
{
    // ─── Supplier ─────────────────────────────────────────────────────────────────

    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Company Name")]
        public string Name { get; set; }

        [MaxLength(100)]
        [Display(Name = "Contact Person")]
        public string ContactPerson { get; set; }

        [MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [MaxLength(20)]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [MaxLength(500)]
        [Display(Name = "Physical Address")]
        public string Address { get; set; }

        [MaxLength(200)]
        [Display(Name = "Website")]
        public string Website { get; set; }

        // Payment terms in days (e.g. 30 = net 30)
        [Display(Name = "Payment Terms (days)")]
        public int PaymentTermsDays { get; set; } = 30;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual ICollection<SupplierProduct> SupplierProducts { get; set; }
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; }
    }

    // ─── SupplierProduct — links a supplier to a product with their price ─────────

    public class SupplierProduct
    {
        [Key]
        public int SupplierProductId { get; set; }

        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        // Supplier's unit cost for this product
        [Required]
        [Display(Name = "Unit Cost (ZAR)")]
        public decimal UnitCost { get; set; }

        // Supplier's own product code / SKU
        [MaxLength(100)]
        [Display(Name = "Supplier SKU")]
        public string SupplierSku { get; set; }

        // Is this the preferred supplier for this product?
        public bool IsPreferred { get; set; } = false;

        // Minimum order quantity this supplier accepts
        [Display(Name = "Min Order Quantity")]
        public int MinOrderQty { get; set; } = 1;

        // Lead time in days
        [Display(Name = "Lead Time (days)")]
        public int LeadTimeDays { get; set; } = 7;
    }

    // ─── PurchaseOrder Status ─────────────────────────────────────────────────────

    public enum PurchaseOrderStatus
    {
        Draft = 0,  // Being built
        Sent = 1,  // Sent to supplier (auto-approved, email sent)
        Received = 2,  // Delivery received, stock updated
        Cancelled = 3
    }

    // ─── PurchaseOrder ────────────────────────────────────────────────────────────

    public class PurchaseOrder
    {
        [Key]
        public int PurchaseOrderId { get; set; }

        [Required, MaxLength(50)]
        [Display(Name = "PO Number")]
        public string PoNumber { get; set; } // e.g. MHS-PO-2026-00001

        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // When the PO was sent to the supplier
        public DateTime? SentAt { get; set; }

        // When the delivery was received
        public DateTime? ReceivedAt { get; set; }

        // Expected delivery date
        public DateTime? ExpectedDelivery { get; set; }

        // Admin notes
        [MaxLength(1000)]
        public string Notes { get; set; }

        // Was supplier emailed?
        public bool EmailSent { get; set; } = false;

        [NotMapped]
        public decimal TotalAmount => LineItems?.Sum(li => li.TotalCost) ?? 0;

        // Navigation
        public virtual ICollection<PurchaseOrderLine> LineItems { get; set; }
    }

    // ─── PurchaseOrderLine ────────────────────────────────────────────────────────

    public class PurchaseOrderLine
    {
        [Key]
        public int PurchaseOrderLineId { get; set; }

        [ForeignKey("PurchaseOrder")]
        public int PurchaseOrderId { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }

        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        [Display(Name = "Quantity Ordered")]
        public int QuantityOrdered { get; set; }

        [Required]
        [Display(Name = "Unit Cost (ZAR)")]
        public decimal UnitCost { get; set; }

        [NotMapped]
        public decimal TotalCost => QuantityOrdered * UnitCost;

        // Quantity actually received (set when delivery arrives)
        public int QuantityReceived { get; set; } = 0;
    }

    // ─── StockMovement — audit trail for all stock changes ───────────────────────

    public enum StockMovementType
    {
        Purchase = 1,  // Stock received from supplier
        Sale = 2,  // Sold to customer
        Adjustment = 3, // Manual admin correction
        Return = 4   // Item returned to stock
    }

    public class StockMovement
    {
        [Key]
        public int StockMovementId { get; set; }

        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public StockMovementType MovementType { get; set; }

        // Positive = stock in, Negative = stock out
        public int Quantity { get; set; }

        // Stock level after this movement
        public int StockAfter { get; set; }

        [MaxLength(500)]
        public string Reference { get; set; } // PO number, order number, etc.

        [MaxLength(500)]
        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}