using Microsoft.AspNetCore.Authorization;
using Michaelhouse.Infrastructure;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "HouseMaster,Admin")]
    public class HouseMasterController : BaseController
    {
        private readonly DBContextClass db = DbContextFactory.Create();

        // Dashboard with cards and quick actions
        public ActionResult Dashboard()
        {
            var userId = (int?)(Session["UserId"]);
            var houseMaster = db.HouseMasters.FirstOrDefault(h => h.HouseMasterId == (int?)Session["HouseMasterId"]);

            // Basic stats
            var residencesQuery = db.Residences.Where(r => !r.IsArchived);
            if (houseMaster != null && !User.IsInRole("Admin"))
            {
                residencesQuery = residencesQuery.Where(r => r.HouseMasterId == houseMaster.HouseMasterId);
            }

            var residences = residencesQuery.ToList();
            var occupiedBeds = residences.Sum(r => r.OccupiedBeds);
            var availableBeds = residences.Sum(r => Math.Max(0, r.AvailableBeds));
            var residenceIds = residences.Select(rr => rr.ResidenceId).ToList();
            var activeStudentIds = db.ResidenceAssignments
                .Where(a => a.IsActive && residenceIds.Contains(a.ResidenceId))
                .Select(a => a.StudentId)
                .ToList();
            var studentsPresent = db.QRScanRecords.Count(q => q.Approved &&
                q.ScannedAt.Date == DateTime.UtcNow.Date &&
                activeStudentIds.Contains(q.StudentId));
            var studentsAway = activeStudentIds.Count - studentsPresent;
            var pendingAlerts = db.AIAlerts.Count(a => residenceIds.Contains(a.ResidenceId) && !a.IsResolved);
            var currentVisitors = db.Notifications.Count(n => n.Type == "Visitor" && n.ResidenceId.HasValue && residenceIds.Contains(n.ResidenceId.Value));

            ViewBag.OccupiedBeds = occupiedBeds;
            ViewBag.AvailableBeds = availableBeds;
            ViewBag.StudentsPresent = studentsPresent;
            ViewBag.StudentsAway = studentsAway;
            ViewBag.PendingAlerts = pendingAlerts;
            ViewBag.CurrentVisitors = currentVisitors;

            // AI recommendations for residences managed
            var recs = db.AIResidenceRecommendations
                .Where(r => residences.Select(rr => rr.ResidenceId).Contains(r.ResidenceId))
                .OrderByDescending(r => r.GeneratedAt).Take(5)
                .Include(r => r.Student).ToList();
            ViewBag.Recommendations = recs;

            return View();
        }

        // Residence summary
        public ActionResult ResidenceSummary(int id)
        {
            var res = db.Residences.Include(r => r.Rooms.Select(ro => ro.Beds)).Include(r => r.HouseMaster).FirstOrDefault(r => r.ResidenceId == id);
            if (res == null) return NotFound();
            return View(res);
        }

        // Current students in residence
        public ActionResult CurrentStudents(int residenceId)
        {
            var students = db.ResidenceAssignments.Where(a => a.ResidenceId == residenceId && a.IsActive)
                .Include(a => a.Student).Include(a => a.Room).Include(a => a.Bed).ToList();
            ViewBag.Residence = db.Residences.Find(residenceId)?.Name;
            return View(students);
        }

        // Students outside residence (e.g., on trip or not present today)
        public ActionResult StudentsOutsideResidence(int residenceId)
        {
            var today = DateTime.UtcNow.Date;
            var assignments = db.ResidenceAssignments.Where(a => a.ResidenceId == residenceId && a.IsActive)
                .Include(a => a.Student).ToList();

            var presentStudentIds = db.QRScanRecords.Where(q => q.ScannedAt.Date == today && q.Approved)
                .Select(q => q.StudentId).Distinct().ToList();

            var outside = assignments.Where(a => !presentStudentIds.Contains(a.StudentId)).ToList();
            ViewBag.Residence = db.Residences.Find(residenceId)?.Name;
            return View(outside);
        }

        // Recent movements
        public ActionResult RecentMovements(int residenceId)
        {
            var movements = db.ResidenceMovements.Where(m => m.FromResidenceId == residenceId || m.ToResidenceId == residenceId)
                .Include(m => m.Student).OrderByDescending(m => m.PerformedAt).Take(50).ToList();
            ViewBag.Residence = db.Residences.Find(residenceId)?.Name;
            return View(movements);
        }

        // Emergency roll call
        public ActionResult EmergencyRollCall(int residenceId)
        {
            var students = db.ResidenceAssignments.Where(a => a.ResidenceId == residenceId && a.IsActive)
                .Include(a => a.Student).ToList();
            ViewBag.Residence = db.Residences.Find(residenceId)?.Name;
            return View(students);
        }

        // Visitor register (simple model from Notifications table or separate Visitor model)
        public ActionResult VisitorRegister(int residenceId)
        {
            var visitors = db.Notifications.Where(n => n.Type == "Visitor" && n.ResidenceId == residenceId).OrderByDescending(n => n.CreatedAt).Take(100).ToList();
            ViewBag.Residence = db.Residences.Find(residenceId)?.Name;
            return View(visitors);
        }

        // Notifications / AI Alerts
        public ActionResult Notifications(int residenceId)
        {
            var alerts = db.AIAlerts.Where(a => a.ResidenceId == residenceId).OrderByDescending(a => a.CreatedAt).ToList();
            ViewBag.Residence = db.Residences.Find(residenceId)?.Name;
            return View(alerts);
        }

        // Quick actions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckIn(int studentId)
        {
            var student = db.Students.Find(studentId);
            if (student == null) return NotFound();
            try
            {
                LogResidenceAction(studentId, "CheckIn", "Student checked in by house master.", true);
                db.SaveChanges();
                TempData["Success"] = "Student checked in.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            var assignment = db.ResidenceAssignments.FirstOrDefault(a => a.StudentId == studentId && a.IsActive);
            return RedirectToAction("CurrentStudents", new { residenceId = assignment?.ResidenceId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckOut(int studentId)
        {
            var student = db.Students.Find(studentId);
            if (student == null) return NotFound();
            try
            {
                var activeAssignment = db.ResidenceAssignments
                    .Include(a => a.Room)
                    .Include(a => a.Residence)
                    .FirstOrDefault(a => a.StudentId == studentId && a.IsActive);

                if (activeAssignment == null)
                    throw new InvalidOperationException("Student has no active residence assignment.");

                var bed = db.Beds.Find(activeAssignment.BedId);
                if (bed != null)
                {
                    bed.IsOccupied = false;
                    bed.Status = "Available";
                    bed.OccupiedByStudentId = null;
                }

                activeAssignment.IsActive = false;
                activeAssignment.Status = "CheckedOut";
                activeAssignment.VacatedDate = DateTime.Now;

                activeAssignment.Room.OccupiedBeds = Math.Max(0, activeAssignment.Room.OccupiedBeds - 1);
                activeAssignment.Room.IsFull = false;
                activeAssignment.Residence.OccupiedBeds = Math.Max(0, activeAssignment.Residence.OccupiedBeds - 1);

                LogResidenceAction(studentId, "CheckOut", "Student checked out by house master.", false, activeAssignment.ResidenceId);
                db.SaveChanges();
                TempData["Success"] = "Student checked out.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            var assignment = db.ResidenceAssignments.FirstOrDefault(a => a.StudentId == studentId && a.IsActive);
            return RedirectToAction("CurrentStudents", new { residenceId = assignment?.ResidenceId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HolidayDeparture(int studentId)
        {
            return RecordStudentAction(studentId, "HolidayDeparture", "Student departed for holiday.", false, true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HolidayReturn(int studentId)
        {
            return RecordStudentAction(studentId, "HolidayReturn", "Student returned from holiday.", true, true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VisitorSignOut(int studentId, string destination, DateTime? expectedReturnTime, string reason)
        {
            var notes = $"Visitor sign out. Destination: {destination}. Expected return: {expectedReturnTime?.ToString("g") ?? "Not captured"}. Reason: {reason}";
            return RecordStudentAction(studentId, "VisitorSignOut", notes, false, false);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VisitorReturn(int studentId)
        {
            return RecordStudentAction(studentId, "VisitorReturn", "Student returned from visitor sign out.", true, false);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EmergencyRollCallScan(int studentId)
        {
            return RecordStudentAction(studentId, "EmergencyRollCall", "Student present for emergency roll call.", true, false);
        }

        private ActionResult RecordStudentAction(int studentId, string action, string notes, bool approved, bool isHoliday)
        {
            var assignment = db.ResidenceAssignments.FirstOrDefault(a => a.StudentId == studentId && a.IsActive);
            try
            {
                if (db.Students.Find(studentId) == null) return NotFound();
                LogResidenceAction(studentId, action, notes, approved, assignment?.ResidenceId, isHoliday);
                db.SaveChanges();
                TempData["Success"] = action.Replace("Holiday", "Holiday ").Replace("Visitor", "Visitor ") + " recorded.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction("CurrentStudents", new { residenceId = assignment?.ResidenceId ?? 0 });
        }

        private void LogResidenceAction(int studentId, string action, string notes, bool approved, int? residenceId = null, bool isHoliday = false)
        {
            var assignment = db.ResidenceAssignments.FirstOrDefault(a => a.StudentId == studentId && a.IsActive);
            db.QRScanRecords.Add(new QRScanRecord
            {
                StudentId = studentId,
                ResidenceId = residenceId ?? assignment?.ResidenceId,
                Action = action,
                ScannedAt = DateTime.UtcNow,
                HouseMasterId = (int?)Session["HouseMasterId"],
                Scanner = User?.Identity?.Name ?? "HouseMaster",
                Approved = approved,
                Reason = notes,
                IsHoliday = isHoliday
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TransferStudent(int studentId, int targetRoomId, string reason)
        {
            try
            {
                var userId = (int?)(Session["UserId"]) ?? 0;
                var userName = (string)(Session["UserName"]) ?? "HouseMaster";
                var service = new AIResidenceAllocationService();
                service.OverrideAllocation(studentId, targetRoomId, userId, userName, reason);
                TempData["Success"] = "Student transferred.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("Dashboard");
        }

        public ActionResult GenerateReport(int residenceId)
        {
            var students = db.ResidenceAssignments.Where(a => a.ResidenceId == residenceId && a.IsActive)
                .Include(a => a.Student).Include(a => a.Bed).Include(a => a.Room).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("Student,Room,Bed,Contact");
            foreach (var a in students)
            {
                sb.AppendLine($"{a.Student.Name},{a.Room.RoomNumber},{a.Bed.BedNumber},{a.Student.Parent?.Contact}");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "residence_report.csv");
        }

        // Scan QR Code view for housemaster
        public ActionResult ScanQRCode()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
