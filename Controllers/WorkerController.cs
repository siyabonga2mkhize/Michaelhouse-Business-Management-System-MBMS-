using Michaelhouse.Models;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
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
        public ActionResult MyProfile()
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
        public ActionResult MyProfile(MaintenanceStaff model)
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

                db.SaveChanges();
                TempData["Success"] = "Profile updated successfully.";
                return RedirectToAction("MyProfile");
            }
            return View(model);
        }

        //// ══════════════════════════════════════════════════════════════════════
        //// LEAVE REQUEST
        //// ══════════════════════════════════════════════════════════════════════
        //public ActionResult RequestLeave()
        //{
        //    if (!IsWorker())
        //        return RedirectToAction("Login", "Account");
        //    return View();
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult RequestLeave(LeaveRequestViewModel model)
        //{
        //    if (!IsWorker())
        //        return RedirectToAction("Login", "Account");

        //    if (ModelState.IsValid)
        //    {
        //        var staffId = GetStaffId();
        //        var leave = new LeaveRequest
        //        {
        //            StaffId = staffId,
        //            Type = model.Type,
        //            StartDate = model.StartDate,
        //            EndDate = model.EndDate,
        //            Reason = model.Reason,
        //            Status = "Pending",
        //            RequestedAt = DateTime.Now
        //        };
        //        db.LeaveRequests.Add(leave);
        //        db.SaveChanges();
        //        TempData["Success"] = "Leave request submitted for approval.";
        //        return RedirectToAction("MyJobs");
        //    }
        //    return View(model);
        //}

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

        // ── COMPLETE JOB ──────────────────────────────────────────────────────
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
                                decimal totalCost)
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

            // ── Multiple photo handling ────────────────────────────
            if (Request.Files != null && Request.Files.Count > 0)
            {
                foreach (string fileKey in Request.Files)
                {
                    var file = Request.Files[fileKey];
                    if (file != null && file.ContentLength > 0)
                    {
                        var ext = Path.GetExtension(file.FileName);
                        var fileName = "after_" + jobCard.JobReference.Replace("-", "_") + "_" + DateTime.Now.Ticks + ext;
                        var savePath = Server.MapPath("~/Content/JobPhotos/");
                        if (!Directory.Exists(savePath))
                            Directory.CreateDirectory(savePath);
                        file.SaveAs(Path.Combine(savePath, fileName));

                        db.JobCardPhotos.Add(new JobCardPhoto
                        {
                            JobCardId = jobCard.Id,
                            FileName = fileName,
                            UploadedAt = DateTime.Now
                        });
                    }
                }
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

            // ── Save parts used ──────────────────────────────────────
            if (inventoryIds != null && quantities != null && inventoryIds.Length == quantities.Length)
            {
                for (int i = 0; i < inventoryIds.Length; i++)
                {
                    int qty = quantities[i];
                    if (qty <= 0) continue;
                    var inventoryItem = db.MaintenanceInventory.Find(inventoryIds[i]);
                    if (inventoryItem == null) continue;

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

            // ── Update job card fields ──────────────────────────────
            jobCard.CompletionNotes = completionNotes;
            jobCard.FinalCondition = finalCondition;
            jobCard.Status = "Completed";
            jobCard.DateCompleted = DateTime.Now;
            jobCard.ResponseTimeMinutes = (int)(DateTime.Now - jobCard.DateCreated).TotalMinutes;

            // Use the passed costs (from hidden fields)
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
                asset.TotalMaintenanceCost = (asset.TotalMaintenanceCost ?? 0) + (jobCard.PartsCost ?? 0) + (jobCard.LabourCost ?? 0);
            }

            // ── Free worker ──────────────────────────────────────────
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