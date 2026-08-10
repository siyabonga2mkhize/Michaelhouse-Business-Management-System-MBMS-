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
    /// Accepts any of the role names used across seeding/testing so
    /// nothing breaks regardless of which one was used to create the account.
    /// </summary>
    public class ReporterController : Controller
    {
        private DBContextClass db = new DBContextClass();

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

            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.LocationBuilding)
                .ThenBy(a => a.AssetName)
                .ToList();

            ViewBag.SelectedAssetId = assetId;

            var userId = GetUserId();
            var myReports = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .Where(j => j.ReportedById == userId)
                .OrderByDescending(j => j.DateCreated)
                .ToList();
            ViewBag.MyReports = myReports;

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
        public ActionResult SubmitFault(
            JobCard jobCard,
            HttpPostedFileBase photoBefore,
            string photoData,
            // ── Manual entry fields ────────────────────────────────────
            string manualAssetName,
            string manualAssetLocation,
            string manualCategory)
        {
            if (!IsReporter())
                return RedirectToAction("Login", "Account");

            int userId = GetUserId();

            if (ModelState.IsValid)
            {
                // ── Determine if this is a manual entry ──────────────────
                bool isManual = string.IsNullOrEmpty(jobCard.AssetId?.ToString()) ||
                                jobCard.AssetId == 0;

                if (isManual)
                {
                    jobCard.ManualAssetName = manualAssetName;
                    jobCard.ManualAssetLocation = manualAssetLocation;
                    jobCard.ManualCategory = manualCategory;
                    jobCard.AssetId = null;
                }
                else
                {
                    var asset = db.Assets.Find(jobCard.AssetId);
                    if (asset == null)
                    {
                        TempData["Warning"] = "That asset could not be found. "
                            + "Please select the asset again and resubmit.";

                        ViewBag.Assets = db.Assets
                            .Where(a => a.Status == "Active")
                            .OrderBy(a => a.LocationBuilding)
                            .ThenBy(a => a.AssetName)
                            .ToList();

                        ViewBag.MyReports = db.JobCards
                            .Include(j => j.Asset)
                            .Include(j => j.AssignedTo)
                            .Where(j => j.ReportedById == userId)
                            .OrderByDescending(j => j.DateCreated)
                            .ToList();

                        return View("ReportFault", jobCard);
                    }

                    // ── Update asset ─────────────────────────────────────
                    asset.FaultCount++;
                    asset.Status = "Under Repair";
                    asset.HealthScore = Math.Max(0, asset.HealthScore - 10);
                }

                // ── Handle photo ─────────────────────────────────────────
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
                else if (!string.IsNullOrEmpty(photoData) && photoData.StartsWith("data:image"))
                {
                    try
                    {
                        var base64 = photoData.Substring(photoData.IndexOf(',') + 1);
                        var bytes = Convert.FromBase64String(base64);
                        var fileName = "before_" + DateTime.Now.Ticks + ".png";
                        var savePath = Server.MapPath("~/Content/JobPhotos/");

                        if (!System.IO.Directory.Exists(savePath))
                            System.IO.Directory.CreateDirectory(savePath);

                        System.IO.File.WriteAllBytes(savePath + fileName, bytes);
                        jobCard.PhotoBefore = fileName;
                    }
                    catch
                    {
                        // If decoding fails, continue without a photo
                    }
                }

                // ── Create job card ──────────────────────────────────────────
                var count = db.JobCards.Count();
                jobCard.JobReference = string.Format("MH-JOB-{0:D4}", count + 1);
                jobCard.DateCreated = DateTime.Now;
                jobCard.JobType = "Reactive";
                jobCard.Status = "Pending";
                jobCard.ReportedById = userId;

                jobCard.DueDate = jobCard.Priority == "Emergency"
                    ? DateTime.Now.AddHours(1)
                    : jobCard.Priority == "High"
                    ? DateTime.Now.AddHours(8)
                    : jobCard.Priority == "Medium"
                    ? DateTime.Now.AddDays(3)
                    : DateTime.Now.AddDays(7);

                db.JobCards.Add(jobCard);
                db.SaveChanges();

                // ── Auto‑assign worker (only if linked to an asset) ────────
                if (!isManual)
                {
                    var asset = db.Assets.Find(jobCard.AssetId);
                    if (asset != null)
                    {
                        var skillNeeded = asset.Category;
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
                                + "Job Card " + jobCard.JobReference + " created and assigned to "
                                + worker.FullName + " (" + worker.SkillType + "). They will attend to it shortly.";
                        }
                        else
                        {
                            TempData["Warning"] = "Fault reported successfully. "
                                + "Job Card " + jobCard.JobReference + " created. "
                                + "No workers are available right now — "
                                + "the Maintenance Manager has been notified.";
                        }
                    }
                    else
                    {
                        TempData["Warning"] = "Fault reported successfully. "
                            + "Job Card " + jobCard.JobReference + " created, but the asset could not be found.";
                    }
                }
                else
                {
                    // For manual entries, no auto‑assign (manager will handle)
                    TempData["Success"] = "Your manual fault report has been submitted. "
                        + "Job Card " + jobCard.JobReference + " created. "
                        + "The Maintenance Manager will review and assign a worker.";
                }

                return RedirectToAction("ReportFault");
            }

            // ── Validation failed — reload the form ──────────────────────────
            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.LocationBuilding)
                .ThenBy(a => a.AssetName)
                .ToList();

            ViewBag.MyReports = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .Where(j => j.ReportedById == userId)
                .OrderByDescending(j => j.DateCreated)
                .ToList();

            return View("ReportFault", jobCard);
        }

        // ══════════════════════════════════════════════════════════════════════
        // IN-APP QR SCANNER LOOKUP
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet]
        public JsonResult GetAssetByQrCode(string code)
        {
            if (!IsReporter())
                return Json(new { success = false, message = "Not authorized." },
                    JsonRequestBehavior.AllowGet);

            if (string.IsNullOrEmpty(code))
                return Json(new { success = false, message = "No code received." },
                    JsonRequestBehavior.AllowGet);

            var asset = db.Assets.FirstOrDefault(a =>
                a.QrCode == code && a.Status == "Active");

            if (asset == null)
                return Json(new
                {
                    success = false,
                    message = "No matching active asset found for this QR code."
                },
                    JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                id = asset.Id,
                name = asset.AssetName,
                building = asset.LocationBuilding,
                room = asset.LocationRoom,
                category = asset.Category
            }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}