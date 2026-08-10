using Michaelhouse.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    /// <summary>
    /// Handles the Fault Reporter portal — for Housemasters, Teachers,
    /// Sports Coordinators, Kitchen Staff etc.
    /// Role name: "FaultReporter" (matches the seeded role in Configuration.cs)
    /// </summary>
    public class ReporterController : Controller
    {
        private DBContextClass db = new DBContextClass();

        /// <summary>
        /// Checks if the logged-in user has the FaultReporter role.
        /// IMPORTANT: The seeded role is "FaultReporter" (not "MaintenanceReporter").
        /// </summary>
        private bool IsReporter()
        {
            return Session["UserId"] != null
                && Session["UserRole"] != null
                && (Session["UserRole"].ToString() == "FaultReporter" ||
                    Session["UserRole"].ToString() == "MaintenanceWorker" ||
                    Session["UserRole"].ToString() == "MaintenanceReporter");
        }

        private int GetUserId()
        {
            return Session["UserId"] != null ? (int)Session["UserId"] : 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        // FAULT REPORTER HOME PAGE
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult ReportFault(int? assetId = null)
        {
            if (!IsReporter())
                return RedirectToAction("Login", "Account");

            // ── Get all active assets ──────────────────────
            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.LocationBuilding)
                .ThenBy(a => a.AssetName)
                .ToList();

            // Pass the selected asset id to the view
            ViewBag.SelectedAssetId = assetId;

            // ── Get this user's reported faults ─────────────
            var userId = GetUserId();
            var myReports = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .Where(j => j.ReportedById == userId)
                .OrderByDescending(j => j.DateCreated)
                .ToList();
            ViewBag.MyReports = myReports;

            // ── Stats ──────────────────────────────────────────
            ViewBag.TotalReported = myReports.Count;
            ViewBag.PendingCount = myReports.Count(j => j.Status == "Pending" || j.Status == "Assigned");
            ViewBag.FixedCount = myReports.Count(j => j.Status == "Completed");

            return View();
        }

        // ══════════════════════════════════════════════════════════════════════
        // SUBMIT A FAULT REPORT
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitFault(JobCard jobCard,
                                        HttpPostedFileBase photoBefore)
        {
            if (!IsReporter())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                // ── Handle photo upload ──────────────────────────────────────
                if (photoBefore != null && photoBefore.ContentLength > 0)
                {
                    var ext = System.IO.Path.GetExtension(photoBefore.FileName);
                    var fileName = "before_" + DateTime.Now.Ticks + ext;
                    var savePath = Server.MapPath("~/Content/JobPhotos/");

                    if (!System.IO.Directory.Exists(savePath))
                        System.IO.Directory.CreateDirectory(savePath);

                    photoBefore.SaveAs(savePath + fileName);
                    jobCard.PhotoBefore = fileName;
                }

                // ── Create job card ──────────────────────────────────────────
                var count = db.JobCards.Count();
                jobCard.JobReference = string.Format("MH-JOB-{0:D4}", count + 1);
                jobCard.DateCreated = DateTime.Now;
                jobCard.JobType = "Reactive";
                jobCard.Status = "Pending";
                jobCard.ReportedById = GetUserId();

                jobCard.DueDate = jobCard.Priority == "Emergency"
                    ? DateTime.Now.AddHours(1)
                    : jobCard.Priority == "High"
                    ? DateTime.Now.AddHours(8)
                    : jobCard.Priority == "Medium"
                    ? DateTime.Now.AddDays(3)
                    : DateTime.Now.AddDays(7);

                db.JobCards.Add(jobCard);

                // ── Update asset ─────────────────────────────────────────────
                var asset = db.Assets.Find(jobCard.AssetId);
                if (asset != null)
                {
                    asset.FaultCount++;
                    asset.Status = "Under Repair";
                    asset.HealthScore = Math.Max(0, asset.HealthScore - 10);
                }

                db.SaveChanges();

                // ── Auto‑assign worker ──────────────────────────────────────
                var skillNeeded = asset != null ? asset.Category : "General";
                if (skillNeeded == "Sports" || skillNeeded == "Building")
                    skillNeeded = "General";

                var worker = db.MaintenanceStaff
                    .Where(s => s.IsActive
                             && s.CurrentStatus == "Available"
                             && s.SkillType == skillNeeded)
                    .FirstOrDefault()
                    ?? db.MaintenanceStaff
                        .Where(s => s.IsActive
                                 && s.CurrentStatus == "Available"
                                 && s.SkillType == "General")
                        .FirstOrDefault()
                    ?? db.MaintenanceStaff
                        .Where(s => s.IsActive && s.CurrentStatus == "Available")
                        .FirstOrDefault();

                if (worker != null)
                {
                    jobCard.AssignedToId = worker.Id;
                    jobCard.Status = "Assigned";
                    jobCard.DateAssigned = DateTime.Now;
                    worker.CurrentStatus = "On Job";
                    db.SaveChanges();

                    TempData["Success"] = "Your fault report has been submitted. "
                        + "Job Card " + jobCard.JobReference + " created. "
                        + worker.FullName + " has been assigned and will attend to it.";
                }
                else
                {
                    TempData["Warning"] = "Fault reported successfully. "
                        + "Job Card " + jobCard.JobReference + " created. "
                        + "No workers are available right now — "
                        + "the Maintenance Manager has been notified.";
                }

                return RedirectToAction("Index", "Scan", new { id = asset.QrCode });
            }

            // ── If validation fails, reload the form ─────────────────────────
            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.LocationBuilding)
                .ThenBy(a => a.AssetName)
                .ToList();

            ViewBag.MyReports = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .Where(j => j.ReportedById == GetUserId())
                .OrderByDescending(j => j.DateCreated)
                .ToList();

            return View("ReportFault", jobCard);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}