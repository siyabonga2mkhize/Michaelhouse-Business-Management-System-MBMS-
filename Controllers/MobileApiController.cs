using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;

namespace Michaelhouse.Controllers
{
    [RoutePrefix("api/mobile")]
    public class MobileApiController : ApiController
    {
        private readonly DBContextClass db = new DBContextClass();
        private readonly MobileApiTokenService tokens = new MobileApiTokenService();

        [AllowAnonymous, HttpPost, Route("auth/login")]
        public IHttpActionResult Login(LoginRequest input)
        {
            if (input == null || String.IsNullOrWhiteSpace(input.Email) || String.IsNullOrWhiteSpace(input.Password)) return BadRequest("Email and password are required.");
            var passwordHash = Hash(input.Password);
            var user = db.Users.FirstOrDefault(x =>
                x.Email == input.Email && x.PasswordHash == passwordHash);

            if (user == null)
            {
                return ResponseMessage(Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    new { message = "Invalid email or password." }));
            }
            return Ok(User(user, tokens.Create(user.UserId, user.Role)));
        }
        [HttpGet, Route("auth/me")]
        public IHttpActionResult Me() { AppUser u; return Current(out u) ? Ok(User(u, null)) : (IHttpActionResult)Unauthorized(); }

        [HttpGet, Route("students")]
        public IHttpActionResult Students(string search = null)
        {
            if (!Staff()) return Unauthorized();
            var q = db.Students.Include(x => x.Parent).AsQueryable();
            if (!String.IsNullOrWhiteSpace(search)) q = q.Where(x => x.FirstName.Contains(search) || x.LastName.Contains(search) || x.StudentNumber.Contains(search));
            return Ok(q.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(100).ToList().Select(Student));
        }
        [HttpGet, Route("students/{id:int}")]
        public IHttpActionResult StudentById(int id)
        {
            AppUser u; if (!Current(out u) || (!IsStaff(u) && !db.Students.Any(x => x.StudentId == id && x.UserId == u.UserId))) return Unauthorized();
            var s = db.Students.Include(x => x.Parent).FirstOrDefault(x => x.StudentId == id); return s == null ? (IHttpActionResult)NotFound() : Ok(Student(s));
        }
        [HttpGet, Route("residences")]
        public IHttpActionResult Residences() { if (!Staff()) return Unauthorized(); return Ok(db.Residences.Where(x => !x.IsArchived).OrderBy(x => x.Name).ToList().Select(Residence)); }
        [HttpGet, Route("residences/{id:int}")]
        public IHttpActionResult ResidenceById(int id)
        {
            if (!Staff()) return Unauthorized();
            var r = db.Residences.Include(x => x.Rooms.Select(y => y.Beds)).FirstOrDefault(x => x.ResidenceId == id && !x.IsArchived);
            return r == null ? (IHttpActionResult)NotFound() : Ok(new { residence = Residence(r), rooms = r.Rooms.Where(x => !x.IsArchived).OrderBy(x => x.RoomNumber).Select(Room) });
        }
        [HttpGet, Route("students/{studentId:int}/assignment")]
        public IHttpActionResult Assignment(int studentId)
        {
            AppUser u; if (!Current(out u) || (!IsStaff(u) && !db.Students.Any(x => x.StudentId == studentId && x.UserId == u.UserId))) return Unauthorized();
            var a = db.ResidenceAssignments.Include(x => x.Residence).Include(x => x.Room).Include(x => x.Bed).FirstOrDefault(x => x.StudentId == studentId && x.IsActive);
            return Ok(a == null ? null : AssignmentDto(a));
        }
        [HttpPost, Route("residence/move")]
        public IHttpActionResult Move(MoveRequest input)
        {
            AppUser u; if (!Current(out u) || !CanManage(u)) return Unauthorized();
            if (input == null || input.StudentId < 1 || input.TargetRoomId < 1) return BadRequest("StudentId and TargetRoomId are required.");
            try { new AIResidenceAllocationService().OverrideAllocation(input.StudentId, input.TargetRoomId, u.UserId, u.Name, input.Reason); return Ok(new { success = true }); } catch (InvalidOperationException e) { return BadRequest(e.Message); }
        }
        [HttpPost, Route("residence/allocation/{studentId:int}")]
        public IHttpActionResult Allocate(int studentId, AllocationRequest input = null)
        {
            AppUser u; if (!Current(out u) || !CanManage(u)) return Unauthorized();
            try { var a = new AIResidenceAllocationService().AllocateStudent(studentId, input == null ? (int?)null : input.RoomId, true, input != null && input.RoomId.HasValue, input == null ? null : input.Reason); return Ok(new { assignmentId = a.ResidenceAssignmentId, studentId = a.StudentId }); } catch (InvalidOperationException e) { return BadRequest(e.Message); }
        }
        [HttpGet, Route("residence/allocation/{studentId:int}/recommendation")]
        public IHttpActionResult Recommendation(int studentId)
        {
            if (!Staff()) return Unauthorized();
            try { var r = new AIResidenceAllocationService().GenerateRecommendation(studentId); return Ok(new { best = RecommendationDto(r.Best), ranked = r.Ranked.Select(RecommendationDto) }); } catch (InvalidOperationException e) { return BadRequest(e.Message); }
        }
        [HttpGet, Route("students/{studentId:int}/qr")]
        public IHttpActionResult Qr(int studentId)
        {
            AppUser u; if (!Current(out u) || (!IsStaff(u) && !db.Students.Any(x => x.StudentId == studentId && x.UserId == u.UserId))) return Unauthorized();
            var service = new StudentQRCodeService(); var code = service.GetActiveQRCode(studentId) ?? service.GenerateQRCode(studentId);
            return Ok(new { studentId = studentId, value = code.QRCodeValue, imageBase64 = Convert.ToBase64String(code.QRImage ?? new byte[0]) });
        }
        [HttpPost, Route("qr/scan")]
        public IHttpActionResult Scan(ScanRequest input)
        {
            if (!Staff()) return Unauthorized(); if (input == null || String.IsNullOrWhiteSpace(input.QrValue)) return BadRequest("QrValue is required.");
            var r = new AIQRCodeVerificationService().VerifyByValue(input.QrValue, "Mobile"); return Ok(new { approved = r.Approved, reason = r.Reason });
        }

        // Student companion endpoints.  They expose the same read-only data used by
        // the existing student web pages and always derive the student from the token.
        [HttpGet, Route("student/profile")]
        public IHttpActionResult StudentProfile()
        {
            AppUser u; Student s;
            if (!CurrentStudent(out u, out s)) return Unauthorized();
            return Ok(Student(s));
        }

        [HttpGet, Route("student/timetable")]
        public IHttpActionResult StudentTimetable(int? year = null)
        {
            AppUser u; Student s;
            if (!CurrentStudent(out u, out s)) return Unauthorized();
            var slots = new TimetableGenerator().GetTimetableForStudent(s.StudentId, year ?? DateTime.Now.Year);
            return Ok(slots.OrderBy(x => x.DayOfWeek).ThenBy(x => x.Period.PeriodNumber).Select(x => new {
                dayOfWeek = x.DayOfWeek, period = x.Period.Label, startTime = x.Period.StartTime.ToString(),
                endTime = x.Period.EndTime.ToString(), subject = x.Subject.Name, teacher = x.Teacher.Name
            }));
        }

        [HttpGet, Route("student/results")]
        public IHttpActionResult StudentResults(int? year = null)
        {
            AppUser u; Student s;
            if (!CurrentStudent(out u, out s)) return Unauthorized();
            int academicYear = year ?? DateTime.Now.Year;
            return Ok(db.TermResults.Include(x => x.Subject).Where(x => x.StudentId == s.StudentId && x.AcademicYear == academicYear)
                .OrderBy(x => x.Term).ThenBy(x => x.Subject.Name).ToList()
                .Select(x => new { term = x.Term, subject = x.Subject.Name, percentage = x.TermMarkPercent, symbol = x.Symbol, passed = x.Passed }));
        }

        [HttpGet, Route("student/attendance")]
        public IHttpActionResult StudentAttendance()
        {
            AppUser u; Student s;
            if (!CurrentStudent(out u, out s)) return Unauthorized();
            var records = db.Attendances.Include(x => x.Subject).Where(x => x.StudentId == s.StudentId).OrderByDescending(x => x.Date).ToList();
            var summary = records.GroupBy(x => new { x.SubjectId, x.Subject.Name }).OrderBy(x => x.Key.Name).Select(g => new {
                subject = g.Key.Name, totalClasses = g.Count(), present = g.Count(x => x.Status == AttendanceStatus.Present),
                late = g.Count(x => x.Status == AttendanceStatus.Late), absent = g.Count(x => x.Status == AttendanceStatus.Absent)
            });
            return Ok(new { summary, records = records.Select(x => new { date = x.Date, subject = x.Subject.Name, status = x.Status.ToString() }) });
        }

        [HttpGet, Route("student/alerts")]
        public IHttpActionResult StudentAlerts()
        {
            AppUser u; Student s;
            if (!CurrentStudent(out u, out s)) return Unauthorized();
            return Ok(db.Notifications.Where(x => x.UserId == u.UserId).OrderByDescending(x => x.CreatedAt).Take(50).ToList()
                .Select(x => new { id = x.Id, message = x.Message, createdAt = x.CreatedAt, isRead = x.IsRead, type = x.Type, relatedEntityType = x.RelatedEntityType }));
        }
        [HttpGet, Route("student/emergency")]
        public IHttpActionResult StudentEmergency()
        {
            AppUser u; Student student; if (!CurrentStudent(out u, out student)) return Unauthorized();
            var alert = db.EmergencyAlerts.Where(x => x.Status == AlertStatus.Active).OrderByDescending(x => x.AlertTime).FirstOrDefault();
            return Ok(alert == null ? null : new { id = alert.AlertId, message = alert.AlertMessage, assemblyPoint = alert.AssemblyPointName, latitude = alert.AssemblyLatitude, longitude = alert.AssemblyLongitude, radiusMeters = alert.GeofenceRadiusMeters, sirenStopped = alert.SirenStopped });
        }
        [HttpPost, Route("student/emergency/confirm")]
        public IHttpActionResult ConfirmStudentSafety(EmergencySafetyRequest request)
        {
            AppUser u; Student student; if (!CurrentStudent(out u, out student) || request == null) return Unauthorized();
            var alert = db.EmergencyAlerts.Where(x => x.Status == AlertStatus.Active).OrderByDescending(x => x.AlertTime).FirstOrDefault();
            if (alert == null) return BadRequest("No active emergency alert.");
            var confirmation = db.StudentSafetyConfirmations.FirstOrDefault(x => x.AlertId == alert.AlertId && x.StudentId == student.StudentId) ?? new StudentSafetyConfirmation { AlertId = alert.AlertId, StudentId = student.StudentId };
            var assembly = new Helpers.GeofencingService.Location(alert.AssemblyLatitude, alert.AssemblyLongitude); var location = new Helpers.GeofencingService.Location(request.Latitude, request.Longitude);
            var distance = Helpers.GeofencingService.CalculateDistance(assembly, location); var inside = Helpers.GeofencingService.IsWithinRadius(assembly, location, alert.GeofenceRadiusMeters);
            confirmation.StudentLatitude = request.Latitude; confirmation.StudentLongitude = request.Longitude; confirmation.ConfirmationTime = DateTime.Now; confirmation.DistanceFromAssemblyPointMeters = distance; confirmation.WithinGeofence = inside; confirmation.Status = inside ? SafetyStatus.Confirmed : SafetyStatus.OutsideZone;
            if (confirmation.ConfirmationId == 0) db.StudentSafetyConfirmations.Add(confirmation); if (inside) alert.SirenStopped = true; db.SaveChanges();
            return Ok(new { success = true, withinGeofence = inside, distance = Math.Round(distance, 2), message = inside ? "Safety confirmed – you are accounted for." : "You are outside the designated safe zone. Move closer and try again." });
        }

        [HttpPost, Route("student/alerts/{id:int}/read")]
        public IHttpActionResult MarkStudentAlertRead(int id)
        {
            AppUser u; Student s;
            if (!CurrentStudent(out u, out s)) return Unauthorized();
            var alert = db.Notifications.FirstOrDefault(x => x.Id == id && x.UserId == u.UserId);
            if (alert == null) return NotFound();
            alert.IsRead = true; db.SaveChanges(); return Ok(new { success = true });
        }

        [HttpGet, Route("teacher/sessions")]
        public IHttpActionResult TeacherSessions()
        {
            AppUser u; Teacher teacher;
            if (!CurrentTeacher(out u, out teacher)) return Unauthorized();
            return Ok(db.TeacherSubjectGrades.Include(x => x.Subject).Where(x => x.TeacherId == teacher.TeacherId)
                .OrderBy(x => x.Grade).ThenBy(x => x.Subject.Name).ToList()
                .Select(x => new { subjectId = x.SubjectId, subject = x.Subject.Name, grade = x.Grade, stream = x.Stream.ToString() }));
        }

        [HttpGet, Route("teacher/sessions/{subjectId:int}/attendance")]
        public IHttpActionResult TeacherSessionAttendance(int subjectId)
        {
            AppUser u; Teacher teacher;
            if (!CurrentTeacher(out u, out teacher) || !db.TeacherSubjectGrades.Any(x => x.TeacherId == teacher.TeacherId && x.SubjectId == subjectId)) return Unauthorized();
            var students = db.StudentSubjects.Where(x => x.SubjectId == subjectId).Select(x => x.Student).OrderBy(x => x.LastName).ToList();
            var existing = db.Attendances.Where(x => x.SubjectId == subjectId && DbFunctions.TruncateTime(x.Date) == DateTime.Today).ToDictionary(x => x.StudentId);
            return Ok(new { date = DateTime.Today, students = students.Select(x => new { studentId = x.StudentId, name = x.Name, status = existing.ContainsKey(x.StudentId) ? existing[x.StudentId].Status.ToString() : "Present" }) });
        }

        [HttpPost, Route("teacher/sessions/{subjectId:int}/attendance")]
        public IHttpActionResult SaveTeacherSessionAttendance(int subjectId, TeacherAttendanceRequest input)
        {
            AppUser u; Teacher teacher;
            if (!CurrentTeacher(out u, out teacher) || !db.TeacherSubjectGrades.Any(x => x.TeacherId == teacher.TeacherId && x.SubjectId == subjectId)) return Unauthorized();
            if (input == null || input.Date.Date != DateTime.Today) return BadRequest("Attendance can only be saved for today.");
            var enrolled = db.StudentSubjects.Where(x => x.SubjectId == subjectId).Select(x => x.StudentId).ToList();
            var existing = db.Attendances.Where(x => x.SubjectId == subjectId && DbFunctions.TruncateTime(x.Date) == DateTime.Today).ToDictionary(x => x.StudentId);
            foreach (var entry in input.Students ?? Enumerable.Empty<TeacherAttendanceEntry>())
            {
                if (!enrolled.Contains(entry.StudentId) || !Enum.TryParse(entry.Status, true, out AttendanceStatus status)) return BadRequest("Invalid attendance entry.");
                Attendance record;
                if (existing.TryGetValue(entry.StudentId, out record)) { if (record.Status != status && status != AttendanceStatus.Late) return BadRequest("Existing attendance can only be changed to Late."); if (status == AttendanceStatus.Late) record.Status = status; }
                else db.Attendances.Add(new Attendance { StudentId = entry.StudentId, SubjectId = subjectId, Date = DateTime.Today, Status = status, RecordedBy = teacher.Email });
            }
            db.SaveChanges(); return Ok(new { success = true, message = "Attendance saved." });
        }

        [HttpGet, Route("driver/trips")]
        public IHttpActionResult DriverTrips()
        {
            AppUser u; Driver driver; if (!CurrentDriver(out u, out driver)) return Unauthorized();
            var direct = db.TripSchedules.Include(x => x.TripRequest).Include(x => x.TripStudents).Where(x => x.DriverId == driver.Id && x.Status != "Completed");
            var assigned = db.TripVehicleAssignments.Include(x => x.TripSchedule.TripRequest).Include(x => x.TripSchedule.TripStudents).Where(x => x.DriverId == driver.Id && x.TripSchedule.Status != "Completed").Select(x => x.TripSchedule);
            return Ok(direct.Union(assigned).Distinct().OrderBy(x => x.ScheduledDate).ToList().Select(x => new { id = x.Id, title = x.TripRequest.Title, scheduledDate = x.ScheduledDate, status = x.Status, students = x.TripStudents.Count }));
        }
        [HttpGet, Route("driver/trips/{scheduleId:int}/manifest")]
        public IHttpActionResult DriverManifest(int scheduleId)
        {
            AppUser u; Driver driver; TripSchedule schedule;
            if (!CurrentDriver(out u, out driver) || !TryDriverSchedule(driver.Id, scheduleId, out schedule)) return Unauthorized();
            return Ok(new { id = schedule.Id, title = schedule.TripRequest.Title, scheduledDate = schedule.ScheduledDate, students = schedule.TripStudents.OrderBy(x => x.Student.LastName).Select(x => new { studentId = x.StudentId, name = x.Student.Name, boarded = x.IsPresentBefore == true, returned = x.IsPresentAfter == true }) });
        }
        [HttpPost, Route("driver/trips/{scheduleId:int}/scan")]
        public IHttpActionResult DriverScan(int scheduleId, DriverScanRequest input)
        {
            AppUser u; Driver driver; TripSchedule schedule;
            if (!CurrentDriver(out u, out driver) || !TryDriverSchedule(driver.Id, scheduleId, out schedule)) return Unauthorized();
            if (input == null || (input.Phase != "before" && input.Phase != "after") || String.IsNullOrWhiteSpace(input.QrValue)) return BadRequest("A QR code and valid trip phase are required.");
            if (schedule.ScheduledDate.Date != DateTime.Today) return BadRequest("Attendance can only be recorded on the scheduled trip date.");
            var student = ResolveStudentQr(input.QrValue); if (student == null) return BadRequest("QR code not recognised.");
            var tripStudent = schedule.TripStudents.FirstOrDefault(x => x.StudentId == student.StudentId); if (tripStudent == null) return BadRequest("This student is not expected on this trip.");
            if (input.Phase == "before" && tripStudent.IsPresentBefore == true || input.Phase == "after" && tripStudent.IsPresentAfter == true) return BadRequest("This student has already been scanned for this trip phase.");
            if (input.Phase == "before") { tripStudent.IsPresentBefore = true; tripStudent.MarkedBeforeAt = DateTime.Now; tripStudent.MarkedBeforeBy = u.UserId.ToString(); }
            else { tripStudent.IsPresentAfter = true; tripStudent.MarkedAfterAt = DateTime.Now; tripStudent.MarkedAfterBy = u.UserId.ToString(); }
            db.QRScanRecords.Add(new QRScanRecord { StudentId = student.StudentId, ScannedAt = DateTime.UtcNow, Scanner = driver.FullName, Approved = true, Reason = "Trip " + input.Phase + " attendance", OnTrip = true });
            if (input.Phase == "after" && schedule.TripStudents.All(x => x.IsPresentAfter == true)) schedule.Status = "Completed";
            db.SaveChanges(); return Ok(new { success = true, message = student.Name + " marked present for " + (input.Phase == "before" ? "boarding" : "return") + ".", studentName = student.Name });
        }

        [HttpGet, Route("housemaster/alerts")]
        public IHttpActionResult HouseMasterAlerts()
        {
            AppUser u; HouseMaster master; if (!CurrentHouseMaster(out u, out master)) return Unauthorized();
            var residenceIds = db.Residences.Where(x => x.HouseMasterId == master.HouseMasterId && !x.IsArchived).Select(x => x.ResidenceId).ToList();
            return Ok(db.AIAlerts.Include(x => x.Residence).Where(x => residenceIds.Contains(x.ResidenceId) && !x.IsResolved).OrderByDescending(x => x.CreatedAt).ToList()
                .Select(x => new { id = x.AIAlertId, residence = x.Residence.Name, title = x.Title, message = x.Message, createdAt = x.CreatedAt }));
        }
        [HttpGet, Route("housemaster/residences")]
        public IHttpActionResult HouseMasterResidences()
        {
            AppUser u; HouseMaster master; if (!CurrentHouseMaster(out u, out master)) return Unauthorized();
            return Ok(db.Residences.Where(x => x.HouseMasterId == master.HouseMasterId && !x.IsArchived).OrderBy(x => x.Name).ToList().Select(x => new { id = x.ResidenceId, name = x.Name }));
        }
        [HttpGet, Route("housemaster/residences/{residenceId:int}/rollcall")]
        public IHttpActionResult EmergencyRollCall(int residenceId)
        {
            AppUser u; HouseMaster master; if (!CurrentHouseMaster(out u, out master) || !OwnsResidence(master.HouseMasterId, residenceId)) return Unauthorized();
            return Ok(db.ResidenceAssignments.Include(x => x.Student).Where(x => x.ResidenceId == residenceId && x.IsActive).OrderBy(x => x.Student.LastName).ToList()
                .Select(x => new { studentId = x.StudentId, name = x.Student.Name }));
        }
        [HttpPost, Route("housemaster/residences/{residenceId:int}/rollcall/{studentId:int}")]
        public IHttpActionResult RecordEmergencyRollCall(int residenceId, int studentId)
        {
            AppUser u; HouseMaster master; if (!CurrentHouseMaster(out u, out master) || !OwnsResidence(master.HouseMasterId, residenceId)) return Unauthorized();
            if (!db.ResidenceAssignments.Any(x => x.ResidenceId == residenceId && x.StudentId == studentId && x.IsActive)) return BadRequest("Student is not active in this residence.");
            db.QRScanRecords.Add(new QRScanRecord { StudentId = studentId, ResidenceId = residenceId, Action = "EmergencyRollCall", ScannedAt = DateTime.UtcNow, HouseMasterId = master.HouseMasterId, Scanner = master.FullName, Approved = true, Reason = "Student present for emergency roll call." });
            db.SaveChanges(); return Ok(new { success = true });
        }

        private bool Current(out AppUser u) { u = null; int id; string role; var h = Request.Headers.Authorization; if (h == null || !String.Equals(h.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || !tokens.TryRead(h.Parameter, out id, out role)) return false; u = db.Users.Find(id); return u != null; }
        private bool Staff() { AppUser u; return Current(out u) && IsStaff(u); }
        private bool CurrentStudent(out AppUser u, out Student student)
        {
            student = null;
            u = null;
            if (!Current(out u) || !String.Equals(u.Role, "Student", StringComparison.OrdinalIgnoreCase)) return false;
            var userId = u.UserId;
            student = db.Students.Include(x => x.Parent).FirstOrDefault(x => x.UserId == userId);
            return student != null;
        }
        private bool CurrentTeacher(out AppUser u, out Teacher teacher)
        {
            teacher = null;
            u = null;
            if (!Current(out u) || !String.Equals(u.Role, "Teacher", StringComparison.OrdinalIgnoreCase)) return false;
            var userId = u.UserId;
            teacher = db.Teachers.FirstOrDefault(x => x.UserId == userId);
            return teacher != null;
        }
        private bool CurrentDriver(out AppUser u, out Driver driver)
        {
            driver = null;
            u = null;
            if (!Current(out u) || !String.Equals(u.Role, "Driver", StringComparison.OrdinalIgnoreCase)) return false;
            var userId = u.UserId;
            driver = db.Drivers.FirstOrDefault(x => x.UserId == userId && x.IsActive);
            return driver != null;
        }
        private bool CurrentHouseMaster(out AppUser u, out HouseMaster master)
        {
            master = null;
            u = null;
            if (!Current(out u) || !String.Equals(u.Role, "HouseMaster", StringComparison.OrdinalIgnoreCase)) return false;
            var email = u.Email;
            master = db.HouseMasters.FirstOrDefault(x => x.ContactEmail == email);
            return master != null;
        }
        private bool OwnsResidence(int masterId, int residenceId) { return db.Residences.Any(x => x.ResidenceId == residenceId && x.HouseMasterId == masterId && !x.IsArchived); }
        private bool TryDriverSchedule(int driverId, int scheduleId, out TripSchedule schedule) { schedule = db.TripSchedules.Include(x => x.TripRequest).Include(x => x.TripStudents.Select(y => y.Student)).FirstOrDefault(x => x.Id == scheduleId && (x.DriverId == driverId || x.VehicleAssignments.Any(y => y.DriverId == driverId))); return schedule != null; }
        private Student ResolveStudentQr(string value) { var direct = db.StudentQRCodes.Include(x => x.Student).FirstOrDefault(x => x.QRCodeValue == value && x.IsActive); if (direct != null) return direct.Student; try { var token = System.Web.HttpUtility.ParseQueryString(new Uri(value).Query).Get("token"); var attendance = db.StudentAttendanceTokens.FirstOrDefault(x => x.Token == token); return attendance == null ? null : db.Students.Find(attendance.StudentId); } catch { return null; } }
        private static bool IsStaff(AppUser u) { return u.Role != "Student" && u.Role != "Parent"; }
        private static bool CanManage(AppUser u) { return u.Role == "Admin" || u.Role == "HouseMaster" || u.Role == "Housemaster"; }
        private static string Hash(string value) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        private static object User(AppUser x, string token) { return new { userId = x.UserId, name = x.Name, email = x.Email, role = x.Role, token = token }; }
        private static object Student(Student x) { return new { studentId = x.StudentId, studentNumber = x.StudentNumber, firstName = x.FirstName, lastName = x.LastName, name = x.Name, grade = x.CurrentGrade, gradeLevel = x.GradeLevel, medicalConditions = x.MedicalConditions, parent = x.Parent == null ? null : new { name = x.Parent.Name, contact = x.Parent.Contact } }; }
        private static object Residence(Residence x) { return new { residenceId = x.ResidenceId, name = x.Name, capacity = x.Capacity, occupiedBeds = x.OccupiedBeds, availableBeds = x.AvailableBeds, gradeCategory = x.GradeCategory }; }
        private static object Room(Room x) { return new { roomId = x.RoomId, roomNumber = x.RoomNumber, capacity = x.Capacity, occupiedBeds = x.OccupiedBeds, availableBeds = x.AvailableBeds, isFull = x.IsFull, beds = x.Beds.Select(b => new { bedId = b.BedId, bedNumber = b.BedNumber, isOccupied = b.IsOccupied, status = b.Status }) }; }
        private static object AssignmentDto(ResidenceAssignment x) { return new { assignmentId = x.ResidenceAssignmentId, studentId = x.StudentId, residence = Residence(x.Residence), room = new { roomId = x.Room.RoomId, roomNumber = x.Room.RoomNumber }, bed = new { bedId = x.Bed.BedId, bedNumber = x.Bed.BedNumber }, moveInDate = x.MoveInDate, status = x.Status }; }
        private static object RecommendationDto(AIResidenceAllocationService.AIAllocationRecommendation x) { return new { residence = Residence(x.Residence), room = new { roomId = x.Room.RoomId, roomNumber = x.Room.RoomNumber }, compatibilityScore = x.CompatibilityScore, confidenceScore = x.ConfidenceScore, explanation = x.Explanation }; }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
    public class LoginRequest { public string Email { get; set; } public string Password { get; set; } }
    public class MoveRequest { public int StudentId { get; set; } public int TargetRoomId { get; set; } public string Reason { get; set; } }
    public class AllocationRequest { public int? RoomId { get; set; } public string Reason { get; set; } }
    public class ScanRequest { public string QrValue { get; set; } }
    public class TeacherAttendanceRequest { public DateTime Date { get; set; } public List<TeacherAttendanceEntry> Students { get; set; } }
    public class TeacherAttendanceEntry { public int StudentId { get; set; } public string Status { get; set; } }
    public class DriverScanRequest { public string QrValue { get; set; } public string Phase { get; set; } }
    public class EmergencySafetyRequest { public double Latitude { get; set; } public double Longitude { get; set; } }
}
