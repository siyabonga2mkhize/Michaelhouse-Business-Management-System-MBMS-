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

        public ActionResult Index(string search, bool archived = false)
        {
            var query = db.HouseMasters.Include(h => h.Residences).AsQueryable();
            query = query.Where(h => h.IsArchived == archived);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(h => h.FullName.Contains(search) || h.ContactEmail.Contains(search));
            ViewBag.Search = search;
            ViewBag.Archived = archived;
            return View(query.OrderBy(h => h.FullName).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            var houseMaster = db.HouseMasters.Include(h => h.Residences).FirstOrDefault(h => h.HouseMasterId == id);
            if (houseMaster == null) return HttpNotFound();
            return View(houseMaster);
        }

        [AdminOnly]
        public ActionResult Create()
        {
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name");
            return View();
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "FullName,ContactEmail,ContactPhone,ResidenceId")] HouseMaster model)
        {
            if (db.HouseMasters.Any(h => h.ContactEmail == model.ContactEmail && !h.IsArchived)) ModelState.AddModelError("ContactEmail", "A house master with this email already exists.");
            if (!ModelState.IsValid)
            {
                ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", model.ResidenceId);
                return View(model);
            }
            db.HouseMasters.Add(model);
            db.SaveChanges();
            if (model.ResidenceId.HasValue)
            {
                var residence = db.Residences.Find(model.ResidenceId.Value);
                if (residence != null) residence.HouseMasterId = model.HouseMasterId;
                db.SaveChanges();
            }
            TempData["Success"] = "House master created.";
            return RedirectToAction("Details", new { id = model.HouseMasterId });
        }

        [AdminOnly]
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            var houseMaster = db.HouseMasters.Find(id);
            if (houseMaster == null) return HttpNotFound();
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", houseMaster.ResidenceId);
            return View(houseMaster);
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "HouseMasterId,FullName,ContactEmail,ContactPhone,ResidenceId")] HouseMaster model)
        {
            var houseMaster = db.HouseMasters.Find(model.HouseMasterId);
            if (houseMaster == null) return HttpNotFound();
            if (db.HouseMasters.Any(h => h.HouseMasterId != model.HouseMasterId && h.ContactEmail == model.ContactEmail && !h.IsArchived)) ModelState.AddModelError("ContactEmail", "A house master with this email already exists.");
            if (!ModelState.IsValid)
            {
                ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", model.ResidenceId);
                return View(model);
            }
            houseMaster.FullName = model.FullName;
            houseMaster.ContactEmail = model.ContactEmail;
            houseMaster.ContactPhone = model.ContactPhone;
            houseMaster.ResidenceId = model.ResidenceId;
            if (model.ResidenceId.HasValue)
            {
                var residence = db.Residences.Find(model.ResidenceId.Value);
                if (residence != null) residence.HouseMasterId = model.HouseMasterId;
            }
            db.SaveChanges();
            TempData["Success"] = "House master updated.";
            return RedirectToAction("Details", new { id = houseMaster.HouseMasterId });
        }

        [AdminOnly]
        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            var houseMaster = db.HouseMasters.Find(id);
            if (houseMaster == null) return HttpNotFound();
            return View(houseMaster);
        }

        [AdminOnly]
        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(int id)
        {
            var houseMaster = db.HouseMasters.Find(id);
            if (houseMaster == null) return HttpNotFound();
            if (db.Residences.Any(r => r.HouseMasterId == id && !r.IsArchived))
            {
                TempData["Error"] = "Reassign this house master's active residences before archiving.";
                return RedirectToAction("Details", new { id });
            }
            houseMaster.IsArchived = true;
            db.SaveChanges();
            TempData["Success"] = "House master archived.";
            return RedirectToAction("Index");
        }

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var houseMaster = db.HouseMasters.Find(id);
            if (houseMaster == null) return HttpNotFound();
            houseMaster.IsArchived = false;
            db.SaveChanges();
            TempData["Success"] = "House master restored.";
            return RedirectToAction("Index", new { archived = true });
        }

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
                EnsureActionAllowed(studentId, "CheckIn");
                var assignment = GetLatestAssignment(studentId);
                if (assignment == null)
                    throw new InvalidOperationException("Student has no residence assignment.");

                assignment.IsActive = true;
                assignment.Status = "Inside";
                assignment.VacatedDate = null;
                var bed = db.Beds.Find(assignment.BedId);
                if (bed != null)
                {
                    bed.IsOccupied = true;
                    bed.Status = "Occupied";
                    bed.OccupiedByStudentId = studentId;
                }
                RecalculateOccupancy(assignment.RoomId, assignment.ResidenceId);
                LogResidenceAction(studentId, "CheckIn", "Student checked in by house master.", true);
                db.SaveChanges();
                TempData["Success"] = "Student checked in.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("ScanQRCode");
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
                EnsureActionAllowed(studentId, "CheckOut");
                var activeAssignment = GetLatestAssignment(studentId);

                if (activeAssignment == null)
                    throw new InvalidOperationException("Student has no active residence assignment.");

                returnResidenceId = activeAssignment.ResidenceId;

                activeAssignment.Status = "Outside";

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
            return RedirectToAction("ScanQRCode");
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
            var assignment = GetLatestAssignment(studentId);
            try
            {
                if (db.Students.Find(studentId) == null) return HttpNotFound();
                EnsureActionAllowed(studentId, action);
                if (assignment != null)
                {
                    if (action == "HolidayDeparture") assignment.Status = "OnHoliday";
                    if (action == "HolidayReturn") assignment.Status = "Inside";
                    if (action == "VisitorSignOut") assignment.Status = "WeekendLeave";
                    if (action == "VisitorReturn") assignment.Status = "Inside";
                }
                LogResidenceAction(studentId, action, notes, approved, assignment?.ResidenceId, isHoliday);
                db.SaveChanges();
                TempData["Success"] = action.Replace("Holiday", "Holiday ").Replace("Visitor", "Visitor ") + " recorded.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction("ScanQRCode");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SuspendStudent(int studentId, string suspensionReason, DateTime? expectedReturnDate)
        {
            try
            {
                var assignment = GetLatestAssignment(studentId);
                if (assignment == null)
                    throw new InvalidOperationException("Student has no residence assignment.");
                if (GetCurrentBoardingStatus(studentId, assignment) == "Archived")
                    throw new InvalidOperationException("Archived students cannot be suspended.");
                if (string.IsNullOrWhiteSpace(suspensionReason))
                    throw new InvalidOperationException("Suspension reason is required.");

                assignment.Status = "Suspended";
                var notes = $"Suspended. Reason: {suspensionReason}. Expected return: {expectedReturnDate?.ToString("g") ?? "Not captured"}.";
                LogResidenceAction(studentId, "SuspendStudent", notes, false, assignment.ResidenceId);
                db.SaveChanges();
                TempData["Success"] = "Student suspended.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction("ScanQRCode");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReinstateStudent(int studentId)
        {
            try
            {
                var assignment = GetLatestAssignment(studentId);
                if (assignment == null)
                    throw new InvalidOperationException("Student has no residence assignment.");
                if (GetCurrentBoardingStatus(studentId, assignment) == "Archived")
                    throw new InvalidOperationException("Archived students cannot be reinstated from this screen.");

                assignment.Status = "Inside";
                assignment.IsActive = true;
                assignment.VacatedDate = null;
                var bed = db.Beds.Find(assignment.BedId);
                if (bed != null)
                {
                    bed.IsOccupied = true;
                    bed.Status = "Occupied";
                    bed.OccupiedByStudentId = studentId;
                }
                RecalculateOccupancy(assignment.RoomId, assignment.ResidenceId);
                LogResidenceAction(studentId, "ReinstateStudent", "Student reinstated by house master.", true, assignment.ResidenceId);
                db.SaveChanges();
                TempData["Success"] = "Student reinstated.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction("ScanQRCode");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ScanQRCode(string qrCodeValue)
        {
            var value = (qrCodeValue ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                ViewBag.ErrorMessage = "Scan or enter a student QR code first.";
                return View("InvalidQRCode");
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
                ViewBag.ErrorMessage = "No active student QR identity was found for that scan.";
                return View("InvalidQRCode");
            }

            var assignment = db.ResidenceAssignments
                .Include(a => a.Residence)
                .Include(a => a.Residence.HouseMaster)
                .Include(a => a.Room)
                .Include(a => a.Bed)
                .OrderByDescending(a => a.MoveInDate)
                .FirstOrDefault(a => a.StudentId == qr.StudentId);

            ViewBag.ScannedStudent = qr.Student;
            ViewBag.ActiveAssignment = assignment;
            ViewBag.QRCode = qr;
            PopulateScanVerification(qr.Student.StudentId, assignment);

            if (assignment == null)
                TempData["Error"] = "This student has a valid QR identity but no active residence assignment.";

            return View();
        }

        private void LogResidenceAction(int studentId, string action, string notes, bool approved, int? residenceId = null, bool isHoliday = false)
        {
            var assignment = GetLatestAssignment(studentId);
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

        private ResidenceAssignment GetLatestAssignment(int studentId)
        {
            return db.ResidenceAssignments
                .Include(a => a.Room)
                .Include(a => a.Residence)
                .Include(a => a.Residence.HouseMaster)
                .Include(a => a.Bed)
                .OrderByDescending(a => a.MoveInDate)
                .FirstOrDefault(a => a.StudentId == studentId);
        }

        private void PopulateScanVerification(int studentId, ResidenceAssignment assignment)
        {
            var status = GetCurrentBoardingStatus(studentId, assignment);
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentStatusLabel = GetStatusLabel(status);
            ViewBag.LastCheckIn = GetLastActionTime(studentId, "CheckIn");
            ViewBag.LastCheckOut = GetLastActionTime(studentId, "CheckOut");
            ViewBag.HolidayDeparture = GetLastActionTime(studentId, "HolidayDeparture");
            ViewBag.SuspensionDetails = GetLastActionReason(studentId, "SuspendStudent");
            ViewBag.CanCheckIn = status == "Outside" || status == "WeekendLeave";
            ViewBag.CanCheckOut = status == "Inside";
            ViewBag.CanHolidayDeparture = status == "Inside" || status == "Outside" || status == "WeekendLeave";
            ViewBag.CanHolidayReturn = status == "OnHoliday";
            ViewBag.CanSuspend = status != "Suspended" && status != "Archived";
            ViewBag.CanReinstate = status == "Suspended";
            ViewBag.ActionsLocked = status == "Archived";
        }

        private string GetCurrentBoardingStatus(int studentId, ResidenceAssignment assignment)
        {
            if (assignment == null || assignment.IsArchived || assignment.Status == "Archived")
                return "Archived";

            if (assignment.Status == "Suspended")
                return "Suspended";

            if (assignment.Status == "OnHoliday")
                return "OnHoliday";

            if (assignment.Status == "WeekendLeave" || assignment.Status == "VisitorSignOut")
                return "WeekendLeave";

            if (assignment.Status == "Outside" || assignment.Status == "CheckedOut")
                return "Outside";

            var lastMovement = db.QRScanRecords
                .Where(q => q.StudentId == studentId && !q.IsArchived &&
                            (q.Action == "CheckIn" || q.Action == "CheckOut" || q.Action == "HolidayDeparture" || q.Action == "HolidayReturn" || q.Action == "VisitorSignOut" || q.Action == "VisitorReturn" || q.Action == "SuspendStudent" || q.Action == "ReinstateStudent"))
                .OrderByDescending(q => q.ScannedAt)
                .FirstOrDefault();

            if (lastMovement != null)
            {
                if (lastMovement.Action == "SuspendStudent") return "Suspended";
                if (lastMovement.Action == "HolidayDeparture") return "OnHoliday";
                if (lastMovement.Action == "VisitorSignOut") return "WeekendLeave";
                if (lastMovement.Action == "CheckOut") return "Outside";
            }

            return "Inside";
        }

        private string GetStatusLabel(string status)
        {
            switch (status)
            {
                case "Inside": return "Inside Residence";
                case "Outside": return "Outside Residence";
                case "OnHoliday": return "On Holiday";
                case "WeekendLeave": return "Weekend Leave";
                case "Suspended": return "Suspended";
                default: return "Archived";
            }
        }

        private DateTime? GetLastActionTime(int studentId, string action)
        {
            return db.QRScanRecords
                .Where(q => q.StudentId == studentId && q.Action == action && !q.IsArchived)
                .OrderByDescending(q => q.ScannedAt)
                .Select(q => (DateTime?)q.ScannedAt)
                .FirstOrDefault();
        }

        private string GetLastActionReason(int studentId, string action)
        {
            return db.QRScanRecords
                .Where(q => q.StudentId == studentId && q.Action == action && !q.IsArchived)
                .OrderByDescending(q => q.ScannedAt)
                .Select(q => q.Reason)
                .FirstOrDefault();
        }

        private void EnsureActionAllowed(int studentId, string action)
        {
            var assignment = GetLatestAssignment(studentId);
            var status = GetCurrentBoardingStatus(studentId, assignment);

            if (status == "Archived")
                throw new InvalidOperationException("No actions can be performed for an archived student assignment.");

            if (status == "Suspended" && action != "ReinstateStudent")
                throw new InvalidOperationException("Student is suspended. Only reinstatement is allowed.");

            if (status == "OnHoliday" && action != "HolidayReturn")
                throw new InvalidOperationException("Student is on holiday. Only holiday return is allowed.");

            if (action == "CheckIn" && status == "Inside")
                throw new InvalidOperationException("Student is already inside the residence.");

            if (action == "CheckOut" && status == "Outside")
                throw new InvalidOperationException("Student is already outside the residence.");

            if (action == "HolidayDeparture" && status == "OnHoliday")
                throw new InvalidOperationException("Student is already on holiday.");
        }

        private void RecalculateOccupancy(int roomId, int residenceId)
        {
            var room = db.Rooms.Find(roomId);
            if (room != null)
            {
                room.OccupiedBeds = db.ResidenceAssignments.Count(a => a.RoomId == roomId && a.IsActive && !a.IsArchived);
                room.IsFull = room.OccupiedBeds >= room.Capacity;
            }

            var residence = db.Residences.Find(residenceId);
            if (residence != null)
                residence.OccupiedBeds = db.ResidenceAssignments.Count(a => a.ResidenceId == residenceId && a.IsActive && !a.IsArchived);
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
