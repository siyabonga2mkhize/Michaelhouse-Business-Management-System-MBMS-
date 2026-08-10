using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class WorkerController : Controller
    {
        private DBContextClass db = new DBContextClass();

        private bool IsWorker()
        {
            return Session["UserId"] != null
                && Session["UserRole"] != null
                && Session["UserRole"].ToString() == "MaintenanceWorker";
        }

        private int GetStaffId()
        {
            if (Session["MaintenanceStaffId"] != null
            && Session["MaintenanceStaffId"].ToString() != "")
                return (int)Session["MaintenanceStaffId"];
            return 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        // WORKER DASHBOARD – My Jobs, My Performance, My Schedule
        // ══════════════════════════════════════════════════════════════════════
        public ActionResult MyJobs()
        {
            if (!IsWorker())
                return RedirectToAction("Login", "Account");

            var staffId = GetStaffId();
            var staff = db.MaintenanceStaff.Find(staffId);
            if (staff == null) return RedirectToAction("Login", "Account");

            // Open jobs
            var myJobs = db.JobCards
                .Include(j => j.Asset)
                .Where(j => j.AssignedToId == staffId
                         && j.Status != "Completed"
                         && j.Status != "Cancelled")
                .OrderBy(j => j.Priority == "Emergency" ? 0 :
                              j.Priority == "High" ? 1 :
                              j.Priority == "Medium" ? 2 : 3)
                .ThenBy(j => j.DueDate)
                .ToList();

            // Completed jobs (last 5)
            var completedJobs = db.JobCards
                .Include(j => j.Asset)
                .Where(j => j.AssignedToId == staffId && j.Status == "Completed")
                .OrderByDescending(j => j.DateCompleted)
                .Take(5)
                .ToList();

            // Performance stats
            var allCompleted = db.JobCards
                .Where(j => j.AssignedToId == staffId && j.Status == "Completed")
                .ToList();
            int totalCompleted = allCompleted.Count;
            int completedThisMonth = allCompleted.Count(j =>
                j.DateCompleted.HasValue &&
                j.DateCompleted.Value.Month == DateTime.Now.Month);
            double avgResponse = allCompleted.Where(j => j.ResponseTimeMinutes.HasValue)
                .Select(j => j.ResponseTimeMinutes.Value)
                .DefaultIfEmpty(0)
                .Average();

            // Upcoming shifts (next 7 days)
            var upcomingShifts = db.StaffShifts
                .Include(ss => ss.ShiftPattern)
                .Where(ss => ss.StaffId == staffId && ss.Date >= DateTime.Today)
                .OrderBy(ss => ss.Date)
                .Take(7)
                .ToList();

            ViewBag.Staff = staff;
            ViewBag.CompletedJobs = completedJobs;
            ViewBag.TotalCompleted = totalCompleted;
            ViewBag.CompletedThisMonth = completedThisMonth;
            ViewBag.AvgResponseTime = (int)avgResponse;
            ViewBag.UpcomingShifts = upcomingShifts;

            return View(myJobs);
        }

        // ══════════════════════════════════════════════════════════════════════
        // WORKER PROFILE (view and edit personal info)
        // ══════════════════════════════════════════════════════════════════════
        public ActionResult Profile()
        {
            if (!IsWorker())
                return RedirectToAction("Login", "Account");

            var staffId = GetStaffId();
            var staff = db.MaintenanceStaff
                .Include(s => s.User)
                .Include(s => s.LeaveRequests)
                .Include(s => s.StaffShifts.Select(ss => ss.ShiftPattern))
                .FirstOrDefault(s => s.Id == staffId);
            if (staff == null) return RedirectToAction("Login", "Account");

            return View(staff);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(MaintenanceStaff model)
        {
            if (!IsWorker())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var staff = db.MaintenanceStaff.Find(model.Id);
                if (staff == null) return HttpNotFound();

                // Only allow editing safe fields
                staff.Phone = model.Phone;
                staff.EmergencyContactName = model.EmergencyContactName;
                staff.EmergencyContactPhone = model.EmergencyContactPhone;
                // Photo upload handled separately

                db.SaveChanges();
                TempData["Success"] = "Profile updated successfully.";
                return RedirectToAction("Profile");
            }
            return View(model);
        }

        // ══════════════════════════════════════════════════════════════════════
        // LEAVE REQUEST
        // ══════════════════════════════════════════════════════════════════════
        public ActionResult RequestLeave()
        {
            if (!IsWorker())
                return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestLeave(LeaveRequestViewModel model)
        {
            if (!IsWorker())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var staffId = GetStaffId();
                var leave = new LeaveRequest
                {
                    StaffId = staffId,
                    Type = model.Type,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Reason = model.Reason,
                    Status = "Pending",
                    RequestedAt = DateTime.Now
                };
                db.LeaveRequests.Add(leave);
                db.SaveChanges();
                TempData["Success"] = "Leave request submitted for approval.";
                return RedirectToAction("MyJobs");
            }
            return View(model);
        }

        // ══════════════════════════════════════════════════════════════════════
        // JOB DETAIL & COMPLETION
        // ══════════════════════════════════════════════════════════════════════
        public ActionResult JobDetail(int? id)
        {
            if (!IsWorker())
                return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("MyJobs");

            var jobCard = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .FirstOrDefault(j => j.Id == id);
            if (jobCard == null) return HttpNotFound();

            ViewBag.Inventory = db.MaintenanceInventory
                .Where(i => i.StockLevel > 0)
                .OrderBy(i => i.Category)
                .ThenBy(i => i.ItemName)
                .ToList();

            ViewBag.PartsUsed = db.JobCardParts
                .Include(p => p.InventoryItem)
                .Where(p => p.JobCardId == id)
                .ToList();

            return View(jobCard);
        }

        // ── UPDATE STATUS ──────────────────────────────────────────────────────
        [HttpPost]
        public ActionResult UpdateStatus(int id, string status)
        {
            if (!IsWorker())
                return Json(new { success = false });

            var staff = db.MaintenanceStaff.Find(id);
            if (staff == null)
                return Json(new { success = false });

            staff.CurrentStatus = status;
            db.SaveChanges();
            return Json(new { success = true, newStatus = status });
        }

        [HttpPost]
        public async Task<JsonResult> SearchParts(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new { success = false, message = "Please enter a search term." });

            try
            {
                string token = GetNexarToken();
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    string url = $"https://api.nexar.com/v1/parts/search?q={Uri.EscapeDataString(query)}&limit=10";
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();

                    // Parse the response (simplified)
                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                    var results = new List<object>();
                    if (data.results != null)
                    {
                        foreach (var item in data.results)
                        {
                            // Get best price (e.g., from first supplier)
                            decimal? unitPrice = null;
                            if (item.prices != null && item.prices.Count > 0)
                            {
                                // Simplistic: take the lowest price from the first supplier's first break
                                foreach (var priceSet in item.prices)
                                {
                                    if (priceSet.breaks != null && priceSet.breaks.Count > 0)
                                    {
                                        var firstBreak = priceSet.breaks[0];
                                        unitPrice = (decimal?)firstBreak.price;
                                        break;
                                    }
                                }
                            }

                            results.Add(new
                            {
                                mpn = (string)item.mpn,
                                manufacturer = (string)item.manufacturer?.name,
                                description = (string)item.short_description,
                                unitPrice = unitPrice
                            });
                        }
                    }
                    return Json(new { success = true, results });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteJob(int id,
                                string completionNotes,
                                string finalCondition,
                                HttpPostedFileBase photoAfter,
                                HttpPostedFileBase receipt,
                                int[] inventoryIds,
                                int[] quantities,
                                decimal labourCost,
                                decimal partsCost,
                                decimal totalCost,
                                string[] externalNames,
                                string[] externalManufacturers,
                                string[] externalMPNs,
                                decimal[] externalUnitCosts,
                                int[] externalQuantities)
        {
            if (!IsWorker())
                return RedirectToAction("Login", "Account");

            var jobCard = db.JobCards
                .Include(j => j.Asset)
                .FirstOrDefault(j => j.Id == id);

            if (jobCard == null) return HttpNotFound();

            // ── Photo handling ──────────────────────────────────────
            if (photoAfter != null && photoAfter.ContentLength > 0)
            {
                var ext = System.IO.Path.GetExtension(photoAfter.FileName);
                var fileName = "after_" + jobCard.JobReference.Replace("-", "_") + "_" + DateTime.Now.Ticks + ext;
                var savePath = Server.MapPath("~/Content/JobPhotos/");
                if (!System.IO.Directory.Exists(savePath))
                    System.IO.Directory.CreateDirectory(savePath);
                photoAfter.SaveAs(savePath + fileName);
                jobCard.PhotoAfter = fileName;
            }

            // ── Receipt handling ──────────────────────────────────────
            if (receipt != null && receipt.ContentLength > 0)
            {
                var ext = System.IO.Path.GetExtension(receipt.FileName);
                var fileName = "receipt_" + jobCard.JobReference.Replace("-", "_") + "_" + DateTime.Now.Ticks + ext;
                var savePath = Server.MapPath("~/Content/Receipts/");
                if (!System.IO.Directory.Exists(savePath))
                    System.IO.Directory.CreateDirectory(savePath);
                receipt.SaveAs(savePath + fileName);
                //jobCard.ReceiptFileName = fileName;
            }

            // ── Save parts used and calculate PartsCost ───────────────
            decimal calculatedPartsCost = 0;
            if (inventoryIds != null && quantities != null && inventoryIds.Length == quantities.Length)
            {
                for (int i = 0; i < inventoryIds.Length; i++)
                {
                    int qty = quantities[i];
                    if (qty <= 0) continue;
                    var inventoryItem = db.MaintenanceInventory.Find(inventoryIds[i]);
                    if (inventoryItem == null) continue;
                    // reduce stock and save JobCardPart (existing)
                    inventoryItem.StockLevel -= qty;
                    db.JobCardParts.Add(new JobCardPart
                    {
                        JobCardId = id,
                        InventoryItemId = inventoryItem.Id,
                        QuantityUsed = qty,
                        DateUsed = DateTime.Now
                    });
                }
            }
            if (externalNames != null && externalNames.Length > 0)
            {
                for (int i = 0; i < externalNames.Length; i++)
                {
                    int qty = externalQuantities != null && i < externalQuantities.Length ? externalQuantities[i] : 1;
                    if (qty <= 0) continue;
                    decimal unitCost = externalUnitCosts != null && i < externalUnitCosts.Length ? externalUnitCosts[i] : 0;
                    db.JobCardParts.Add(new JobCardPart
                    {
                        JobCardId = id,
                        InventoryItemId = null,
                        QuantityUsed = qty,
                        DateUsed = DateTime.Now,
                        ExternalPartName = externalNames[i],
                        ExternalManufacturer = externalManufacturers != null && i < externalManufacturers.Length ? externalManufacturers[i] : null,
                        ExternalUnitCost = unitCost
                    });
                }
            }

            // ── Update job card fields ──────────────────────────────
            jobCard.CompletionNotes = completionNotes;
            jobCard.FinalCondition = finalCondition;
            jobCard.Status = "Completed";
            jobCard.DateCompleted = DateTime.Now;
            jobCard.ResponseTimeMinutes = (int)(DateTime.Now - jobCard.DateCreated).TotalMinutes;

            // Use the passed costs (from hidden fields) to ensure consistency
            jobCard.PartsCost = partsCost;
            jobCard.LabourCost = labourCost;
            jobCard.TotalCost = totalCost;

            // ── Update asset ─────────────────────────────────────────
            var asset = db.Assets.Find(jobCard.AssetId);
            if (asset != null)
            {
                asset.Status = "Active";
                if (finalCondition == "Fixed" || finalCondition == "Replaced")
                {
                    asset.ConditionRating = "Good";
                    asset.HealthScore = Math.Min(100, asset.HealthScore + 20);
                }
                else if (finalCondition == "Repaired Temporarily")
                {
                    asset.ConditionRating = "Fair";
                    asset.HealthScore = Math.Min(100, asset.HealthScore + 10);
                }
                // Update total maintenance cost
                asset.TotalMaintenanceCost = (asset.TotalMaintenanceCost ?? 0) + (jobCard.PartsCost ?? 0) + (jobCard.LabourCost ?? 0);
            }

            // ── Free worker ────────────────────────────────────────────
            var worker = db.MaintenanceStaff.Find(GetStaffId());
            if (worker != null) worker.CurrentStatus = "Available";

            db.SaveChanges();

            TempData["Success"] = "Job " + jobCard.JobReference + " completed!";
            return RedirectToAction("MyJobs");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}