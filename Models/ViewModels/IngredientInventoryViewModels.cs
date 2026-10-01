using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    // ============================================================
    // Cafeteria inventory screens. Quantities typed by people are in
    // the display unit (kg / L); the server converts them to the
    // ingredient's own unit (g / ml) using the ingredient it loads
    // itself — never a unit sent by the browser.
    // ============================================================

    // ── Record Stock Delivery ────────────────────────────────────

    public class DeliveryFormViewModel
    {
        public DeliveryFormViewModel()
        {
            Lines = new List<DeliveryFormLine>();
            IngredientOptions = new List<IngredientOption>();
            OpenOrders = new List<IngredientPurchaseOrder>();
            Messages = new List<string>();
        }

        // One-time token for this form (duplicate-submission guard)
        public string Token { get; set; }

        public int? PurchaseOrderId { get; set; }
        public string PoNumber { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }

        // Recording a delivery that has no purchase order
        public bool NoOrder { get; set; }

        public string InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string InvoiceFilePath { get; set; }
        public string InvoiceFileName { get; set; }
        public string Notes { get; set; }

        // Invoice scan details shown for checking
        public bool FromScan { get; set; }
        public bool ScanAvailable { get; set; }
        public string DetectedSupplier { get; set; }
        public string DetectedPoReference { get; set; }
        public List<string> Messages { get; set; }

        public List<DeliveryFormLine> Lines { get; set; }
        public List<IngredientOption> IngredientOptions { get; set; }
        public List<IngredientPurchaseOrder> OpenOrders { get; set; }
    }

    public class DeliveryFormLine
    {
        public int? PurchaseOrderLineId { get; set; }
        public int? IngredientId { get; set; }
        public string IngredientName { get; set; }
        public string Unit { get; set; }            // ingredient's own unit
        public string DisplayUnit { get; set; }     // kg / L

        // From the order (display unit)
        public decimal? Ordered { get; set; }
        public decimal? Outstanding { get; set; }

        // What the manager confirms (display unit)
        public decimal? Quantity { get; set; }
        public bool Include { get; set; }
        public bool AcceptExtra { get; set; }
        public bool NotOnOrderConfirmed { get; set; }
        public string Note { get; set; }

        // From the invoice
        public string InvoiceDescription { get; set; }
        public decimal? InvoiceQuantity { get; set; }
        public string InvoiceUnit { get; set; }
        public bool Matched { get; set; }
        public string MatchMessage { get; set; }
        public string UnitMessage { get; set; }
    }

    public class IngredientOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayUnit { get; set; }
    }

    // ── Kitchen ingredient issue (per day / per event) ───────────

    public class KitchenIssueViewModel
    {
        public KitchenIssueViewModel()
        {
            Lines = new List<KitchenIssueLine>();
            Issued = new List<IngredientStockTransaction>();
        }

        public string Title { get; set; }
        public string IssueKey { get; set; }
        public int? MealMenuId { get; set; }
        public DateTime? Date { get; set; }
        public int? CafeteriaEventId { get; set; }

        public bool CanIssue { get; set; }
        public string CannotIssueReason { get; set; }

        public KitchenIngredientIssue Issue { get; set; }
        public string IssuedByName { get; set; }
        public List<IngredientStockTransaction> Issued { get; set; }

        public List<KitchenIssueLine> Lines { get; set; }
        public bool HasShortages { get { return Lines.Exists(l => l.Shortfall > 0m); } }
    }

    public class KitchenIssueLine
    {
        public KitchenIssueLine()
        {
            UsedFor = new List<string>();
        }

        public int IngredientId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal Required { get; set; }
        public decimal Farm { get; set; }
        public decimal External { get; set; }
        public decimal Available { get { return Farm + External; } }
        public decimal Shortfall { get { return Math.Max(0m, Required - Available); } }

        // Pre-filled: what's needed, or all there is
        public decimal SuggestedIssue { get { return Math.Min(Required, Available); } }

        public List<string> UsedFor { get; set; }
    }
}
