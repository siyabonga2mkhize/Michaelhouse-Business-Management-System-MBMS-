using Microsoft.AspNetCore.Authorization;
using Michaelhouse.Infrastructure;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Admin,HouseMaster")]
    public class AIController : BaseController
    {
        private readonly DBContextClass db = DbContextFactory.Create();

        // List recent AI recommendations with search/filter
        public ActionResult Index(string q = null, int? residenceId = null)
        {
            var query = db.AIResidenceRecommendations.Include(r => r.Student).Include(r => r.Residence).Include(r => r.Room).AsQueryable();
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(r => r.Student.Name.Contains(q) || r.Explanation.Contains(q));
                ViewBag.Query = q;
            }
            if (residenceId.HasValue)
            {
                query = query.Where(r => r.ResidenceId == residenceId.Value);
                ViewBag.ResidenceId = residenceId.Value;
            }

            var list = query.OrderByDescending(r => r.GeneratedAt).Take(200).ToList();
            ViewBag.Residences = db.Residences.Where(r=>!r.IsArchived).ToList();
            return View(list);
        }

        // Show full recommendation details
        public ActionResult Details(int id)
        {
            var rec = db.AIResidenceRecommendations.Include(r => r.Student).Include(r => r.Residence).Include(r => r.Room).FirstOrDefault(r => r.RecommendationId == id);
            if (rec == null) return NotFound();

            // Additional alternatives: other recent recommendations for the same student
            var alternatives = db.AIResidenceRecommendations.Include(r=>r.Residence).Include(r=>r.Room)
                .Where(r => r.StudentId == rec.StudentId && r.RecommendationId != rec.RecommendationId)
                .OrderByDescending(r => r.GeneratedAt).Take(5).ToList();

            ViewBag.Alternatives = alternatives;

            // Pull allocation factors from RoomScoreAudit for the room/student combination
            var factors = db.RoomScoreAudits.Where(a => a.RoomId == rec.RoomId && a.StudentId == rec.StudentId)
                .OrderByDescending(a => a.CalculatedAt).Take(1).FirstOrDefault();
            ViewBag.Factors = factors;

            // Recent AI allocation history for student
            ViewBag.History = db.AIAllocationHistories.Where(h => h.StudentId == rec.StudentId).OrderByDescending(h => h.CreatedDate).Take(20).ToList();

            return View(rec);
        }

        // AI-powered residence dashboard
        public ActionResult Dashboard()
        {
            var model = new AIViewModel();

            model.TotalResidences = db.Residences.Count(r => !r.IsArchived);
            model.TotalRooms = db.Rooms.Count(r => !r.IsArchived);
            model.TotalBeds = db.Beds.Count();
            model.OccupiedBeds = db.Beds.Count(b => b.IsOccupied);
            model.AvailableBeds = model.TotalBeds - model.OccupiedBeds;
            model.StudentsPresent = db.QRScanRecords.Count(q => q.Approved && q.ScannedAt.Date == DateTime.UtcNow.Date);
            model.StudentsAway = db.ResidenceAssignments.Count(a => a.IsActive) - model.StudentsPresent;
            model.HouseMasters = db.HouseMasters.Count();
            model.Alerts = db.AIAlerts.Count(a => !a.IsResolved);

            // Allocation accuracy sample
            var recs = db.AIResidenceRecommendations.OrderByDescending(r => r.GeneratedAt).Take(200).ToList();
            int matches = 0, checkedCount = 0;
            foreach (var r in recs)
            {
                var alloc = db.AIAllocationHistories.Where(h => h.StudentId == r.StudentId && !h.Overridden).OrderBy(h => h.CreatedDate).FirstOrDefault();
                if (alloc != null) { checkedCount++; if (alloc.RoomId == r.RoomId) matches++; }
            }
            model.AllocationAccuracy = checkedCount > 0 ? Math.Round(matches / (double)checkedCount * 100, 1) : 0;

            // Recent allocations, movements, transfers
            model.RecentAllocations = db.AIAllocationHistories.Include(h => h.Student).OrderByDescending(h => h.CreatedDate).Take(10).ToList();
            model.RecentMovements = db.ResidenceMovements.Include(m => m.Student).OrderByDescending(m => m.PerformedAt).Take(10).ToList();

            // Predicted occupancy per residence (use prediction service)
            var predictor = new AIResidencePredictionService(db, new ResidenceAvailabilityService(db));
            model.Predicted = predictor.PredictResidencesLikelyToBecomeFull(30).Take(10).ToList();

            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class AIViewModel
    {
        public int TotalResidences { get; set; }
        public int TotalRooms { get; set; }
        public int TotalBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int AvailableBeds { get; set; }
        public int StudentsPresent { get; set; }
        public int StudentsAway { get; set; }
        public int HouseMasters { get; set; }
        public int Alerts { get; set; }
        public double AllocationAccuracy { get; set; }
        public System.Collections.Generic.List<AIAllocationHistory> RecentAllocations { get; set; }
        public System.Collections.Generic.List<ResidenceMovement> RecentMovements { get; set; }
        public System.Collections.Generic.List<Michaelhouse.Services.AIResidencePredictionService.ResidencePrediction> Predicted { get; set; }
    }
}
