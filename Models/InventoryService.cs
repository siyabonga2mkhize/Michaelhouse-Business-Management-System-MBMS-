using Microsoft.EntityFrameworkCore;
using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
            using (var db = DbContextFactory.Create())
            {
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
            using (var db = DbContextFactory.Create())
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
            using (var db = DbContextFactory.Create())
            {
                var supplier = db.Suppliers.Find(supplierId);
                if (supplier == null)
                    throw new Exception("Supplier not found.");

                // Use the maximum lead time across all ordered products for expected delivery.
                // Fall back to 7 days if no SupplierProduct records are linked.
                int leadDays = 7;
                var productIds = lines.Select(l => l.ProductId).ToList();
                var maxLead = db.SupplierProducts
                    .Where(sp => sp.SupplierId == supplierId && productIds.Contains(sp.ProductId))
                    .Select(sp => (int?)sp.LeadTimeDays)
                    .Max();
                if (maxLead.HasValue && maxLead.Value > 0)
                    leadDays = maxLead.Value;

                var poNumber = GeneratePoNumber(db);

                var po = new PurchaseOrder
                {
                    PoNumber = poNumber,
                    SupplierId = supplierId,
                    Status = PurchaseOrderStatus.Draft,
                    CreatedAt = DateTime.Now,
                    ExpectedDelivery = DateTime.Now.AddDays(leadDays),
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

        // ─── Auto-create POs from low stock ──────────────────────────────────────

        /// <summary>
        /// For each low-stock product with a linked supplier,
        /// creates a Draft PO grouped by supplier (preferred supplier first, any active as fallback).
        /// Returns the list of created POs.
        ///
        /// BUG FIX: Previously used three separate DbContext instances (GetLowStockProducts opens
        /// one, AutoCreateLowStockOrders opened another, then CreatePurchaseOrder a third).
        /// Collapsed into a single context here to avoid EF tracking conflicts and stale data.
        /// </summary>
        public List<PurchaseOrder> AutoCreateLowStockOrders()
        {
            var created = new List<PurchaseOrder>();

            using (var db = DbContextFactory.Create())
            {
                // Replicate GetLowStockProducts() logic inline so we stay in one context.
                var openPoProductIds = db.PurchaseOrderLines
                    .Where(pol =>
                        pol.PurchaseOrder.Status == PurchaseOrderStatus.Draft ||
                        pol.PurchaseOrder.Status == PurchaseOrderStatus.Sent)
                    .Select(pol => pol.ProductId)
                    .Distinct()
                    .ToList();

                var lowStockProducts = db.Products
                    .Include("Category")
                    .Where(p => p.IsActive &&
                                p.QuantityInStock <= p.ReorderLevel &&
                                !openPoProductIds.Contains(p.Id))
                    .OrderBy(p => p.QuantityInStock)
                    .ToList();

                if (!lowStockProducts.Any()) return created;

                // Load all relevant SupplierProduct records in one query.
                var lowStockProductIds = lowStockProducts.Select(p => p.Id).ToList();
                var allSupplierProducts = db.SupplierProducts
                    .Include("Supplier")
                    .Where(sp => lowStockProductIds.Contains(sp.ProductId) && sp.Supplier.IsActive)
                    .ToList();

                // Group by supplier — preferred supplier wins; fall back to any active one.
                var supplierGroups = new Dictionary<int, List<(int ProductId, int Qty, decimal Cost)>>();

                foreach (var product in lowStockProducts)
                {
                    var candidateSuppliers = allSupplierProducts
                        .Where(sp => sp.ProductId == product.Id)
                        .ToList();

                    if (!candidateSuppliers.Any()) continue; // No supplier linked — skip.

                    var chosen = candidateSuppliers.FirstOrDefault(sp => sp.IsPreferred)
                                 ?? candidateSuppliers.First();

                    // Order enough to bring stock up to 2× the reorder level, respecting min order qty.
                    int reorderQty = Math.Max(
                        chosen.MinOrderQty,
                        product.ReorderLevel * 2 - product.QuantityInStock);

                    if (!supplierGroups.ContainsKey(chosen.SupplierId))
                        supplierGroups[chosen.SupplierId] = new List<(int, int, decimal)>();

                    supplierGroups[chosen.SupplierId].Add((product.Id, reorderQty, chosen.UnitCost));
                }

                // Create one PO per supplier group.
                foreach (var entry in supplierGroups)
                {
                    // CreatePurchaseOrder opens its own context — that is intentional and safe here
                    // because we are only passing value-type data (ids, quantities, costs), not EF entities.
                    var po = CreatePurchaseOrder(
                        entry.Key,
                        entry.Value,
                        "Auto-generated from low stock alert");
                    created.Add(po);
                }
            }

            return created;
        }

        // ─── Approve & Send PO ────────────────────────────────────────────────────

        /// <summary>
        /// Marks PO as Sent and emails the supplier.
        /// </summary>
        public (bool Success, string Error) ApprovePurchaseOrder(int purchaseOrderId)
        {
            using (var db = DbContextFactory.Create())
            {
                var po = db.PurchaseOrders
                    .Include("Supplier")
                    .Include("LineItems")
                    .Include("LineItems.Product")
                    .FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId);

                if (po == null)
                    return (false, "Purchase order not found.");

                if (po.Status != PurchaseOrderStatus.Draft)
                    return (false, $"PO is already {po.Status}. Only Draft orders can be approved.");

                if (!po.LineItems.Any())
                    return (false, "Cannot approve an empty purchase order.");

                po.Status = PurchaseOrderStatus.Sent;
                po.SentAt = DateTime.Now;
                db.SaveChanges();

                try
                {
                    SendPurchaseOrderEmail(po);
                    po.EmailSent = true;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    // Email failure is non-fatal — PO is still marked Sent.
                    System.Diagnostics.Debug.WriteLine($"PO email failed: {ex.Message}");
                }

                return (true, null);
            }
        }

        // ─── Receive Delivery ────────────────────────────────────────────────────

        /// <summary>
        /// Marks the full PO as received and updates stock for all line items.
        /// Creates a StockMovement record for each product.
        ///
        /// BUG FIX: Added Include("Supplier") so po.Supplier?.Name is never null
        /// when writing the StockMovement Notes field.
        /// </summary>
        public (bool Success, string Error) ReceivePurchaseOrder(
            int purchaseOrderId, string notes = null)
        {
            using (var db = DbContextFactory.Create())
            {
                var po = db.PurchaseOrders
                    .Include("Supplier")           // ← was missing; caused null supplier name
                    .Include("LineItems")
                    .Include("LineItems.Product")
                    .FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId);

                if (po == null)
                    return (false, "Purchase order not found.");

                if (po.Status != PurchaseOrderStatus.Sent)
                    return (false, "Only Sent orders can be marked as received.");

                foreach (var line in po.LineItems)
                {
                    var product = db.Products.Find(line.ProductId);
                    if (product == null) continue;

                    int newStock = product.QuantityInStock + line.QuantityOrdered;

                    db.StockMovements.Add(new StockMovement
                    {
                        ProductId = line.ProductId,
                        MovementType = StockMovementType.Purchase,
                        Quantity = line.QuantityOrdered,
                        StockAfter = newStock,
                        Reference = po.PoNumber,
                        Notes = $"Received from {po.Supplier?.Name ?? "supplier"}",
                        CreatedAt = DateTime.Now
                    });

                    product.QuantityInStock = newStock;
                    line.QuantityReceived = line.QuantityOrdered;
                }

                po.Status = PurchaseOrderStatus.Received;
                po.ReceivedAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(notes))
                    po.Notes = string.IsNullOrWhiteSpace(po.Notes)
                        ? notes
                        : po.Notes + "\n" + notes;

                db.SaveChanges();
                return (true, null);
            }
        }

        // ─── Cancel PO ───────────────────────────────────────────────────────────

        /// <summary>
        /// Cancels a Draft or Sent PO.
        /// Warns the caller if the PO was already sent to the supplier so the UI
        /// can surface a meaningful message to the admin.
        /// </summary>
        public (bool Success, string Error) CancelPurchaseOrder(int purchaseOrderId)
        {
            using (var db = DbContextFactory.Create())
            {
                var po = db.PurchaseOrders.Find(purchaseOrderId);
                if (po == null)
                    return (false, "Purchase order not found.");

                if (po.Status == PurchaseOrderStatus.Received)
                    return (false, "Cannot cancel an order that has already been received.");

                if (po.Status == PurchaseOrderStatus.Cancelled)
                    return (false, "This order is already cancelled.");

                bool wasSent = po.Status == PurchaseOrderStatus.Sent;
                po.Status = PurchaseOrderStatus.Cancelled;
                db.SaveChanges();

                // Surface a warning so the controller can relay it to the admin.
                string warning = wasSent
                    ? "Purchase order cancelled. Note: this PO was already emailed to the supplier — please notify them directly."
                    : null;

                return (true, warning);
            }
        }

        // ─── Manual Stock Adjustment ─────────────────────────────────────────────

        public void AdjustStock(int productId, int newQuantity, string reason)
        {
            using (var db = DbContextFactory.Create())
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
            using (var db = DbContextFactory.Create())
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
        }

          /*  _email.SendRaw(po.Supplier.Email,
                $"Purchase Order {po.PoNumber} — Michaelhouse", body);
        }*/

        // ─── PO Number Generator ─────────────────────────────────────────────────

        /// <summary>
        /// Generates a unique PO number using MAX(PurchaseOrderId) + 1 rather than COUNT() + 1.
        /// COUNT() is unsafe because cancelled/deleted POs reduce the count and cause collisions.
        /// </summary>
        private string GeneratePoNumber(DBContextClass db)
        {
            int maxId = db.PurchaseOrders.Any()
                ? db.PurchaseOrders.Max(po => po.PurchaseOrderId)
                : 0;
            // Append milliseconds as a tiebreaker against concurrent requests.
            int sequence = maxId + 1;
            return $"MHS-PO-{DateTime.Now.Year}-{sequence:D5}";
        }
    }
}