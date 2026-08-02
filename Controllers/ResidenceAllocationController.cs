using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web.Mvc;


namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Admin,HouseMaster")]
    public class ResidenceAllocationController : Controller
    {
        private ResidenceAllocationEngine engine = new ResidenceAllocationEngine();
        private AIResidencePredictionService _predictor = new AIResidencePredictionService();

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
        public ActionResult Confirm(int studentId, int roomId)
        {
            try
            {
                engine.AllocateStudent(studentId, roomId);
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
        public ActionResult AutoAllocate(int studentId)
        {
            try
            {
                engine.AllocateStudent(studentId);
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
                    .Select(r => new { r.ResidenceId, r.Name, r.OccupiedBeds, r.Capacity })
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
    }
}