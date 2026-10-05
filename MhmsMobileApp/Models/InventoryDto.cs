using System;
using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    // ============================================================
    // Record Stock Delivery (Cafeteria Manager / Admin)
    // Matches the JSON from the web app's CafeteriaInventoryApiController:
    //   GET  /api/cafeteria-inventory/open-orders
    //   GET  /api/cafeteria-inventory/delivery-form?orderId=5
    //   POST /api/cafeteria-inventory/scan          (multipart: invoice, orderId)
    //   POST /api/cafeteria-inventory/deliveries
    // Quantities are in kg / L, as on the web form.
    // ============================================================

    public class OpenOrdersDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
        public List<OpenOrderDto> Orders { get; set; } = new List<OpenOrderDto>();
    }

    public class OpenOrderDto
    {
        public int Id { get; set; }
        public string PoNumber { get; set; } = "";
        public string Supplier { get; set; } = "";
        public string Status { get; set; } = "";
        public string? RequestedDelivery { get; set; }
        public List<OpenOrderLineDto> Lines { get; set; } = new List<OpenOrderLineDto>();
    }

    public class OpenOrderLineDto
    {
        public int LineId { get; set; }
        public int IngredientId { get; set; }
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal Expected { get; set; }
        public decimal Outstanding { get; set; }
    }

    // The review form (same fields as the web's DeliveryFormViewModel)
    public class DeliveryFormDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";

        public string Token { get; set; } = "";
        public int? PurchaseOrderId { get; set; }
        public string? PoNumber { get; set; }
        public int SupplierId { get; set; }
        public string? Supplier { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceDate { get; set; }        // yyyy-MM-dd
        public string? InvoiceFilePath { get; set; }
        public string? InvoiceFileName { get; set; }
        public string? DetectedSupplier { get; set; }
        public string? Notes { get; set; }
        public bool FromScan { get; set; }
        public bool ScanAvailable { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
        public List<IngredientOptionDto> IngredientOptions { get; set; } = new List<IngredientOptionDto>();
        public List<DeliveryFormLineDto> Lines { get; set; } = new List<DeliveryFormLineDto>();
    }

    public class IngredientOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
    }

    public class DeliveryFormLineDto
    {
        public int? PurchaseOrderLineId { get; set; }
        public int? IngredientId { get; set; }
        public string? Ingredient { get; set; }
        public string? Unit { get; set; }
        public decimal? Ordered { get; set; }
        public decimal? Outstanding { get; set; }
        public decimal? Quantity { get; set; }
        public bool Include { get; set; }
        public bool Matched { get; set; }
        public string? MatchMessage { get; set; }
        public string? UnitMessage { get; set; }
        public string? InvoiceDescription { get; set; }
        public decimal? InvoiceQuantity { get; set; }
        public string? InvoiceUnit { get; set; }

        // Entered on the phone
        public bool AcceptExtra { get; set; }
        public bool NotOnOrderConfirmed { get; set; }
        public string? Note { get; set; }

        public bool IsOnOrder => PurchaseOrderLineId.HasValue;
    }

    // POST /api/cafeteria-inventory/deliveries — same fields as the web's DeliveryInput
    public class DeliveryRequestDto
    {
        public string Token { get; set; } = "";
        public int? PurchaseOrderId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string? InvoiceFilePath { get; set; }
        public string? InvoiceFileName { get; set; }
        public string? Notes { get; set; }
        public List<DeliveryLineRequestDto> Lines { get; set; } = new List<DeliveryLineRequestDto>();
    }

    public class DeliveryLineRequestDto
    {
        public int? PurchaseOrderLineId { get; set; }
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public bool AcceptExtra { get; set; }
        public bool NotOnOrderConfirmed { get; set; }
        public string? Note { get; set; }
        public string? InvoiceDescription { get; set; }
        public decimal? InvoiceQuantity { get; set; }
        public string? InvoiceUnit { get; set; }
    }

    public class DeliveryResultDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public int DeliveryId { get; set; }
        public string? Message { get; set; }
    }
}
