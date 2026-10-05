using Michaelhouse.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Mobile — Complete Job Card (Maintenance Worker)
    //
    //   GET  /api/worker-jobs              my open jobs + recently completed
    //   GET  /api/worker-jobs/{id}         one job, its "before" photo,
    //                                      parts in stock
    //   POST /api/worker-jobs/{id}/complete
    //        { completionNotes, finalCondition, photoAfterBase64,
    //          parts: [ { inventoryId, quantity } ], labourCost }
    //
    // Same steps as the web page Worker/JobDetail + Worker/CompleteJob:
    // the repair photo is required, parts come out of maintenance stock,
    // parts cost = quantity × unit cost, total = parts + labour, the job
    // is Completed with its response time, the asset goes back to Active
    // (healthier when Fixed / Replaced / Repaired Temporarily) and the
    // worker becomes Available again.
    // Extra checks on mobile: only the worker's own assigned, open jobs,
    // and no more parts than are in stock.
    // ============================================================
    [RoutePrefix("api/worker-jobs")]
    [Authorize(Roles = "MaintenanceWorker")]
    public class WorkerJobsApiController : Controller
    {
        public static readonly string[] FinalConditions = { "Fixed", "Replaced", "Repaired Temporarily" };

        private readonly DBContextClass db = new DBContextClass();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET /api/worker-jobs — Worker/MyJobs
        // ============================================================

        [HttpGet]
        [Route("")]
        public JsonResult MyJobs()
        {
            int staffId = StaffId();
            var staff = db.MaintenanceStaff.Find(staffId);
            if (staff == null) return Json(new { ok = false, error = "No maintenance staff record is linked to this account." }, JsonRequestBehavior.AllowGet);

            var open = db.JobCards
                .Include(j => j.Asset)
                .Where(j => j.AssignedToId == staffId && j.Status != "Completed" && j.Status != "Cancelled")
                .OrderBy(j => j.Priority == "Emergency" ? 0 : j.Priority == "High" ? 1 : j.Priority == "Medium" ? 2 : 3)
                .ThenBy(j => j.DueDate)
                .ToList();

            var completed = db.JobCards
                .Include(j => j.Asset)
                .Where(j => j.AssignedToId == staffId && j.Status == "Completed")
                .OrderByDescending(j => j.DateCompleted)
                .Take(5)
                .ToList();

            return Json(new
            {
                ok = true,
                worker = staff.FullName,
                skill = staff.SkillType,
                currentStatus = staff.CurrentStatus,
                openJobs = open.Select(Summary),
                completedJobs = completed.Select(Summary)
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // GET /api/worker-jobs/5 — Worker/JobDetail
        // ============================================================

        [HttpGet]
        [Route("{id:int}")]
        public JsonResult Detail(int id)
        {
            var job = OwnJob(id);
            if (job == null) return Json(new { ok = false, error = "This job isn't assigned to you." }, JsonRequestBehavior.AllowGet);

            var inventory = db.MaintenanceInventory
                .Where(i => i.StockLevel > 0)
                .OrderBy(i => i.Category)
                .ThenBy(i => i.ItemName)
                .ToList();

            return Json(new
            {
                ok = true,
                job = Summary(job),
                description = job.Description,
                location = job.Asset != null
                    ? string.Join(", ", new[] { job.Asset.LocationBuilding, job.Asset.LocationRoom }.Where(x => !string.IsNullOrEmpty(x)))
                    : job.ManualAssetLocation,
                photoBeforeBase64 = PhotoBase64(job.PhotoBefore),
                finalConditions = FinalConditions,
                inventory = inventory.Select(i => new
                {
                    id = i.Id,
                    name = i.ItemName,
                    category = i.Category,
                    stock = i.StockLevel,
                    unit = i.Unit,
                    unitCost = i.UnitCost
                })
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // POST /api/worker-jobs/5/complete — Worker/CompleteJob
        // ============================================================

        [HttpPost]
        [Route("{id:int}/complete")]
        public JsonResult Complete(int id)
        {
            var job = OwnJob(id);
            if (job == null) return Json(new { ok = false, error = "This job isn't assigned to you." });
            if (job.Status == "Completed" || job.Status == "Cancelled")
                return Json(new { ok = false, error = "Job " + job.JobReference + " is already " + job.Status.ToLower() + "." });

            var input = ReadBody<CompleteJobInput>();
            if (input == null) return Json(new { ok = false, error = "The job sign-off could not be read." });

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(input.PhotoAfterBase64)) errors.Add("Take a photo of the repair (Repair Photo Proof).");
            if (!FinalConditions.Contains(input.FinalCondition)) errors.Add("Choose the final condition.");
            if (input.LabourCost < 0) errors.Add("Labour cost can't be negative.");

            // Parts: in stock, and no more than what's there
            var parts = (input.Parts ?? new List<PartInput>()).Where(p => p != null && p.Quantity > 0).ToList();
            var items = new Dictionary<int, MaintenanceInventory>();
            foreach (var group in parts.GroupBy(p => p.InventoryId))
            {
                var item = db.MaintenanceInventory.Find(group.Key);
                int qty = group.Sum(p => p.Quantity);
                if (item == null) errors.Add("One of the parts isn't in the maintenance store.");
                else if (qty > item.StockLevel) errors.Add(string.Format("Only {0} {1} of {2} in stock.", item.StockLevel, item.Unit, item.ItemName));
                else items[item.Id] = item;
            }

            byte[] photo = null;
            if (!string.IsNullOrWhiteSpace(input.PhotoAfterBase64))
            {
                try
                {
                    var base64 = input.PhotoAfterBase64;
                    int comma = base64.IndexOf(',');
                    if (comma >= 0) base64 = base64.Substring(comma + 1);
                    photo = Convert.FromBase64String(base64);
                }
                catch (FormatException)
                {
                    errors.Add("The repair photo could not be read. Please take it again.");
                }
            }

            if (errors.Count > 0) return Json(new { ok = false, error = string.Join(" ", errors), errors });

            // ── Photo (Content/JobPhotos, named like the web's) ──
            var fileName = "after_" + job.JobReference.Replace("-", "_") + "_" + DateTime.Now.Ticks + ".jpg";
            var savePath = Server.MapPath("~/Content/JobPhotos/");
            if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
            System.IO.File.WriteAllBytes(Path.Combine(savePath, fileName), photo);
            job.PhotoAfter = fileName;
            db.JobCardPhotos.Add(new JobCardPhoto { JobCardId = job.Id, FileName = fileName, UploadedAt = DateTime.Now });

            // ── Parts used (cost worked out here, not trusted from the app) ──
            decimal partsCost = 0m;
            foreach (var p in parts)
            {
                var item = items[p.InventoryId];
                item.StockLevel -= p.Quantity;
                partsCost += p.Quantity * item.UnitCost;
                db.JobCardParts.Add(new JobCardPart
                {
                    JobCardId = job.Id,
                    InventoryItemId = item.Id,
                    QuantityUsed = p.Quantity,
                    DateUsed = DateTime.Now
                });
            }

            // ── Job card ──
            job.CompletionNotes = string.IsNullOrWhiteSpace(input.CompletionNotes) ? null : input.CompletionNotes.Trim();
            job.FinalCondition = input.FinalCondition;
            job.Status = "Completed";
            job.DateCompleted = DateTime.Now;
            job.ResponseTimeMinutes = (int)(DateTime.Now - job.DateCreated).TotalMinutes;
            job.PartsCost = partsCost;
            job.LabourCost = input.LabourCost;
            job.TotalCost = partsCost + input.LabourCost;

            // ── Asset ──
            var asset = job.AssetId.HasValue ? db.Assets.Find(job.AssetId.Value) : null;
            if (asset != null)
            {
                asset.Status = "Active";
                if (input.FinalCondition == "Fixed" || input.FinalCondition == "Replaced")
                {
                    asset.ConditionRating = "Good";
                    asset.HealthScore = Math.Min(100, asset.HealthScore + 20);
                }
                else if (input.FinalCondition == "Repaired Temporarily")
                {
                    asset.ConditionRating = "Fair";
                    asset.HealthScore = Math.Min(100, asset.HealthScore + 10);
                }
                asset.TotalMaintenanceCost = (asset.TotalMaintenanceCost ?? 0) + (job.PartsCost ?? 0) + (job.LabourCost ?? 0);
            }

            // ── Free the worker ──
            var worker = db.MaintenanceStaff.Find(StaffId());
            if (worker != null) worker.CurrentStatus = "Available";

            db.SaveChanges();

            return Json(new
            {
                ok = true,
                message = "Job " + job.JobReference + " completed!",
                totalCost = job.TotalCost
            });
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static object Summary(JobCard j)
        {
            return new
            {
                id = j.Id,
                jobReference = j.JobReference,
                title = j.Title,
                asset = j.Asset != null ? j.Asset.AssetName : j.ManualAssetName,
                priority = j.Priority,
                status = j.Status,
                due = j.DueDate.HasValue ? j.DueDate.Value.ToString("ddd dd MMM, HH:mm") : null,
                overdue = j.DueDate.HasValue && j.DueDate.Value < DateTime.Now && j.Status != "Completed",
                reported = j.DateCreated.ToString("ddd dd MMM, HH:mm"),
                completed = j.DateCompleted.HasValue ? j.DateCompleted.Value.ToString("ddd dd MMM, HH:mm") : null,
                finalCondition = j.FinalCondition,
                totalCost = j.TotalCost
            };
        }

        // The worker's own job (assigned to them)
        private JobCard OwnJob(int id)
        {
            int staffId = StaffId();
            return db.JobCards.Include(j => j.Asset).FirstOrDefault(j => j.Id == id && j.AssignedToId == staffId);
        }

        private string PhotoBase64(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var path = Server.MapPath("~/Content/JobPhotos/" + Path.GetFileName(fileName));
            return System.IO.File.Exists(path) ? Convert.ToBase64String(System.IO.File.ReadAllBytes(path)) : null;
        }

        // Same as WorkerController.GetStaffId (set at login)
        private int StaffId()
        {
            int id;
            return Session["MaintenanceStaffId"] != null && int.TryParse(Session["MaintenanceStaffId"].ToString(), out id) ? id : 0;
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

    public class CompleteJobInput
    {
        public string CompletionNotes { get; set; }
        public string FinalCondition { get; set; }
        public string PhotoAfterBase64 { get; set; }
        public List<PartInput> Parts { get; set; }
        public decimal LabourCost { get; set; }
    }

    public class PartInput
    {
        public int InventoryId { get; set; }
        public int Quantity { get; set; }
    }
}
