using Michaelhouse.Models;
using Michaelhouse.Filters;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;


namespace Michaelhouse.Controllers
{
    [AdminOrHouseMasterOnly]
    public class ResidenceAllocationController : Controller
    {
        private ResidenceAllocationEngine engine = new ResidenceAllocationEngine();
        private AIResidencePredictionService _predictor = new AIResidencePredictionService();
        private DBContextClass db = new DBContextClass();

        public ActionResult Index(string search, bool archived = false)
        {
            var query = db.ResidenceAllocations.Include("Student").Include("Residence").Include("Room").Include("Bed").AsQueryable();
            query = query.Where(a => a.IsArchived == archived);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(a => a.Student.FirstName.Contains(search) || a.Student.LastName.Contains(search) || a.Residence.Name.Contains(search) || a.Room.RoomNumber.Contains(search));
            ViewBag.Search = search;
            ViewBag.Archived = archived;
            return View(query.OrderByDescending(a => a.AllocatedAt).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            var allocation = db.ResidenceAllocations.Include("Student").Include("Residence").Include("Room").Include("Bed").FirstOrDefault(a => a.ResidenceAllocationId == id);
            if (allocation == null) return HttpNotFound();
            return View(allocation);
        }

        [AdminOnly]
        public ActionResult Create()
        {
            PopulateAllocationLists();
            return View(new ResidenceAllocation { IsActive = true, AllocatedAt = DateTime.UtcNow });
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "StudentId,ResidenceId,RoomId,BedId,IsActive,AllocatedAt,ReleasedAt")] ResidenceAllocation model)
        {
            ValidateAllocation(model);
            if (!ModelState.IsValid)
            {
                PopulateAllocationLists(model);
                return View(model);
            }
            model.CreatedBy = Session["UserName"] as string ?? "system";
            model.CreatedAt = DateTime.UtcNow;
            db.ResidenceAllocations.Add(model);
            db.SaveChanges();
            TempData["Success"] = "Residence allocation record created.";
            return RedirectToAction("Details", new { id = model.ResidenceAllocationId });
        }

        [AdminOnly]
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            var allocation = db.ResidenceAllocations.Find(id);
            if (allocation == null) return HttpNotFound();
            PopulateAllocationLists(allocation);
            return View(allocation);
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ResidenceAllocationId,StudentId,ResidenceId,RoomId,BedId,IsActive,AllocatedAt,ReleasedAt")] ResidenceAllocation model)
        {
            var allocation = db.ResidenceAllocations.Find(model.ResidenceAllocationId);
            if (allocation == null) return HttpNotFound();
            ValidateAllocation(model);
            if (!ModelState.IsValid)
            {
                PopulateAllocationLists(model);
                return View(model);
            }
            allocation.StudentId = model.StudentId;
            allocation.ResidenceId = model.ResidenceId;
            allocation.RoomId = model.RoomId;
            allocation.BedId = model.BedId;
            allocation.IsActive = model.IsActive;
            allocation.AllocatedAt = model.AllocatedAt;
            allocation.ReleasedAt = model.ReleasedAt;
            allocation.UpdatedBy = Session["UserName"] as string ?? "system";
            allocation.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
            TempData["Success"] = "Residence allocation record updated.";
            return RedirectToAction("Details", new { id = allocation.ResidenceAllocationId });
        }

        [AdminOnly]
        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            var allocation = db.ResidenceAllocations.Include("Student").FirstOrDefault(a => a.ResidenceAllocationId == id);
            if (allocation == null) return HttpNotFound();
            return View(allocation);
        }

        [AdminOnly]
        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(int id)
        {
            var allocation = db.ResidenceAllocations.Find(id);
            if (allocation == null) return HttpNotFound();
            if (allocation.IsActive)
            {
                TempData["Error"] = "Release this allocation before archiving it.";
                return RedirectToAction("Details", new { id });
            }
            allocation.IsArchived = true;
            db.SaveChanges();
            TempData["Success"] = "Residence allocation archived.";
            return RedirectToAction("Index");
        }

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var allocation = db.ResidenceAllocations.Find(id);
            if (allocation == null) return HttpNotFound();
            allocation.IsArchived = false;
            db.SaveChanges();
            TempData["Success"] = "Residence allocation restored.";
            return RedirectToAction("Index", new { archived = true });
        }

        // GET: shows ranked room suggestions with score breakdowns
        public ActionResult Recommend(int studentId)
        {
            try
            {
                var recommendation = engine.GetRecommendations(studentId);
                return View(recommendation);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", "Students", new { id = studentId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm(int studentId, int roomId, int? bedId)
        {
            try
            {
                var service = new AIResidenceAllocationService();
                service.AllocateStudent(studentId, roomId, accepted: true, overrideBedId: bedId);
                TempData["Success"] = "Student successfully allocated to residence.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", "Students", new { id = studentId });
        }

        // Called automatically right after admission (no staff review)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AutoAllocate(int studentId)
        {
            try
            {
                var service = new AIResidenceAllocationService();
                service.AllocateStudent(studentId);
                TempData["Success"] = "Student automatically allocated to residence.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", "Students", new { id = studentId });
        }

        // Dashboard view to show predictions
        public ActionResult Dashboard()
        {
            var preds = _predictor.PredictResidencesLikelyToBecomeFull(30);
            var roomPreds = _predictor.PredictRoomsLikelyToBecomeEmpty(30);
            var recs = _predictor.GenerateRecommendations(30);

            ViewBag.ResidencePredictions = preds;
            ViewBag.RoomPredictions = roomPreds;
            ViewBag.PredictionRecommendations = recs;

            return View();
        }

        // GET: AI Dashboard
        public ActionResult AIDashboard()
        {
            using (var db = new DBContextClass())
            {
                // Total AI recommendations
                var totalRecommendations = db.AIResidenceRecommendations.Count();

                // Average confidence
                var avgConfidence = db.AIResidenceRecommendations.Any()
                    ? db.AIResidenceRecommendations.Average(r => r.ConfidenceScore)
                    : 0.0;

                // Most suitable residence (highest average compatibility across recommendations)
                var mostSuitable = db.AIResidenceRecommendations
                    .GroupBy(r => r.ResidenceId)
                    .Select(g => new { ResidenceId = g.Key, AvgScore = g.Average(x => x.CompatibilityScore) })
                    .OrderByDescending(x => x.AvgScore)
                    .FirstOrDefault();

                var mostSuitableResidence = mostSuitable != null ? db.Residences.Find(mostSuitable.ResidenceId)?.Name : "N/A";

                // Residences nearing capacity (>85%)
                var nearing = db.Residences
                    .Where(r => r.Capacity > 0 && (r.OccupiedBeds / (double)r.Capacity) >= 0.85)
                    .ToList();

                // Students requiring manual review (waiting list)
                var waiting = db.AIWaitingLists
                    .Include("Student")
                    .Where(w => !w.NotifiedAdmissions)
                    .OrderByDescending(w => w.RequestedAt)
                    .ToList();

                // Allocation overrides
                var overrides = db.AIAllocationHistories.Count(h => h.Overridden);

                // Prediction accuracy: percent of recommendations that matched final allocation (non-overridden)
                var recs = db.AIResidenceRecommendations.OrderByDescending(r => r.GeneratedAt).Take(200).ToList();
                int matches = 0; int checkedCount = 0;
                foreach (var rec in recs)
                {
                    var alloc = db.AIAllocationHistories
                        .Where(h => h.StudentId == rec.StudentId && h.Overridden == false)
                        .OrderBy(h => h.CreatedDate)
                        .FirstOrDefault();
                    if (alloc != null)
                    {
                        checkedCount++;
                        if (alloc.RoomId == rec.RoomId) matches++;
                    }
                }
                double predictionAccuracy = checkedCount > 0 ? (matches / (double)checkedCount) : 0.0;

                // Recent AI decisions
                var recentDecisions = db.AIAllocationHistories
                    .OrderByDescending(h => h.CreatedDate)
                    .Take(10)
                    .ToList();

                ViewBag.TotalRecommendations = totalRecommendations;
                ViewBag.AvgConfidence = avgConfidence;
                ViewBag.MostSuitableResidence = mostSuitableResidence;
                ViewBag.ResidencesNearing = nearing;
                ViewBag.WaitingList = waiting;
                ViewBag.OverridesCount = overrides;
                ViewBag.PredictionAccuracy = predictionAccuracy;
                ViewBag.RecentDecisions = recentDecisions;

                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OverrideRecommendation(int recommendationId, int newRoomId, string reason)
        {
            using (var db = new DBContextClass())
            {
                var rec = db.AIResidenceRecommendations.Find(recommendationId);
                if (rec == null) return HttpNotFound();

                var adminId = (int)(Session["UserId"] ?? 0);
                var adminName = Session["UserName"] as string ?? "Admin";

                var service = new AIResidenceAllocationService();
                try
                {
                    // Perform override
                    service.OverrideAllocation(rec.StudentId, newRoomId, adminId, adminName, reason);
                    TempData["Success"] = "Recommendation overridden and student moved.";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = ex.Message;
                }

                return RedirectToAction("AIDashboard");
            }
        }

        private void PopulateAllocationLists(ResidenceAllocation selected = null)
        {
            ViewBag.Students = new SelectList(db.Students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList(), "StudentId", "Name", selected?.StudentId);
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", selected?.ResidenceId);
            ViewBag.Rooms = new SelectList(db.Rooms.Where(r => !r.IsArchived && !r.Residence.IsArchived).OrderBy(r => r.RoomNumber).ToList(), "RoomId", "RoomNumber", selected?.RoomId);
            ViewBag.Beds = new SelectList(db.Beds.Where(b => !b.IsArchived).OrderBy(b => b.BedNumber).ToList(), "BedId", "BedNumber", selected?.BedId);
        }

        private void ValidateAllocation(ResidenceAllocation model)
        {
            if (db.Beds.Any(b => b.BedId == model.BedId && (b.IsArchived || b.IsOccupied))) ModelState.AddModelError("BedId", "Bed is not available.");
            if (db.Rooms.Any(r => r.RoomId == model.RoomId && (r.IsArchived || r.Residence.IsArchived))) ModelState.AddModelError("RoomId", "Room is not available.");
            if (db.ResidenceAllocations.Any(a => a.ResidenceAllocationId != model.ResidenceAllocationId && a.BedId == model.BedId && a.IsActive && !a.IsArchived)) ModelState.AddModelError("BedId", "This bed already has an active allocation.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
