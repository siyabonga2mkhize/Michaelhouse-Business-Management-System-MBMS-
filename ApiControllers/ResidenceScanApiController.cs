using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Mobile — residence QR sign in / sign out (House Master / Admin)
    //
    //   POST /api/residence-scan/lookup     { qrValue }    → student + status
    //   POST /api/residence-scan/check-in   { studentId }
    //   POST /api/residence-scan/check-out  { studentId }
    //
    // Same rules as the web page HouseMaster/ScanQRCode and its
    // CheckIn / CheckOut actions: the QR must be an active student QR
    // identity, the student must be in the House Master's residence
    // (BoardingAccessService), check in only when Outside, check out
    // only when Inside, nothing for suspended / on-holiday / archived
    // students. Each action is logged as a QRScanRecord.
    //
    // The status and rule methods below are the same as the private
    // ones in HouseMasterController (GetLatestAssignment,
    // GetCurrentBoardingStatus, GetStatusLabel, EnsureActionAllowed,
    // RecalculateOccupancy, LogResidenceAction) — keep them in step.
    // ============================================================
    [RoutePrefix("api/residence-scan")]
    [AdminOrHouseMasterOnly]
    public class ResidenceScanApiController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private readonly BoardingAccessService boardingAccess = new BoardingAccessService();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // POST /api/residence-scan/lookup — what the scanned QR is
        // ============================================================

        [HttpPost]
        [Route("lookup")]
        public JsonResult Lookup()
        {
            var data = ReadBody<ScanLookupRequest>();
            var value = (data == null ? "" : data.QrValue ?? "").Trim();
            if (value.Length == 0)
                return Json(new { ok = false, error = "Scan or enter a student QR code first." });

            // Same lookup as HouseMaster/ScanQRCode: the QR value, or a student id
            var qr = db.StudentQRCodes.Include(q => q.Student).FirstOrDefault(q => q.IsActive && q.QRCodeValue == value);
            int studentIdValue;
            if (qr == null && int.TryParse(value, out studentIdValue))
                qr = db.StudentQRCodes.Include(q => q.Student).FirstOrDefault(q => q.IsActive && q.StudentId == studentIdValue);

            if (qr == null || qr.Student == null)
                return Json(new { ok = false, error = "No active student QR identity was found for that scan." });

            if (!boardingAccess.IsAdmin(this) && !boardingAccess.CanAccessStudentCurrentResidence(this, db, qr.StudentId))
                return Json(new { ok = false, error = "This student is not currently allocated to your assigned residence." });

            return Json(StudentJson(qr.StudentId, null));
        }

        // ============================================================
        // POST /api/residence-scan/check-in — HouseMaster/CheckIn
        // ============================================================

        [HttpPost]
        [Route("check-in")]
        public JsonResult CheckIn()
        {
            var data = ReadBody<ScanActionRequest>();
            int studentId = data == null ? 0 : data.StudentId;
            if (db.Students.Find(studentId) == null) return Json(new { ok = false, error = "Student not found." });

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

                return Json(StudentJson(studentId, "Student checked in."));
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ============================================================
        // POST /api/residence-scan/check-out — HouseMaster/CheckOut
        // ============================================================

        [HttpPost]
        [Route("check-out")]
        public JsonResult CheckOut()
        {
            var data = ReadBody<ScanActionRequest>();
            int studentId = data == null ? 0 : data.StudentId;
            if (db.Students.Find(studentId) == null) return Json(new { ok = false, error = "Student not found." });

            try
            {
                EnsureActionAllowed(studentId, "CheckOut");
                var assignment = GetLatestAssignment(studentId);
                if (assignment == null)
                    throw new InvalidOperationException("Student has no active residence assignment.");

                assignment.Status = "Outside";
                LogResidenceAction(studentId, "CheckOut", "Student checked out by house master.", false, assignment.ResidenceId);
                db.SaveChanges();

                return Json(StudentJson(studentId, "Student checked out."));
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ============================================================
        // REPLY
        // ============================================================

        // The student, their residence and what can be done now
        // (HouseMasterController.PopulateScanVerification)
        private object StudentJson(int studentId, string message)
        {
            var student = db.Students.Find(studentId);
            var assignment = GetLatestAssignment(studentId);
            var status = GetCurrentBoardingStatus(studentId, assignment);

            return new
            {
                ok = true,
                message,
                studentId,
                name = student != null ? (student.FirstName + " " + student.LastName).Trim() : "",
                studentNumber = student != null ? student.StudentNumber : null,
                residence = assignment != null && assignment.Residence != null ? assignment.Residence.Name : null,
                room = assignment != null && assignment.Room != null ? assignment.Room.RoomNumber : null,
                bed = assignment != null && assignment.Bed != null ? assignment.Bed.BedNumber : null,
                hasAssignment = assignment != null,
                status,
                statusLabel = GetStatusLabel(status),
                lastCheckIn = LastActionTime(studentId, "CheckIn"),
                lastCheckOut = LastActionTime(studentId, "CheckOut"),
                canCheckIn = status == "Outside",
                canCheckOut = status == "Inside"
            };
        }

        private string LastActionTime(int studentId, string action)
        {
            var when = db.QRScanRecords
                .Where(q => q.StudentId == studentId && q.Action == action && !q.IsArchived)
                .OrderByDescending(q => q.ScannedAt)
                .Select(q => (DateTime?)q.ScannedAt)
                .FirstOrDefault();
            return when.HasValue ? SchoolClock.FromUtc(when.Value).ToString("ddd dd MMM, HH:mm") : null;
        }

        // ============================================================
        // RULES — same as HouseMasterController
        // ============================================================

        private void EnsureActionAllowed(int studentId, string action)
        {
            var assignment = GetLatestAssignment(studentId);
            var status = GetCurrentBoardingStatus(studentId, assignment);

            if (!boardingAccess.CanAccessStudentCurrentResidence(this, db, studentId))
                throw new InvalidOperationException("You can only manage students currently allocated to your assigned residence.");

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

            if (action == "CheckIn" && status != "Outside")
                throw new InvalidOperationException("Check in is only available when the student is outside the residence.");

            if (action == "CheckOut" && status != "Inside")
                throw new InvalidOperationException("Check out is only available when the student is inside the residence.");
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

        private static string GetStatusLabel(string status)
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

    public class ScanLookupRequest
    {
        public string QrValue { get; set; }
    }

    public class ScanActionRequest
    {
        public int StudentId { get; set; }
    }
}
