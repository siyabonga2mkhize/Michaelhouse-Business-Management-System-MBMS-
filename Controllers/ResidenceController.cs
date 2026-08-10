using Michaelhouse.Models;
using Michaelhouse.Filters;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOrHouseMasterOnly]
    public class ResidenceController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private readonly AIResidencePredictionService _predictor = new AIResidencePredictionService();
        private readonly BoardingAccessService boardingAccess = new BoardingAccessService();

        // Dashboard: high-level stats and predictions
        public ActionResult Dashboard()
        {
            var residenceIds = boardingAccess.GetAccessibleResidenceIds(this, db);
            var residencesQuery = db.Residences.Where(r => !r.IsArchived && residenceIds.Contains(r.ResidenceId));
            var total = residencesQuery.Count();
            var totalAvailable = residencesQuery.ToList().Sum(r => r.AvailableBeds);
            var avgOccupancy = residencesQuery.Where(r => r.Capacity > 0).Select(r => (double)r.OccupiedBeds / r.Capacity).DefaultIfEmpty(0).Average();

            ViewBag.TotalResidences = total;
            ViewBag.TotalAvailableBeds = totalAvailable;
            ViewBag.AverageOccupancy = Math.Round(avgOccupancy * 100, 1) + "%";

            var preds = _predictor.PredictResidencesLikelyToBecomeFull(30)
                .Where(p => residenceIds.Contains(p.ResidenceId));
            ViewBag.Predictions = preds.Take(5).ToList();

            return View();
        }

        // GET: Residence
        public ActionResult Index(bool archived = false)
        {
            var query = db.Residences.AsQueryable();
            if (!archived) query = query.Where(r => !r.IsArchived);
            var residenceIds = boardingAccess.GetAccessibleResidenceIds(this, db);
            query = query.Where(r => residenceIds.Contains(r.ResidenceId));
            var list = query.Include(r => r.HouseMaster).Include(r => r.Rooms).ToList();
            ViewBag.Archived = archived;
            return View(list);
        }

        // GET: Details
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var res = db.Residences.Include(r => r.Rooms.Select(ro => ro.Beds)).Include(r => r.HouseMaster).FirstOrDefault(r => r.ResidenceId == id);
            if (res == null) return HttpNotFound();
            if (!boardingAccess.CanAccessResidence(this, db, res.ResidenceId)) return new HttpUnauthorizedResult();
            return View(res);
        }

        // GET: Create
        [AdminOnly]
        public ActionResult Create()
        {
            ViewBag.HouseMasters = new SelectList(db.HouseMasters.ToList(), "HouseMasterId", "FullName");
            return View();
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminOnly]
        public ActionResult Create([Bind(Include = "Name,Capacity,GradeCategory,NearMedicalFacility,NearHouseMasterOffice,HouseMasterId")] Residence model)
        {
            if (!ModelState.IsValid) { ViewBag.HouseMasters = new SelectList(db.HouseMasters.ToList(), "HouseMasterId", "FullName"); return View(model); }
            model.OccupiedBeds = 0;
            db.Residences.Add(model);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // GET: Edit
        [AdminOnly]
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var res = db.Residences.Find(id);
            if (res == null) return HttpNotFound();
            ViewBag.HouseMasters = new SelectList(db.HouseMasters.ToList(), "HouseMasterId", "FullName", res.HouseMasterId);
            return View(res);
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminOnly]
        public ActionResult Edit([Bind(Include = "ResidenceId,Name,Capacity,GradeCategory,NearMedicalFacility,NearHouseMasterOffice,HouseMasterId")] Residence model)
        {
            if (!ModelState.IsValid) { ViewBag.HouseMasters = new SelectList(db.HouseMasters.ToList(), "HouseMasterId", "FullName", model.HouseMasterId); return View(model); }
            var res = db.Residences.Find(model.ResidenceId);
            if (res == null) return HttpNotFound();
            res.Name = model.Name; res.Capacity = model.Capacity; res.GradeCategory = model.GradeCategory; res.NearMedicalFacility = model.NearMedicalFacility; res.NearHouseMasterOffice = model.NearHouseMasterOffice; res.HouseMasterId = model.HouseMasterId;
            db.SaveChanges();
            return RedirectToAction("Details", new { id = res.ResidenceId });
        }

        // GET: Archive
        [AdminOnly]
        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var res = db.Residences.Include(r => r.Rooms).FirstOrDefault(r => r.ResidenceId == id);
            if (res == null) return HttpNotFound();
            return View(res);
        }

        // POST: Archive
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminOnly]
        public ActionResult ArchiveConfirmed(int id, string reason)
        {
            var res = db.Residences.Include(r => r.Rooms.Select(ro => ro.Beds)).FirstOrDefault(r => r.ResidenceId == id);
            if (res == null) return HttpNotFound();
            if (res.Rooms.Any(ro => ro.OccupiedBeds > 0))
            {
                TempData["Error"] = "Cannot archive a residence that has rooms with occupied beds.";
                return RedirectToAction("Details", new { id = id });
            }
            res.IsArchived = true; res.ArchivedDate = DateTime.UtcNow; res.ArchivedBy = (string)Session["UserName"] ?? "system";
            db.SaveChanges();
            TempData["Success"] = "Residence archived successfully.";
            return RedirectToAction("Index");
        }

        // Occupancy view
        public ActionResult Occupancy(int id)
        {
            if (!boardingAccess.CanAccessResidence(this, db, id)) return new HttpUnauthorizedResult();
            var res = db.Residences.Include(r => r.Rooms.Select(ro => ro.Beds)).Include(r => r.Rooms.Select(ro => ro.Beds.Select(b => b.OccupiedByStudent))).FirstOrDefault(r => r.ResidenceId == id);
            if (res == null) return HttpNotFound();
            return View(res);
        }

        // View Rooms
        public ActionResult ViewRooms(int id)
        {
            if (!boardingAccess.CanAccessResidence(this, db, id)) return new HttpUnauthorizedResult();
            var rooms = db.Rooms.Where(r => r.ResidenceId == id && !r.IsArchived).Include(r => r.Beds).ToList();
            ViewBag.Residence = db.Residences.Find(id)?.Name;
            return View(rooms);
        }

        // View Beds
        public ActionResult ViewBeds(int roomId)
        {
            if (!boardingAccess.CanAccessRoom(this, db, roomId)) return new HttpUnauthorizedResult();
            var beds = db.Beds.Where(b => b.RoomId == roomId).Include(b => b.OccupiedByStudent).ToList();
            ViewBag.Room = db.Rooms.Find(roomId)?.RoomNumber;
            return View(beds);
        }

        // View Students
        public ActionResult ViewStudents(int id)
        {
            if (!boardingAccess.CanAccessResidence(this, db, id)) return new HttpUnauthorizedResult();
            var students = db.ResidenceAssignments.Where(a => a.ResidenceId == id && a.IsActive).Include(a => a.Student).Include(a => a.Room).Include(a => a.Bed).ToList();
            ViewBag.Residence = db.Residences.Find(id)?.Name;
            return View(students);
        }

        // Transfer student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TransferStudent(int studentId, int targetRoomId, string reason)
        {
            try
            {
                var assignment = db.ResidenceAssignments
                    .OrderByDescending(a => a.MoveInDate)
                    .FirstOrDefault(a => a.StudentId == studentId && a.IsActive && !a.IsArchived);
                var targetRoom = db.Rooms.Find(targetRoomId);
                if (assignment == null || targetRoom == null)
                    throw new InvalidOperationException("Student assignment or target room could not be found.");

                if (!boardingAccess.IsAdmin(this))
                {
                    if (!boardingAccess.CanAccessResidence(this, db, assignment.ResidenceId) ||
                        !boardingAccess.CanAccessResidence(this, db, targetRoom.ResidenceId) ||
                        assignment.ResidenceId != targetRoom.ResidenceId)
                        throw new InvalidOperationException("House Masters may only move students within their assigned residence. Inter-residence transfers require an administrator.");
                }

                var service = new AIResidenceAllocationService();
                var userId = (int)(Session["UserId"] ?? 0);
                var userName = (string)(Session["UserName"] ?? "system");
                service.OverrideAllocation(studentId, targetRoomId, userId, userName, reason);
                TempData["Success"] = "Student transferred successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Index");
        }

        // Generate occupancy report (CSV simple)
        public ActionResult GenerateOccupancyReport()
        {
            var residenceIds = boardingAccess.GetAccessibleResidenceIds(this, db);
            var data = db.Residences.Where(r => residenceIds.Contains(r.ResidenceId)).Include(r => r.Rooms).ToList();
            var csv = "Residence,Room,Capacity,Occupied\n";
            foreach (var r in data)
            {
                foreach (var room in r.Rooms)
                {
                    csv += $"{r.Name},{room.RoomNumber},{room.Capacity},{room.OccupiedBeds}\n";
                }
            }
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "occupancy.csv");
        }

        // AI Recommendations
        public ActionResult AIRecommendations(int id)
        {
            if (!boardingAccess.CanAccessResidence(this, db, id)) return new HttpUnauthorizedResult();
            var recs = db.AIResidenceRecommendations.Where(r => r.ResidenceId == id).Include(r => r.Student).OrderByDescending(r => r.GeneratedAt).ToList();
            ViewBag.ResidenceName = db.Residences.Find(id)?.Name;
            return View(recs);
        }

        // Emergency roll call
        public ActionResult EmergencyRollCall(int id)
        {
            if (!boardingAccess.CanAccessResidence(this, db, id)) return new HttpUnauthorizedResult();
            var students = db.ResidenceAssignments.Where(a => a.ResidenceId == id && a.IsActive).Include(a => a.Student).ToList();
            ViewBag.Residence = db.Residences.Find(id)?.Name;
            return View(students);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
