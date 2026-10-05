using Michaelhouse.Models;
using Newtonsoft.Json;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Mobile — Report Asset Failure (any logged-in user)
    //
    //   GET  /api/fault-reports/form              assets, choices, my reports
    //   GET  /api/fault-reports/asset?code=...    asset from its QR code
    //   POST /api/fault-reports                   { assetId | manual..., title,
    //                                               description, priority,
    //                                               photoBase64 }
    //
    // Same job card as the web page Reporter/SubmitFault: reference
    // MH-JOB-0001…, due date from the priority, the asset marked Under
    // Repair, the photo saved in Content/JobPhotos, and the job
    // auto-assigned to an available maintenance worker with the right
    // skill (manual entries are left for the Maintenance Manager).
    // On the website only fault reporters / maintenance staff report
    // faults; on mobile any logged-in user can.
    // ============================================================
    [RoutePrefix("api/fault-reports")]
    [Authorize]
    public class FaultReportApiController : Controller
    {
        public static readonly string[] Priorities = { "Emergency", "High", "Medium", "Low" };

        public static readonly string[] Categories =
            { "Electrical", "Plumbing", "HVAC", "Grounds", "Sports", "Building", "Safety", "IT", "Kitchen", "General" };

        private readonly DBContextClass db = new DBContextClass();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET /api/fault-reports/form
        // ============================================================

        [HttpGet]
        [Route("form")]
        public JsonResult Form()
        {
            int userId = CurrentUserId();

            var assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.LocationBuilding)
                .ThenBy(a => a.AssetName)
                .ToList();

            var mine = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .Where(j => j.ReportedById == userId)
                .OrderByDescending(j => j.DateCreated)
                .Take(20)
                .ToList();

            return Json(new
            {
                ok = true,
                priorities = Priorities,
                categories = Categories,
                assets = assets.Select(a => new
                {
                    id = a.Id,
                    name = a.AssetName,
                    building = a.LocationBuilding,
                    room = a.LocationRoom,
                    category = a.Category
                }),
                myReports = mine.Select(j => new
                {
                    jobReference = j.JobReference,
                    title = j.Title,
                    asset = j.Asset != null ? j.Asset.AssetName : j.ManualAssetName,
                    priority = j.Priority,
                    status = j.Status,
                    reported = j.DateCreated.ToString("ddd dd MMM yyyy, HH:mm"),
                    assignedTo = j.AssignedTo != null ? j.AssignedTo.FullName : null
                })
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // GET /api/fault-reports/asset?code=... — Reporter/GetAssetByQrCode
        // ============================================================

        [HttpGet]
        [Route("asset")]
        public JsonResult AssetByQrCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { ok = false, error = "No code received." }, JsonRequestBehavior.AllowGet);

            var asset = db.Assets.FirstOrDefault(a => a.QrCode == code && a.Status == "Active");
            if (asset == null)
                return Json(new { ok = false, error = "No matching active asset found for this QR code." }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                ok = true,
                id = asset.Id,
                name = asset.AssetName,
                building = asset.LocationBuilding,
                room = asset.LocationRoom,
                category = asset.Category
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // POST /api/fault-reports — Reporter/SubmitFault
        // ============================================================

        [HttpPost]
        [Route("")]
        public JsonResult Submit()
        {
            int userId = CurrentUserId();
            if (userId <= 0) return Json(new { ok = false, error = "Please log in again." });

            var data = ReadBody<FaultReportRequest>();
            if (data == null) return Json(new { ok = false, error = "The report could not be read." });

            // Same required fields as the web form
            var errors = new System.Collections.Generic.List<string>();
            bool isManual = !data.AssetId.HasValue || data.AssetId.Value == 0;
            if (string.IsNullOrWhiteSpace(data.Title)) errors.Add("Say what the problem is.");
            if (string.IsNullOrWhiteSpace(data.Description)) errors.Add("Describe the problem.");
            if (!Priorities.Contains(data.Priority)) errors.Add("Choose how urgent it is.");
            if (isManual && (string.IsNullOrWhiteSpace(data.ManualAssetName) || string.IsNullOrWhiteSpace(data.ManualAssetLocation)))
                errors.Add("Choose the broken item, or type what it is and where it is.");
            if (isManual && !string.IsNullOrWhiteSpace(data.ManualCategory) && !Categories.Contains(data.ManualCategory))
                errors.Add("Choose a category from the list.");
            if (errors.Count > 0) return Json(new { ok = false, error = string.Join(" ", errors) });

            var jobCard = new JobCard
            {
                Title = data.Title.Trim(),
                Description = data.Description.Trim(),
                Priority = data.Priority
            };

            if (isManual)
            {
                jobCard.ManualAssetName = data.ManualAssetName.Trim();
                jobCard.ManualAssetLocation = data.ManualAssetLocation.Trim();
                jobCard.ManualCategory = string.IsNullOrWhiteSpace(data.ManualCategory) ? null : data.ManualCategory;
                jobCard.AssetId = null;
            }
            else
            {
                var asset = db.Assets.Find(data.AssetId.Value);
                if (asset == null)
                    return Json(new { ok = false, error = "That asset could not be found. Please select the asset again and resubmit." });

                jobCard.AssetId = asset.Id;
                asset.FaultCount++;
                asset.Status = "Under Repair";
                asset.HealthScore = Math.Max(0, asset.HealthScore - 10);
            }

            // ── Photo (Content/JobPhotos, like the web form) ──
            if (!string.IsNullOrWhiteSpace(data.PhotoBase64))
            {
                try
                {
                    var base64 = data.PhotoBase64;
                    int comma = base64.IndexOf(',');
                    if (comma >= 0) base64 = base64.Substring(comma + 1);
                    var bytes = Convert.FromBase64String(base64);

                    var fileName = "before_" + DateTime.Now.Ticks + ".jpg";
                    var savePath = Server.MapPath("~/Content/JobPhotos/");
                    if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
                    System.IO.File.WriteAllBytes(savePath + fileName, bytes);
                    jobCard.PhotoBefore = fileName;
                }
                catch (FormatException)
                {
                    // If decoding fails, continue without a photo (as the web form does)
                }
            }

            // ── Create job card ──
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

            string message;

            // ── Auto-assign a worker (only when linked to an asset) ──
            if (!isManual)
            {
                var asset = db.Assets.Find(jobCard.AssetId);
                var skillNeeded = asset.Category;
                if (skillNeeded == "Sports" || skillNeeded == "Building")
                    skillNeeded = "General";

                var worker = db.MaintenanceStaff
                    .Where(s => s.IsActive && s.CurrentStatus == "Available" && s.SkillType == skillNeeded)
                    .FirstOrDefault()
                    ?? db.MaintenanceStaff
                        .Where(s => s.IsActive && s.CurrentStatus == "Available" && s.SkillType == "General")
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

                    message = "Your fault report has been submitted. Job Card " + jobCard.JobReference +
                              " created and assigned to " + worker.FullName + " (" + worker.SkillType + "). They will attend to it shortly.";
                }
                else
                {
                    message = "Fault reported successfully. Job Card " + jobCard.JobReference +
                              " created. No workers are available right now — the Maintenance Manager has been notified.";
                }
            }
            else
            {
                message = "Your fault report has been submitted. Job Card " + jobCard.JobReference +
                          " created. The Maintenance Manager will review and assign a worker.";
            }

            return Json(new { ok = true, jobReference = jobCard.JobReference, status = jobCard.Status, message });
        }

        private int CurrentUserId()
        {
            int id;
            return Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out id) ? id : 0;
        }

        private T ReadBody<T>() where T : class
        {
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public class FaultReportRequest
    {
        public int? AssetId { get; set; }
        public string ManualAssetName { get; set; }
        public string ManualAssetLocation { get; set; }
        public string ManualCategory { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public string PhotoBase64 { get; set; }
    }
}
