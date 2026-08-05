using Michaelhouse.Models;
using Michaelhouse.Filters;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOrHouseMasterOnly]
    public class HouseMasterController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        // Dashboard with cards and quick actions
        public ActionResult Dashboard()
        {
            if (Session == null)
            {
                throw new Exception("Session is null");
            }
            int? houseMasterId = Session["HouseMasterId"] as int?;

            var role = (Session["UserRole"] as string) ?? string.Empty;
            var isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

            HouseMaster houseMaster = null;

            if (houseMasterId.HasValue)
            {
                houseMaster = db.HouseMasters
                    .FirstOrDefault(h => h.HouseMasterId == houseMasterId.Value);
            }

            // Basic stats
            var residencesQuery = db.Residences.Where(r => !r.IsArchived);

            if (!isAdmin)
            {
                if (houseMaster == null)
                {
                    TempData["Error"] = "No house master profile is linked to this user account.";
                    residencesQuery = residencesQuery.Where(r => false);
                }
                else
                {
                    residencesQuery = residencesQuery.Where(r => r.HouseMasterId == houseMaster.HouseMasterId);
                }
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
                DbFunctions.TruncateTime(q.ScannedAt) == DbFunctions.TruncateTime(DateTime.UtcNow) &&
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
            ViewBag.ManagedCount = residences.Count;
            ViewBag.CurrentOccupancy = occupiedBeds;
            ViewBag.Pending = pendingAlerts;
            ViewBag.ManagedResidences = residences;

            // AI recommendations for residences managed
            var recs = db.AIResidenceRecommendations
     .Where(r => residenceIds.Contains(r.ResidenceId))
     .Include(r => r.Student)
     .OrderByDescending(r => r.GeneratedAt)
     .Take(5)
     .ToList();

            ViewBag.RecentAllocations = db.ResidenceAssignments
                .Where(a => residenceIds.Contains(a.ResidenceId))
                .Include(a => a.Student)
                .Include(a => a.Residence)
                .Include(a => a.Room)
                .OrderByDescending(a => a.MoveInDate)
                .Take(10)
                .ToList();

            return View();
        }

        // Residence summary
        public ActionResult ResidenceSummary(int id)
        {
            var res = db.Residences.Include(r => r.Rooms.Select(ro => ro.Beds)).Include(r => r.HouseMaster).FirstOrDefault(r => r.ResidenceId == id);
            if (res == null) return HttpNotFound();
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

            var presentStudentIds = db.QRScanRecords.Where(q => DbFunctions.TruncateTime(q.ScannedAt) == today && q.Approved)
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
            if (student == null) return HttpNotFound();
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
            if (student == null) return HttpNotFound();
            var returnResidenceId = 0;
            try
            {
                var activeAssignment = db.ResidenceAssignments
                    .Include(a => a.Room)
                    .Include(a => a.Residence)
                    .FirstOrDefault(a => a.StudentId == studentId && a.IsActive);

                if (activeAssignment == null)
                    throw new InvalidOperationException("Student has no active residence assignment.");

                returnResidenceId = activeAssignment.ResidenceId;

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
            if (returnResidenceId == 0)
            {
                var assignment = db.ResidenceAssignments.FirstOrDefault(a => a.StudentId == studentId && a.IsActive);
                returnResidenceId = assignment?.ResidenceId ?? 0;
            }
            return returnResidenceId > 0
                ? RedirectToAction("CurrentStudents", new { residenceId = returnResidenceId })
                : RedirectToAction("Dashboard");
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
                if (db.Students.Find(studentId) == null) return HttpNotFound();
                LogResidenceAction(studentId, action, notes, approved, assignment?.ResidenceId, isHoliday);
                db.SaveChanges();
                TempData["Success"] = action.Replace("Holiday", "Holiday ").Replace("Visitor", "Visitor ") + " recorded.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction("CurrentStudents", new { residenceId = assignment?.ResidenceId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ScanQRCode(string qrCodeValue)
        {
            var value = (qrCodeValue ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                TempData["Error"] = "Scan or enter a student QR code first.";
                return View();
            }

            var qr = db.StudentQRCodes
                .Include(q => q.Student)
                .FirstOrDefault(q => q.IsActive && q.QRCodeValue == value);

            if (qr == null && int.TryParse(value, out var studentId))
            {
                qr = db.StudentQRCodes
                    .Include(q => q.Student)
                    .FirstOrDefault(q => q.IsActive && q.StudentId == studentId);
            }

            if (qr == null || qr.Student == null)
            {
                TempData["Error"] = "No active student QR identity was found for that scan.";
                return View();
            }

            var assignment = db.ResidenceAssignments
                .Include(a => a.Residence)
                .Include(a => a.Room)
                .Include(a => a.Bed)
                .FirstOrDefault(a => a.StudentId == qr.StudentId && a.IsActive);

            ViewBag.ScannedStudent = qr.Student;
            ViewBag.ActiveAssignment = assignment;
            ViewBag.QRCode = qr;

            if (assignment == null)
                TempData["Error"] = "This student has a valid QR identity but no active residence assignment.";

            return View();
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
                Scanner = Session["UserName"] as string ?? "HouseMaster",
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
