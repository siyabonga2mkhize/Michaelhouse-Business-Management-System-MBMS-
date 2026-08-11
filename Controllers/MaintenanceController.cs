using Michaelhouse.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    /// <summary>
    /// Handles everything the Maintenance Manager can see and do.
    /// Only users with Role == "MaintenanceManager" can access this controller.
    /// </summary>
    public class MaintenanceController : Controller
    {
        private DBContextClass db = new DBContextClass();

        private bool IsAuthorized()
        {
            return Session["UserId"] != null
                && Session["UserRole"] != null
                && Session["UserRole"].ToString() == "MaintenanceManager";
        }

        // ══════════════════════════════════════════════════════════════════════
        // DASHBOARD
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult Index()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            // ── Stats Cards ────────────────────────────────────────────────────
            ViewBag.TotalStaff = db.MaintenanceStaff.Count(s => s.IsActive);
            ViewBag.AvailableStaff = db.MaintenanceStaff.Count(s =>
                s.CurrentStatus == "Available" && s.IsActive);
            ViewBag.TotalAssets = db.Assets.Count(a => a.Status == "Active");
            ViewBag.OpenJobs = db.JobCards.Count(j =>
                j.Status != "Completed" && j.Status != "Cancelled");
            ViewBag.CompletedToday = db.JobCards.Count(j =>
                j.Status == "Completed"
                && j.DateCompleted.HasValue
                && DbFunctions.TruncateTime(j.DateCompleted)
                == DbFunctions.TruncateTime(DateTime.Now));
            ViewBag.EmergencyJobs = db.JobCards.Count(j =>
                j.Priority == "Emergency" && j.Status != "Completed");
            ViewBag.OverdueJobs = db.JobCards.Count(j =>
                j.Status != "Completed"
                && j.Status != "Cancelled"
                && j.DueDate.HasValue
                && j.DueDate.Value < DateTime.Now);
            ViewBag.LowStockCount = db.MaintenanceInventory.Count(i =>
                i.StockLevel <= i.MinimumStock);

            // ── Recent Jobs ──────────────────────────────────────────────────
            var recentJobs = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .OrderByDescending(j => j.DateCreated)
                .Take(8)
                .ToList();
            ViewBag.RecentJobs = recentJobs;

            // ── Building Health ──────────────────────────────────────────────
            var buildingNames = new[] {
                "Founders House", "East House", "West House", "Tatham House",
                "Farfield House", "Pascoe House", "Baines House",
                "Mackenzie House", "Ralfe House", "McCormick House"
            };

            var buildingHealth = buildingNames.Select(b =>
                new BuildingHealthModel
                {
                    Name = b,
                    OpenJobs = db.JobCards.Count(j =>
                        j.Asset.LocationBuilding == b
                        && j.Status != "Completed"
                        && j.Status != "Cancelled"),
                    TotalAssets = db.Assets.Count(a =>
                        a.LocationBuilding == b
                        && a.Status == "Active")
                }).ToList();

            ViewBag.BuildingHealth = buildingHealth;

            // ── Worker Stats ──────────────────────────────────────────────────
            var workerStats = db.MaintenanceStaff
                .Where(s => s.IsActive)
                .ToList()
                .Select(s => new WorkerStatModel
                {
                    Name = s.FullName,
                    Skill = s.SkillType,
                    Status = s.CurrentStatus,
                    CompletedMonth = db.JobCards.Count(j =>
                        j.AssignedToId == s.Id
                        && j.Status == "Completed"
                        && j.DateCompleted.HasValue
                        && j.DateCompleted.Value.Month == DateTime.Now.Month)
                })
                .OrderByDescending(s => s.CompletedMonth)
                .ToList();

            ViewBag.WorkerStats = workerStats;

            return View();
        }

        // ══════════════════════════════════════════════════════════════════════
        // STAFF MANAGEMENT
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult Staff(string search = "",
                                  string skill = "",
                                  string status = "")
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var query = db.MaintenanceStaff.Where(s => s.IsActive);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(s =>
                    s.FullName.Contains(search) ||
                    s.StaffNumber.Contains(search));

            if (!string.IsNullOrEmpty(skill))
                query = query.Where(s => s.SkillType == skill);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.CurrentStatus == status);

            ViewBag.Search = search;
            ViewBag.Skill = skill;
            ViewBag.Status = status;

            var staffList = query.OrderBy(s => s.SkillType)
                                 .ThenBy(s => s.FullName)
                                 .ToList();

            foreach (var s in staffList)
            {
                ViewData["completed_" + s.Id] = db.JobCards.Count(j =>
                    j.AssignedToId == s.Id && j.Status == "Completed");
                ViewData["active_" + s.Id] = db.JobCards.Count(j =>
                    j.AssignedToId == s.Id
                    && j.Status != "Completed"
                    && j.Status != "Cancelled");
            }

            return View(staffList);
        }

        public ActionResult AddStaff()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddStaff(MaintenanceStaff staff)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var count = db.MaintenanceStaff.Count();
                staff.StaffNumber = string.Format("MH-MAINT-{0:D3}", count + 1);
                staff.DateJoined = DateTime.Now;
                staff.CurrentStatus = "Available";
                staff.IsActive = true;

                var newUser = new AppUser
                {
                    Name = staff.FullName,
                    Email = staff.Email,
                    PasswordHash = AccountController.HashPassword("Worker@123"),
                    Role = "MaintenanceWorker"
                };
                db.Users.Add(newUser);
                db.SaveChanges();

                staff.UserId = newUser.UserId;

                db.MaintenanceStaff.Add(staff);
                db.SaveChanges();

                TempData["Success"] = staff.FullName
                    + " added. Login: " + staff.Email + " / Worker@123";
                return RedirectToAction("Staff");
            }
            return View(staff);
        }

        public ActionResult EditStaff(int? id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("Staff");
            var staff = db.MaintenanceStaff.Find(id);
            if (staff == null) return HttpNotFound();
            return View(staff);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditStaff(MaintenanceStaff staff)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                db.Entry(staff).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = staff.FullName + " has been updated.";
                return RedirectToAction("Staff");
            }
            return View(staff);
        }

        [HttpPost]
        public ActionResult UpdateStatus(int id, string status)
        {
            if (!IsAuthorized())
                return Json(new { success = false });

            var staff = db.MaintenanceStaff.Find(id);
            if (staff == null) return Json(new { success = false });

            staff.CurrentStatus = status;
            db.SaveChanges();
            return Json(new { success = true, newStatus = status });
        }

        [HttpPost]
        public ActionResult DeactivateStaff(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var staff = db.MaintenanceStaff.Find(id);
            if (staff == null) return HttpNotFound();

            staff.IsActive = false;
            db.SaveChanges();
            TempData["Success"] = staff.FullName + " has been deactivated.";
            return RedirectToAction("Staff");
        }

        // ══════════════════════════════════════════════════════════════════════
        // STAFF DETAILS
        // ══════════════════════════════════════════════════════════════════════
        public ActionResult StaffDetails(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var staff = db.MaintenanceStaff
                .Include(s => s.User)
                .Include(s => s.LeaveRequests)
                .Include(s => s.StaffShifts.Select(ss => ss.ShiftPattern))
                .FirstOrDefault(s => s.Id == id);
            if (staff == null) return HttpNotFound();

            var recentJobs = db.JobCards
                .Include(j => j.Asset)
                .Where(j => j.AssignedToId == id)
                .OrderByDescending(j => j.DateCreated)
                .Take(10)
                .ToList();

            var allJobs = db.JobCards.Where(j => j.AssignedToId == id).ToList();
            int totalCompleted = allJobs.Count(j => j.Status == "Completed");
            int onTime = allJobs.Count(j => j.Status == "Completed" && j.DateCompleted <= j.DueDate);
            int onTimeRate = totalCompleted > 0 ? (onTime * 100 / totalCompleted) : 0;
            double avgResponse = allJobs.Where(j => j.ResponseTimeMinutes.HasValue)
                .Average(j => j.ResponseTimeMinutes) ?? 0;

            staff.JobsCompletedThisMonth = allJobs.Count(j => j.Status == "Completed" && j.DateCompleted.HasValue && j.DateCompleted.Value.Month == DateTime.Now.Month);
            staff.AvgResponseTime = (int)avgResponse;
            staff.OnTimeCompletionRate = onTimeRate;

            var upcomingShifts = db.StaffShifts
                .Include(ss => ss.ShiftPattern)
                .Where(ss => ss.StaffId == id && ss.Date >= DateTime.Today)
                .OrderBy(ss => ss.Date)
                .Take(7)
                .ToList();

            var viewModel = new StaffDetailsViewModel
            {
                Staff = staff,
                RecentJobs = recentJobs,
                //LeaveRequests = staff.LeaveRequests.OrderByDescending(l => l.RequestedAt).ToList(),
                UpcomingShifts = upcomingShifts,
                TotalJobsCompleted = totalCompleted,
                AvgResponseTime = avgResponse,
                OnTimeRate = onTimeRate
            };

            return View(viewModel);
        }

        // ══════════════════════════════════════════════════════════════════════
        // LEAVE MANAGEMENT
        //// ══════════════════════════════════════════════════════════════════════
        //public ActionResult LeaveRequests()
        //{
        //    if (!IsAuthorized())
        //        return RedirectToAction("Login", "Account");

        //    var requests = db.LeaveRequests
        //        .Include(l => l.Staff)
        //        .Include(l => l.ApprovedBy)
        //        .OrderByDescending(l => l.RequestedAt)
        //        .ToList();
        //    return View(requests);
        //}

        [HttpPost]
        public ActionResult ApproveLeave(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false });

            var request = db.LeaveRequests.Find(id);
            if (request == null) return Json(new { success = false });

            request.Status = "Approved";
            //request.ApprovedByUserId = (int)Session["UserId"];
            //request.ApprovedAt = DateTime.Now;
            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult DenyLeave(int id)
        {
            if (!IsAuthorized())
                return Json(new { success = false });

            var request = db.LeaveRequests.Find(id);
            if (request == null) return Json(new { success = false });

            request.Status = "Denied";
            //request.ApprovedByUserId = (int)Session["UserId"];
            //request.ApprovedAt = DateTime.Now;
            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult UpdateStaffStatus(int id, string status)
        {
            if (!IsAuthorized())
                return Json(new { success = false });

            var staff = db.MaintenanceStaff.Find(id);
            if (staff == null) return Json(new { success = false });

            staff.CurrentStatus = status;
            db.SaveChanges();
            return Json(new { success = true, newStatus = status });
        }

        // ══════════════════════════════════════════════════════════════════════
        // ASSET MANAGEMENT
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult Assets(string search = "",
                                   string category = "",
                                   string condition = "")
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var query = db.Assets.Where(a => a.Status != "Decommissioned");

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a =>
                    a.AssetName.Contains(search) ||
                    a.LocationBuilding.Contains(search));

            if (!string.IsNullOrEmpty(category))
                query = query.Where(a => a.Category == category);

            if (!string.IsNullOrEmpty(condition))
                query = query.Where(a => a.ConditionRating == condition);

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Condition = condition;

            ViewBag.TotalAssets = db.Assets.Count(a => a.Status == "Active");
            ViewBag.PoorCondition = db.Assets.Count(a => a.ConditionRating == "Poor");
            ViewBag.UnderRepair = db.Assets.Count(a => a.Status == "Under Repair");
            ViewBag.HighRisk = db.Assets.Count(a =>
                a.FaultCount >= 3 && a.Status == "Active");

            return View(query.OrderBy(a => a.Category)
                             .ThenBy(a => a.AssetName)
                             .ToList());
        }

        public ActionResult RegisterAsset()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterAsset(Asset asset)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var count = db.Assets.Count();
                var assetCode = string.Format("MH-ASSET-{0:D4}", count + 1);

                asset.QrCode = assetCode;
                asset.DateRegistered = DateTime.Now;
                asset.Status = "Active";
                asset.FaultCount = 0;
                asset.HealthScore = asset.ConditionRating == "Good" ? 100 :
                                    asset.ConditionRating == "Fair" ? 65 : 30;

                db.Assets.Add(asset);
                db.SaveChanges();

                if (asset.ConditionRating == "Poor")
                    TempData["Warning"] = asset.AssetName
                        + " registered with POOR condition. "
                        + "Consider creating an inspection job card immediately.";
                else
                    TempData["Success"] = asset.AssetName
                        + " registered. Asset Code: " + assetCode;

                return RedirectToAction("AssetDetail", new { id = asset.Id });
            }
            return View(asset);
        }

        public ActionResult AssetDetail(int? id, bool fromScan = false)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("Assets");

            var asset = db.Assets.Find(id);
            if (asset == null) return HttpNotFound();

            var jobHistory = db.JobCards
                .Include(j => j.AssignedTo)
                .Where(j => j.AssetId == id)
                .OrderByDescending(j => j.DateCreated)
                .ToList();

            ViewBag.JobHistory = jobHistory;
            ViewBag.FromScan = fromScan;

            var completed = jobHistory.Where(j =>
                j.Status == "Completed" && j.ResponseTimeMinutes.HasValue)
                .ToList();
            ViewBag.AvgResponseTime = completed.Any()
                ? (int)completed.Average(j => j.ResponseTimeMinutes.Value) : 0;

            return View(asset);
        }

        public ActionResult AssetHistory(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var asset = db.Assets.Find(id);
            if (asset == null) return HttpNotFound();

            var jobs = db.JobCards
                .Include(j => j.AssignedTo)
                .Where(j => j.AssetId == id)
                .OrderByDescending(j => j.DateCreated)
                .ToList();

            ViewBag.Jobs = jobs;
            return View(asset);
        }

        public ActionResult EditAsset(int? id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("Assets");
            var asset = db.Assets.Find(id);
            if (asset == null) return HttpNotFound();
            return View(asset);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditAsset(Asset asset)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                asset.HealthScore = asset.ConditionRating == "Good" ? 100 :
                                    asset.ConditionRating == "Fair" ? 65 : 30;
                db.Entry(asset).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = asset.AssetName + " has been updated.";
                return RedirectToAction("AssetDetail", new { id = asset.Id });
            }
            return View(asset);
        }

        // ── QR CODE GENERATION (UPDATED) ─────────────────────────────────────
        public ActionResult QrCode(int? id)
        {
            if (id == null) return RedirectToAction("Assets");
            var asset = db.Assets.Find(id);
            if (asset == null) return HttpNotFound();

            // Build the absolute scan URL, e.g. https://yourdomain.com/Scan/Index/MH-ASSET-0001
            string scanUrl = Url.Action("Index", "Scan",
                new { id = asset.QrCode }, Request.Url.Scheme);

            using (var gen = new QRCoder.QRCodeGenerator())
            {
                var data = gen.CreateQrCode(scanUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
                var qr = new QRCoder.PngByteQRCode(data);
                var bytes = qr.GetGraphic(10);
                return File(bytes, "image/png");
            }
        }

        [HttpPost]
        public ActionResult DecommissionAsset(int? id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("Assets");

            var asset = db.Assets.Find(id);
            if (asset == null) return HttpNotFound();

            asset.Status = "Decommissioned";
            db.SaveChanges();
            TempData["Success"] = asset.AssetName + " has been decommissioned.";
            return RedirectToAction("Assets");
        }

        // ══════════════════════════════════════════════════════════════════════
        // JOB CARD MANAGEMENT
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult JobCards(string search = "",
                                     string priority = "",
                                     string status = "",
                                     string jobType = "")
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var query = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(j =>
                    j.Title.Contains(search) ||
                    j.JobReference.Contains(search));

            if (!string.IsNullOrEmpty(priority))
                query = query.Where(j => j.Priority == priority);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(j => j.Status == status);

            if (!string.IsNullOrEmpty(jobType))
                query = query.Where(j => j.JobType == jobType);

            ViewBag.Search = search;
            ViewBag.Priority = priority;
            ViewBag.Status = status;
            ViewBag.JobType = jobType;

            ViewBag.TotalOpen = db.JobCards.Count(j =>
                j.Status != "Completed" && j.Status != "Cancelled");
            ViewBag.InProgress = db.JobCards.Count(j =>
                j.Status == "In Progress" || j.Status == "Assigned");
            ViewBag.Emergency = db.JobCards.Count(j =>
                j.Priority == "Emergency" && j.Status != "Completed");
            ViewBag.CompletedMonth = db.JobCards.Count(j =>
                j.Status == "Completed"
                && j.DateCompleted.HasValue
                && j.DateCompleted.Value.Month == DateTime.Now.Month
                && j.DateCompleted.Value.Year == DateTime.Now.Year);

            return View(query.OrderByDescending(j => j.DateCreated).ToList());
        }

        public ActionResult CreateJobCard()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.AssetName).ToList();
            ViewBag.Staff = db.MaintenanceStaff
                .Where(s => s.IsActive && s.CurrentStatus == "Available")
                .OrderBy(s => s.FullName).ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateJobCard(JobCard jobCard)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var count = db.JobCards.Count();
                jobCard.JobReference = string.Format("MH-JOB-{0:D4}", count + 1);
                jobCard.DateCreated = DateTime.Now;
                jobCard.Status = "Pending";
                jobCard.ReportedById = (int)Session["UserId"];

                jobCard.DueDate = jobCard.Priority == "Emergency"
                    ? DateTime.Now.AddHours(1)
                    : jobCard.Priority == "High"
                    ? DateTime.Now.AddHours(8)
                    : jobCard.Priority == "Medium"
                    ? DateTime.Now.AddDays(3)
                    : DateTime.Now.AddDays(7);

                if (jobCard.AssignedToId.HasValue)
                {
                    jobCard.Status = "Assigned";
                    jobCard.DateAssigned = DateTime.Now;
                    var w = db.MaintenanceStaff.Find(jobCard.AssignedToId.Value);
                    if (w != null) w.CurrentStatus = "On Job";
                }

                var asset = db.Assets.Find(jobCard.AssetId);
                if (asset != null)
                {
                    asset.FaultCount++;
                    asset.Status = "Under Repair";
                    asset.HealthScore = Math.Max(0, asset.HealthScore - 10);
                }

                db.JobCards.Add(jobCard);
                db.SaveChanges();

                TempData["Success"] = "Job Card " + jobCard.JobReference
                    + " created successfully.";
                return RedirectToAction("JobCardDetail", new { id = jobCard.Id });
            }

            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.AssetName).ToList();
            ViewBag.Staff = db.MaintenanceStaff
                .Where(s => s.IsActive && s.CurrentStatus == "Available")
                .OrderBy(s => s.FullName).ToList();

            return View(jobCard);
        }

        public ActionResult JobCardDetail(int? id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("JobCards");

            var jobCard = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .FirstOrDefault(j => j.Id == id);

            if (jobCard == null) return HttpNotFound();

            ViewBag.PartsUsed = db.JobCardParts
                .Include(p => p.InventoryItem)
                .Where(p => p.JobCardId == id)
                .ToList();

            return View(jobCard);
        }

        public ActionResult CompleteJobCard(int? id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("JobCards");

            var jobCard = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .FirstOrDefault(j => j.Id == id);

            if (jobCard == null) return HttpNotFound();
            return View(jobCard);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteJobCard(
            int id,
            string completionNotes,
            string finalCondition,
            HttpPostedFileBase photoAfter,
            HttpPostedFileBase receipt,
            string photoData,
            int[] inventoryIds,
            int[] quantities,
            decimal labourCost,
            decimal partsCost,
            decimal totalCost)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var jobCard = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .FirstOrDefault(j => j.Id == id);

            if (jobCard == null)
                return HttpNotFound();

            // ── Save after photo ──────────────────────────────────────────────────
            string savedFileName = null;

            if (photoAfter != null && photoAfter.ContentLength > 0)
            {
                var ext = System.IO.Path.GetExtension(photoAfter.FileName);
                savedFileName = "after_" + jobCard.JobReference.Replace("-", "_")
                                + "_" + DateTime.Now.Ticks + ext;
                var savePath = Server.MapPath("~/Content/JobPhotos/");
                if (!System.IO.Directory.Exists(savePath))
                    System.IO.Directory.CreateDirectory(savePath);
                photoAfter.SaveAs(savePath + savedFileName);
            }
            else if (!string.IsNullOrEmpty(photoData) && photoData.StartsWith("data:image"))
            {
                try
                {
                    var base64 = photoData.Substring(photoData.IndexOf(',') + 1);
                    var bytes = Convert.FromBase64String(base64);
                    savedFileName = "after_" + jobCard.JobReference.Replace("-", "_")
                                    + "_" + DateTime.Now.Ticks + ".png";
                    var savePath = Server.MapPath("~/Content/JobPhotos/");
                    if (!System.IO.Directory.Exists(savePath))
                        System.IO.Directory.CreateDirectory(savePath);
                    System.IO.File.WriteAllBytes(savePath + savedFileName, bytes);
                }
                catch
                {
                    TempData["Warning"] = "Could not save the after photo.";
                }
            }

            if (!string.IsNullOrEmpty(savedFileName))
                jobCard.PhotoAfter = savedFileName;

            // ── Update fields ─────────────────────────────────────────────────────
            jobCard.CompletionNotes = completionNotes;
            jobCard.FinalCondition = finalCondition;
            jobCard.Status = "Completed";
            jobCard.DateCompleted = DateTime.Now;
            jobCard.ResponseTimeMinutes = (int)(DateTime.Now - jobCard.DateCreated).TotalMinutes;

            // ── Update worker ────────────────────────────────────────────────────
            if (jobCard.AssignedToId.HasValue)
            {
                var worker = db.MaintenanceStaff.Find(jobCard.AssignedToId.Value);
                if (worker != null)
                    worker.CurrentStatus = "Available";
            }

            // ── Update asset ─────────────────────────────────────────────────────
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
            }

            // ── Save cost fields ─────────────────────────────────────────────────
            jobCard.LabourCost = labourCost;
            jobCard.PartsCost = partsCost;
            jobCard.TotalCost = totalCost;

            // ── Save parts used ──────────────────────────────────────────────────
            if (inventoryIds != null && quantities != null)
            {
                for (int i = 0; i < inventoryIds.Length; i++)
                {
                    if (i >= quantities.Length || quantities[i] <= 0) continue;
                    var item = db.MaintenanceInventory.Find(inventoryIds[i]);
                    if (item == null) continue;
                    item.StockLevel = Math.Max(0, item.StockLevel - quantities[i]);

                    db.JobCardParts.Add(new JobCardPart
                    {
                        JobCardId = id,
                        InventoryItemId = inventoryIds[i],
                        QuantityUsed = quantities[i],
                        DateUsed = DateTime.Now
                    });
                }
            }

            db.SaveChanges();

            TempData["Success"] = "Job Card " + jobCard.JobReference
                                + " completed. Response time: "
                                + jobCard.ResponseTimeMinutes + " minutes.";

            return RedirectToAction("JobCardDetail", new { id = jobCard.Id });
        }

        [HttpPost]
        public ActionResult AutoAssign(int jobCardId)
        {
            if (!IsAuthorized())
                return Json(new { success = false });

            var jobCard = db.JobCards
                .Include(j => j.Asset)
                .FirstOrDefault(j => j.Id == jobCardId);

            if (jobCard == null)
                return Json(new
                {
                    success = false,
                    message = "Job card not found."
                });

            var skillNeeded = jobCard.Asset != null
                ? jobCard.Asset.Category : "General";

            if (skillNeeded == "Sports" || skillNeeded == "Building")
                skillNeeded = "General";

            var worker = db.MaintenanceStaff
                .Where(s => s.IsActive
                         && s.CurrentStatus == "Available"
                         && s.SkillType == skillNeeded)
                .OrderBy(s => s.FullName)
                .FirstOrDefault();

            if (worker == null)
                worker = db.MaintenanceStaff
                    .Where(s => s.IsActive
                             && s.CurrentStatus == "Available"
                             && s.SkillType == "General")
                    .FirstOrDefault();

            if (worker == null)
                worker = db.MaintenanceStaff
                    .Where(s => s.IsActive && s.CurrentStatus == "Available")
                    .FirstOrDefault();

            if (worker == null)
                return Json(new
                {
                    success = false,
                    message = "No available workers found. Please assign manually."
                });

            jobCard.AssignedToId = worker.Id;
            jobCard.Status = "Assigned";
            jobCard.DateAssigned = DateTime.Now;
            worker.CurrentStatus = "On Job";
            db.SaveChanges();

            return Json(new
            {
                success = true,
                workerName = worker.FullName,
                workerSkill = worker.SkillType,
                workerPhone = worker.Phone,
                message = worker.FullName + " ("
                            + worker.SkillType + ") has been assigned."
            });
        }

        public ActionResult ReportFault(int? assetId)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.AssetName).ToList();
            ViewBag.SelectedAssetId = assetId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReportFault(JobCard jobCard,
                                        HttpPostedFileBase photoBefore)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
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

                var count = db.JobCards.Count();
                jobCard.JobReference = string.Format("MH-JOB-{0:D4}", count + 1);
                jobCard.DateCreated = DateTime.Now;
                jobCard.JobType = "Reactive";
                jobCard.Status = "Pending";
                jobCard.ReportedById = (int)Session["UserId"];

                jobCard.DueDate = jobCard.Priority == "Emergency"
                    ? DateTime.Now.AddHours(1)
                    : jobCard.Priority == "High"
                    ? DateTime.Now.AddHours(8)
                    : jobCard.Priority == "Medium"
                    ? DateTime.Now.AddDays(3)
                    : DateTime.Now.AddDays(7);

                db.JobCards.Add(jobCard);

                var asset = db.Assets.Find(jobCard.AssetId);
                if (asset != null)
                {
                    asset.FaultCount++;
                    asset.Status = "Under Repair";
                    asset.HealthScore = Math.Max(0, asset.HealthScore - 10);
                }

                db.SaveChanges();

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

                    TempData["Success"] = "Fault reported. Job "
                        + jobCard.JobReference
                        + " assigned to " + worker.FullName + " automatically.";
                }
                else
                {
                    TempData["Warning"] = "Fault reported. Job "
                        + jobCard.JobReference
                        + " created but no available worker found.";
                }

                return RedirectToAction("JobCardDetail", new { id = jobCard.Id });
            }

            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.AssetName).ToList();
            return View(jobCard);
        }

        // ══════════════════════════════════════════════════════════════════════
        // INVENTORY MANAGEMENT
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult Inventory(string search = "",
                                      string category = "")
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var query = db.MaintenanceInventory.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(i => i.ItemName.Contains(search));

            if (!string.IsNullOrEmpty(category))
                query = query.Where(i => i.Category == category);

            ViewBag.Search = search;
            ViewBag.Category = category;

            ViewBag.TotalItems = db.MaintenanceInventory.Count();
            ViewBag.LowStock = db.MaintenanceInventory.Count(i =>
                i.StockLevel <= i.MinimumStock);
            ViewBag.TotalValue = db.MaintenanceInventory.ToList()
                .Sum(i => i.StockLevel * i.UnitCost);

            return View(query.OrderBy(i => i.Category)
                             .ThenBy(i => i.ItemName)
                             .ToList());
        }

        public ActionResult AddInventory()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddInventory(MaintenanceInventory item)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                item.LastRestocked = DateTime.Now;
                db.MaintenanceInventory.Add(item);
                db.SaveChanges();
                TempData["Success"] = item.ItemName + " added to inventory.";
                return RedirectToAction("Inventory");
            }
            return View(item);
        }

        [HttpPost]
        public ActionResult Restock(int id, int quantity)
        {
            if (!IsAuthorized())
                return Json(new { success = false });

            var item = db.MaintenanceInventory.Find(id);
            if (item == null) return Json(new { success = false });

            item.StockLevel += quantity;
            item.LastRestocked = DateTime.Now;
            db.SaveChanges();

            return Json(new
            {
                success = true,
                newStockLevel = item.StockLevel,
                isLowStock = item.IsLowStock
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // PREVENTIVE SCHEDULES
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult Schedules()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var schedules = db.PreventiveSchedules
                .Include(s => s.Asset)
                .OrderBy(s => s.NextDueDate)
                .ToList();

            foreach (var s in schedules)
            {
                if (s.Status != "Active") continue;

                if (s.NextDueDate < DateTime.Now)
                    s.ComplianceStatus = "Red";
                else if (s.NextDueDate < DateTime.Now.AddDays(7))
                    s.ComplianceStatus = "Yellow";
                else
                    s.ComplianceStatus = "Green";
            }

            ViewBag.TotalSchedules = schedules.Count(s => s.Status == "Active");
            ViewBag.DueSoon = schedules.Count(s =>
                s.Status == "Active" && s.NextDueDate < DateTime.Now.AddDays(7));
            ViewBag.Overdue = schedules.Count(s =>
                s.Status == "Active" && s.NextDueDate < DateTime.Now);
            ViewBag.Paused = schedules.Count(s => s.Status == "Paused");

            return View(schedules);
        }

        public ActionResult AddSchedule()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.LocationBuilding)
                .ThenBy(a => a.AssetName)
                .ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddSchedule(PreventiveSchedule schedule)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                schedule.NextDueDate = CalculateNextDueDate(
                    schedule.StartDate, schedule.Frequency);
                schedule.CreatedById = (int)Session["UserId"];
                schedule.CreatedAt = DateTime.Now;
                schedule.Status = "Active";
                schedule.ComplianceStatus = "Green";

                db.PreventiveSchedules.Add(schedule);
                db.SaveChanges();

                TempData["Success"] = "Schedule for "
                    + schedule.ScheduleName
                    + " created. Next due: "
                    + schedule.NextDueDate.ToString("dd MMM yyyy");

                return RedirectToAction("Schedules");
            }

            ViewBag.Assets = db.Assets
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.LocationBuilding)
                .ThenBy(a => a.AssetName)
                .ToList();

            return View(schedule);
        }

        [HttpPost]
        public ActionResult PauseSchedule(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var schedule = db.PreventiveSchedules.Find(id);
            if (schedule == null) return HttpNotFound();

            schedule.Status = "Paused";
            db.SaveChanges();
            TempData["Success"] = schedule.ScheduleName + " has been paused.";
            return RedirectToAction("Schedules");
        }

        [HttpPost]
        public ActionResult ResumeSchedule(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var schedule = db.PreventiveSchedules.Find(id);
            if (schedule == null) return HttpNotFound();

            schedule.Status = "Active";
            schedule.NextDueDate = CalculateNextDueDate(
                DateTime.Now, schedule.Frequency);
            db.SaveChanges();
            TempData["Success"] = schedule.ScheduleName
                + " resumed. Next due: "
                + schedule.NextDueDate.ToString("dd MMM yyyy");

            return RedirectToAction("Schedules");
        }

        [HttpPost]
        public ActionResult CancelSchedule(int id)
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var schedule = db.PreventiveSchedules.Find(id);
            if (schedule == null) return HttpNotFound();

            schedule.Status = "Cancelled";
            db.SaveChanges();
            TempData["Success"] = schedule.ScheduleName + " has been cancelled.";
            return RedirectToAction("Schedules");
        }

        [HttpPost]
        public ActionResult RunScheduledJobs()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var dueSchedules = db.PreventiveSchedules
                .Include(s => s.Asset)
                .Where(s => s.Status == "Active"
                         && s.NextDueDate <= DateTime.Now)
                .ToList();

            int jobsCreated = 0;

            foreach (var schedule in dueSchedules)
            {
                var count = db.JobCards.Count();
                var jobCard = new JobCard
                {
                    JobReference = string.Format("MH-JOB-{0:D4}", count + 1),
                    Title = "Scheduled: " + schedule.ScheduleName,
                    Description = schedule.TaskDescription,
                    AssetId = schedule.AssetId,
                    JobType = "Preventive",
                    Priority = "Medium",
                    Status = "Pending",
                    ReportedById = (int)Session["UserId"],
                    DateCreated = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(3)
                };

                db.JobCards.Add(jobCard);
                db.SaveChanges();

                var skillNeeded = schedule.SkillRequired;
                var worker = db.MaintenanceStaff
                    .Where(s => s.IsActive
                             && s.CurrentStatus == "Available"
                             && s.SkillType == skillNeeded)
                    .FirstOrDefault()
                    ?? db.MaintenanceStaff
                        .Where(s => s.IsActive
                                 && s.CurrentStatus == "Available")
                        .FirstOrDefault();

                if (worker != null)
                {
                    jobCard.AssignedToId = worker.Id;
                    jobCard.Status = "Assigned";
                    jobCard.DateAssigned = DateTime.Now;
                    worker.CurrentStatus = "On Job";
                }

                schedule.LastCompletedDate = DateTime.Now;
                schedule.NextDueDate = CalculateNextDueDate(
                    DateTime.Now, schedule.Frequency);
                schedule.ComplianceStatus = "Green";

                db.SaveChanges();
                jobsCreated++;
            }

            TempData["Success"] = jobsCreated + " scheduled job card(s) created "
                + "and assigned automatically.";

            return RedirectToAction("Schedules");
        }

        private DateTime CalculateNextDueDate(DateTime fromDate, string frequency)
        {
            switch (frequency)
            {
                case "Daily": return fromDate.AddDays(1);
                case "Weekly": return fromDate.AddDays(7);
                case "Fortnightly": return fromDate.AddDays(14);
                case "Monthly": return fromDate.AddMonths(1);
                case "Every3Months": return fromDate.AddMonths(3);
                case "Every6Months": return fromDate.AddMonths(6);
                case "Annually": return fromDate.AddYears(1);
                default: return fromDate.AddDays(14);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // PERFORMANCE DASHBOARD
        // ══════════════════════════════════════════════════════════════════════

        public ActionResult Performance()
        {
            if (!IsAuthorized())
                return RedirectToAction("Login", "Account");

            var completedThisMonth = db.JobCards
                .Where(j => j.Status == "Completed"
                         && j.DateCompleted.HasValue
                         && j.DateCompleted.Value.Month == DateTime.Now.Month
                         && j.ResponseTimeMinutes.HasValue)
                .ToList();

            ViewBag.AvgResponseTime = completedThisMonth.Any()
                ? (int)completedThisMonth.Average(j =>
                    j.ResponseTimeMinutes.Value) : 0;

            ViewBag.JobsThisMonth = db.JobCards.Count(j =>
                j.DateCreated.Month == DateTime.Now.Month
                && j.DateCreated.Year == DateTime.Now.Year);

            ViewBag.JobsLastMonth = db.JobCards.Count(j =>
                j.DateCreated.Month == DateTime.Now.AddMonths(-1).Month
                && j.DateCreated.Year == DateTime.Now.AddMonths(-1).Year);

            ViewBag.EmergencyCount = db.JobCards.Count(j =>
                j.Priority == "Emergency"
                && j.DateCreated.Month == DateTime.Now.Month);
            ViewBag.HighCount = db.JobCards.Count(j =>
                j.Priority == "High"
                && j.DateCreated.Month == DateTime.Now.Month);
            ViewBag.MediumCount = db.JobCards.Count(j =>
                j.Priority == "Medium"
                && j.DateCreated.Month == DateTime.Now.Month);
            ViewBag.LowCount = db.JobCards.Count(j =>
                j.Priority == "Low"
                && j.DateCreated.Month == DateTime.Now.Month);

            ViewBag.HighFaultAssets = db.Assets
                .Where(a => a.FaultCount > 0)
                .OrderByDescending(a => a.FaultCount)
                .Take(5)
                .ToList();

            var workerPerformance = db.MaintenanceStaff
                .Where(s => s.IsActive)
                .ToList()
                .Select(s => new WorkerPerformanceModel
                {
                    Name = s.FullName,
                    Skill = s.SkillType,
                    Completed = db.JobCards.Count(j =>
                        j.AssignedToId == s.Id
                        && j.Status == "Completed"
                        && j.DateCompleted.HasValue
                        && j.DateCompleted.Value.Month == DateTime.Now.Month),
                    AvgTime = db.JobCards
                        .Where(j => j.AssignedToId == s.Id
                                 && j.Status == "Completed"
                                 && j.ResponseTimeMinutes.HasValue)
                        .ToList()
                        .Any()
                        ? (int)db.JobCards
                            .Where(j => j.AssignedToId == s.Id
                                     && j.Status == "Completed"
                                     && j.ResponseTimeMinutes.HasValue)
                            .ToList()
                            .Average(j => j.ResponseTimeMinutes.Value)
                        : 0
                })
                .OrderByDescending(w => w.Completed)
                .ToList();

            ViewBag.WorkerPerformance = workerPerformance;

            ViewBag.OverdueJobs = db.JobCards
                .Include(j => j.Asset)
                .Include(j => j.AssignedTo)
                .Where(j => j.Status != "Completed"
                         && j.Status != "Cancelled"
                         && j.DueDate.HasValue
                         && j.DueDate.Value < DateTime.Now)
                .OrderBy(j => j.DueDate)
                .ToList();

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}