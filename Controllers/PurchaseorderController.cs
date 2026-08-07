using Microsoft.EntityFrameworkCore;
using Michaelhouse.Infrastructure;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    [InventoryManagerOnly]
    public class PurchaseOrderController : BaseController
    {
        private readonly DBContextClass db = DbContextFactory.Create();
        private readonly InventoryService _inventoryService = new InventoryService();

        // ─── List all POs ─────────────────────────────────────────────────────────

        public ActionResult Index()
        {
            var orders = db.PurchaseOrders
                .Include("Supplier")
                .Include("LineItems")
                .OrderByDescending(po => po.CreatedAt)
                .ToList();

            return View(orders);
        }

        // ─── Create PO manually ───────────────────────────────────────────────────

        public ActionResult Create(int? supplierId)
        {
            var suppliers = db.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Suppliers = suppliers;

            if (supplierId.HasValue)
            {
                var supplier = db.Suppliers
                    .Include("SupplierProducts")
                    .Include("SupplierProducts.Product")
                    .Include("SupplierProducts.Product.Category")
                    .FirstOrDefault(s => s.SupplierId == supplierId.Value);

                ViewBag.SelectedSupplier = supplier;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(int supplierId, string notes,
            int[] productIds, int[] quantities, decimal[] unitCosts)
        {
            if (productIds == null || productIds.Length == 0)
            {
                TempData["Error"] = "Please add at least one product.";
                return RedirectToAction("Create", new { supplierId });
            }

            var lines = new List<(int ProductId, int Qty, decimal Cost)>();
            for (int i = 0; i < productIds.Length; i++)
            {
                if (quantities[i] <= 0) continue;
                lines.Add((productIds[i], quantities[i], unitCosts[i]));
            }

            if (!lines.Any())
            {
                TempData["Error"] = "All quantities were zero. Please enter valid quantities.";
                return RedirectToAction("Create", new { supplierId });
            }

            var po = _inventoryService.CreatePurchaseOrder(supplierId, lines, notes);

            TempData["Success"] = $"Purchase Order {po.PoNumber} created as Draft.";
            return RedirectToAction("Details", new { id = po.PurchaseOrderId });
        }

        // ─── Auto-create POs from low stock ──────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AutoCreate()
        {
            var created = _inventoryService.AutoCreateLowStockOrders();

            if (!created.Any())
                TempData["Info"] = "No low-stock products with linked suppliers found.";
            else
                TempData["Success"] =
                    $"{created.Count} purchase order(s) created as Draft. " +
                    "Review and approve them to send to suppliers.";

            return RedirectToAction("Index");
        }

        // ─── View PO Details ──────────────────────────────────────────────────────

        public ActionResult Details(int id)
        {
            var po = db.PurchaseOrders
                .Include("Supplier")
                .Include("LineItems")
                .Include("LineItems.Product")
                .Include("LineItems.Product.Category")
                .FirstOrDefault(p => p.PurchaseOrderId == id);

            if (po == null) return NotFound();
            return View(po);
        }

        // ─── Approve PO ───────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id)
        {
            var (success, error) = _inventoryService.ApprovePurchaseOrder(id);

            if (success)
                TempData["Success"] = "Purchase order approved and sent to supplier via email.";
            else
                TempData["Error"] = error;

            return RedirectToAction("Details", new { id });
        }

        // ─── Receive Full Delivery ────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Receive(int id, string notes)
        {
            var (success, error) = _inventoryService.ReceivePurchaseOrder(id, notes);

            if (success)
                TempData["Success"] = "Delivery received. Stock has been updated for all items.";
            else
                TempData["Error"] = error;

            return RedirectToAction("Details", new { id });
        }

        // ─── Cancel PO ────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var (success, warning) = _inventoryService.CancelPurchaseOrder(id);

            if (success)
                // warning is non-null when the PO was already sent to the supplier.
                TempData[warning != null ? "Warning" : "Success"] =
                    warning ?? "Purchase order cancelled.";
            else
                TempData["Error"] = warning; // error message when success == false

            return RedirectToAction("Details", new { id });
        }

        // ─── Get supplier products as JSON (for dynamic PO form) ──────────────────

        public JsonResult GetSupplierProducts(int supplierId)
        {
            var products = db.SupplierProducts
                .Include("Product")
                .Include("Product.Category")
                .Where(sp => sp.SupplierId == supplierId)
                .Select(sp => new
                {
                    productId = sp.ProductId,
                    productName = sp.Product.Name,
                    category = sp.Product.Category.Name,
                    currentStock = sp.Product.QuantityInStock,
                    reorderLevel = sp.Product.ReorderLevel,
                    unitCost = sp.UnitCost,
                    minOrderQty = sp.MinOrderQty,
                    isLowStock = sp.Product.QuantityInStock <= sp.Product.ReorderLevel
                })
                .OrderBy(x => x.category)
                .ThenBy(x => x.productName)
                .ToList();

            return Json(products);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}