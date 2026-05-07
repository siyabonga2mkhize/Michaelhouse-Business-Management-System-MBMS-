
using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Michaelhouse.Services
{
    public class InventoryService
    {
        private readonly EmailService _email = new EmailService();

        // ─── Low Stock Detection ──────────────────────────────────────────────────

        /// <summary>
        /// Returns all products that are at or below their reorder level
        /// and don't already have an open (Draft/Sent) purchase order.
        /// </summary>
        public List<Product> GetLowStockProducts()
        {
            using (var db = new DBContextClass())
            {
                // Get product IDs that already have an open PO
                var openPoProductIds = db.PurchaseOrderLines
                    .Where(pol =>
                        pol.PurchaseOrder.Status == PurchaseOrderStatus.Draft ||
                        pol.PurchaseOrder.Status == PurchaseOrderStatus.Sent)
                    .Select(pol => pol.ProductId)
                    .Distinct()
                    .ToList();

                return db.Products
                    .Include("Category")
                    .Where(p => p.IsActive &&
                                p.QuantityInStock <= p.ReorderLevel &&
                                !openPoProductIds.Contains(p.Id))
                    .OrderBy(p => p.QuantityInStock)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns all products at or below reorder level (including ones with open POs).
        /// Used for the inventory dashboard.
        /// </summary>
        public List<Product> GetAllLowStockProducts()
        {
            using (var db = new DBContextClass())
            {
                return db.Products
                    .Include("Category")
                    .Where(p => p.IsActive && p.QuantityInStock <= p.ReorderLevel)
                    .OrderBy(p => p.QuantityInStock)
                    .ToList();
            }
        }

        // ─── Create Purchase Order ────────────────────────────────────────────────

        /// <summary>
        /// Creates a Draft PO for a supplier with the specified products and quantities.
        /// Admin reviews it, then calls ApprovePurchaseOrder() to send.
        /// </summary>
        public PurchaseOrder CreatePurchaseOrder(
            int supplierId,
            List<(int ProductId, int Quantity, decimal UnitCost)> lines,
            string notes = null)
        {
            using (var db = new DBContextClass())
            {
                var poNumber = GeneratePoNumber(db);

                var supplier = db.Suppliers.Find(supplierId);
                if (supplier == null)
                    throw new Exception("Supplier not found.");

                var po = new PurchaseOrder
                {
                    PoNumber = poNumber,
                    SupplierId = supplierId,
                    Status = PurchaseOrderStatus.Draft,
                    CreatedAt = DateTime.Now,
                    ExpectedDelivery = DateTime.Now.AddDays(
                        supplier.PaymentTermsDays > 0 ? supplier.PaymentTermsDays : 7),
                    Notes = notes
                };

                db.PurchaseOrders.Add(po);
                db.SaveChanges();

                foreach (var (productId, quantity, unitCost) in lines)
                {
                    db.PurchaseOrderLines.Add(new PurchaseOrderLine
                    {
                        PurchaseOrderId = po.PurchaseOrderId,
                        ProductId = productId,
                        QuantityOrdered = quantity,
                        UnitCost = unitCost
                    });
                }

                db.SaveChanges();
                return po;
            }
        }

        // ─── Auto-create PO from low stock ────────────────────────────────────────

        /// <summary>
        /// For each low-stock product with a preferred supplier,
        /// creates a Draft PO grouped by supplier.
        /// Returns the list of created POs.
        /// </summary>
        public List<PurchaseOrder> AutoCreateLowStockOrders()
        {
            var created = new List<PurchaseOrder>();
            var lowStockProducts = GetLowStockProducts();

            if (!lowStockProducts.Any()) return created;

            using (var db = new DBContextClass())
            {
                // Group low-stock products by their preferred supplier
                var supplierGroups = new Dictionary<int, List<(int ProductId, int Qty, decimal Cost)>>();

                foreach (var product in lowStockProducts)
                {
                    var preferredSupplier = db.SupplierProducts
                        .Include("Supplier")
                        .FirstOrDefault(sp =>
                            sp.ProductId == product.Id &&
                            sp.IsPreferred &&
                            sp.Supplier.IsActive);

                    if (preferredSupplier == null)
                    {
                        // Fallback: any active supplier for this product
                        preferredSupplier = db.SupplierProducts
                            .Include("Supplier")
                            .FirstOrDefault(sp =>
                                sp.ProductId == product.Id &&
                                sp.Supplier.IsActive);
                    }

                    if (preferredSupplier == null) continue; // No supplier linked

                    int reorderQty = Math.Max(
                        preferredSupplier.MinOrderQty,
                        product.ReorderLevel * 2 - product.QuantityInStock);

                    if (!supplierGroups.ContainsKey(preferredSupplier.SupplierId))
                        supplierGroups[preferredSupplier.SupplierId] = new List<(int, int, decimal)>();

                    supplierGroups[preferredSupplier.SupplierId].Add(
                        (product.Id, reorderQty, preferredSupplier.UnitCost));
                }

                foreach (var entry in supplierGroups)
                {
                    int supplierId = entry.Key;
                    var lines = entry.Value; // This will be your List<(int, int, decimal)>

                    var po = CreatePurchaseOrder(supplierId, lines, "Auto-generated from low stock alert");
                    created.Add(po);
                }
            }

            return created;
        }

        // ─── Approve & Send PO ────────────────────────────────────────────────────

        /// <summary>
        /// Marks PO as Sent and emails the supplier.
        /// Admin approval is auto (per requirements) so this runs immediately.
        /// </summary>
        public (bool Success, string Error) ApprovePurchaseOrder(int purchaseOrderId)
        {
            using (var db = new DBContextClass())
            {
                var po = db.PurchaseOrders
                    .Include("Supplier")
                    .Include("LineItems")
                    .Include("LineItems.Product")
                    .FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId);

                if (po == null)
                    return (false, "Purchase order not found.");

                if (po.Status != PurchaseOrderStatus.Draft)
                    return (false, $"PO is already {po.Status}.");

                po.Status = PurchaseOrderStatus.Sent;
                po.SentAt = DateTime.Now;
                db.SaveChanges();

                // Email supplier
                try
                {
                    SendPurchaseOrderEmail(po);
                    po.EmailSent = true;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PO email failed: {ex.Message}");
                }

                return (true, null);
            }
        }

        // ─── Receive Delivery (mark full order received) ──────────────────────────

        /// <summary>
        /// Marks the full PO as received and updates stock for all line items.
        /// Creates a StockMovement record for each product.
        /// </summary>
        public (bool Success, string Error) ReceivePurchaseOrder(
            int purchaseOrderId, string notes = null)
        {
            using (var db = new DBContextClass())
            {
                var po = db.PurchaseOrders
                    .Include("LineItems")
                    .Include("LineItems.Product")
                    .FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId);

                if (po == null)
                    return (false, "Purchase order not found.");

                if (po.Status != PurchaseOrderStatus.Sent)
                    return (false, "Only Sent orders can be received.");

                // Update stock for each line item
                foreach (var line in po.LineItems)
                {
                    var product = db.Products.Find(line.ProductId);
                    if (product == null) continue;

                    int newStock = product.QuantityInStock + line.QuantityOrdered;

                    // Record stock movement
                    db.StockMovements.Add(new StockMovement
                    {
                        ProductId = line.ProductId,
                        MovementType = StockMovementType.Purchase,
                        Quantity = line.QuantityOrdered,
                        StockAfter = newStock,
                        Reference = po.PoNumber,
                        Notes = $"Received from {po.Supplier?.Name}",
                        CreatedAt = DateTime.Now
                    });

                    product.QuantityInStock = newStock;
                    line.QuantityReceived = line.QuantityOrdered;
                }

                po.Status = PurchaseOrderStatus.Received;
                po.ReceivedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(notes))
                    po.Notes = (po.Notes ?? "") + "\n" + notes;

                db.SaveChanges();
                return (true, null);
            }
        }

        // ─── Cancel PO ────────────────────────────────────────────────────────────

        public (bool Success, string Error) CancelPurchaseOrder(int purchaseOrderId)
        {
            using (var db = new DBContextClass())
            {
                var po = db.PurchaseOrders.Find(purchaseOrderId);
                if (po == null) return (false, "PO not found.");
                if (po.Status == PurchaseOrderStatus.Received)
                    return (false, "Cannot cancel a received order.");

                po.Status = PurchaseOrderStatus.Cancelled;
                db.SaveChanges();
                return (true, null);
            }
        }

        // ─── Manual Stock Adjustment ──────────────────────────────────────────────

        public void AdjustStock(int productId, int newQuantity, string reason)
        {
            using (var db = new DBContextClass())
            {
                var product = db.Products.Find(productId);
                if (product == null) return;

                int diff = newQuantity - product.QuantityInStock;

                db.StockMovements.Add(new StockMovement
                {
                    ProductId = productId,
                    MovementType = StockMovementType.Adjustment,
                    Quantity = diff,
                    StockAfter = newQuantity,
                    Reference = "MANUAL-ADJ",
                    Notes = reason,
                    CreatedAt = DateTime.Now
                });

                product.QuantityInStock = newQuantity;
                db.SaveChanges();
            }
        }

        // ─── Stock Movement History ───────────────────────────────────────────────

        public List<StockMovement> GetStockHistory(int productId)
        {
            using (var db = new DBContextClass())
            {
                return db.StockMovements
                    .Include("Product")
                    .Where(sm => sm.ProductId == productId)
                    .OrderByDescending(sm => sm.CreatedAt)
                    .ToList();
            }
        }

        // ─── Email supplier with PO details ──────────────────────────────────────

        private void SendPurchaseOrderEmail(PurchaseOrder po)
        {
            if (string.IsNullOrEmpty(po.Supplier?.Email)) return;

            var lineRows = string.Join("", po.LineItems.Select(l =>
                $"<tr>" +
                $"<td style='padding:8px;border-bottom:1px solid #eee;'>{l.Product?.Name}</td>" +
                $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:center;'>{l.QuantityOrdered}</td>" +
                $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>ZAR {l.UnitCost:N2}</td>" +
                $"<td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>ZAR {l.TotalCost:N2}</td>" +
                $"</tr>"));

            var body = $@"
<html><body style='font-family:Segoe UI,sans-serif;color:#333;max-width:700px;margin:0 auto;'>
    <div style='background:#1a3c5e;padding:24px 32px;'>
        <h2 style='color:#fff;margin:0;'>Michaelhouse</h2>
        <p style='color:rgba(255,255,255,0.7);margin:4px 0 0;'>Purchase Order</p>
    </div>
    <div style='padding:32px;background:#fff;'>
        <h3 style='color:#1a3c5e;'>Purchase Order — {po.PoNumber}</h3>
        <p>Dear {po.Supplier.ContactPerson ?? po.Supplier.Name},</p>
        <p>Please supply the following items as per this purchase order.</p>
 
        <div style='background:#f8f9fa;padding:16px;border-radius:8px;margin:20px 0;'>
            <table style='width:100%;font-size:0.9rem;'>
                <tr><td style='color:#666;'>PO Number</td><td style='font-weight:600;'>{po.PoNumber}</td></tr>
                <tr><td style='color:#666;'>Date Issued</td><td>{po.SentAt:dd MMMM yyyy}</td></tr>
                <tr><td style='color:#666;'>Expected Delivery</td><td>{po.ExpectedDelivery:dd MMMM yyyy}</td></tr>
            </table>
        </div>
 
        <table style='width:100%;border-collapse:collapse;margin:20px 0;'>
            <thead>
                <tr style='background:#1a3c5e;color:#fff;'>
                    <th style='padding:10px;text-align:left;'>Product</th>
                    <th style='padding:10px;text-align:center;'>Qty</th>
                    <th style='padding:10px;text-align:right;'>Unit Cost</th>
                    <th style='padding:10px;text-align:right;'>Total</th>
                </tr>
            </thead>
            <tbody>{lineRows}</tbody>
            <tfoot>
                <tr style='font-weight:700;background:#f4f6f9;'>
                    <td colspan='3' style='padding:10px;text-align:right;'>TOTAL</td>
                    <td style='padding:10px;text-align:right;color:#1a3c5e;font-size:1.1rem;'>
                        ZAR {po.TotalAmount:N2}
                    </td>
                </tr>
            </tfoot>
        </table>
 
        {(!string.IsNullOrEmpty(po.Notes) ? $"<p><strong>Notes:</strong> {po.Notes}</p>" : "")}
 
        <p>Please deliver to: <strong>Michaelhouse, Private Bag X1, Balgowan, KwaZulu-Natal, 3275</strong></p>
        <p>For queries contact our procurement office.</p>
    </div>
    <div style='background:#f4f6f9;padding:16px 32px;text-align:center;color:#888;font-size:0.8rem;'>
        Michaelhouse — This is an official purchase order.
    </div>
</body></html>";

            _email.SendRaw(po.Supplier.Email,
                $"Purchase Order {po.PoNumber} — Michaelhouse", body);
        }

        private string GeneratePoNumber(DBContextClass db)
        {
            int count = db.PurchaseOrders.Count() + 1;
            return $"MHS-PO-{DateTime.Now.Year}-{count:D5}";
        }
    }
}
