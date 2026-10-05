using Michaelhouse.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using static Michaelhouse.Models.Schoolcalendarevent;
using static Michaelhouse.Services.Calendarconflictservice;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Mobile — Request Permission to Leave Residence (UC27, Student)
    //
    //   GET  /api/leave-requests/form         residence, house master, my requests
    //   GET  /api/leave-requests/conflicts?start=…&end=…   school events in the way
    //   POST /api/leave-requests              { destination, departureDateTime,
    //                                           expectedReturnDateTime, reason,
    //                                           acknowledgeConflict }
    //
    // Same rules as the web page LeaveRequest/Create: a parent must be
    // linked, the student must have an active residence assignment whose
    // residence has a house master (resolved here, never chosen by the
    // student), departure in the future, return after departure, and a
    // school-calendar clash must be acknowledged. The request then waits
    // for the parent, then the house master — on the website.
    // ============================================================
    [RoutePrefix("api/leave-requests")]
    [Authorize(Roles = "Student")]
    public class LeaveRequestApiController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET /api/leave-requests/form
        // ============================================================

        [HttpGet]
        [Route("form")]
        public JsonResult Form()
        {
            int studentId = CurrentStudentId();
            if (studentId == 0) return Json(new { ok = false, error = "Please log in as a student." }, JsonRequestBehavior.AllowGet);

            var assignment = GetActiveAssignment(studentId);

            var requests = db.LeaveRequests
                .Include(l => l.HouseMaster)
                .Include(l => l.Residence)
                .Where(l => l.StudentId == studentId && !l.IsArchived)
                .OrderByDescending(l => l.SubmittedAt)
                .Take(20)
                .ToList();

            return Json(new
            {
                ok = true,
                // Same message as the web's Create page when there's no residence
                canRequest = assignment != null && assignment.Residence != null,
                cannotRequestReason = assignment == null || assignment.Residence == null
                    ? "You do not have an active residence assignment, so a leave request cannot be created. Please contact your house master or administration."
                    : null,
                residence = assignment != null && assignment.Residence != null ? assignment.Residence.Name : null,
                houseMaster = assignment != null && assignment.Residence != null && assignment.Residence.HouseMaster != null
                    ? assignment.Residence.HouseMaster.FullName
                    : "Not yet assigned",
                myRequests = requests.Select(l => new
                {
                    id = l.LeaveRequestId,
                    destination = l.Destination,
                    departure = l.DepartureDateTime.ToString("ddd dd MMM yyyy, HH:mm"),
                    expectedReturn = l.ExpectedReturnDateTime.ToString("ddd dd MMM yyyy, HH:mm"),
                    reason = l.Reason,
                    status = l.Status,
                    statusLabel = StatusLabel(l.Status),
                    parentComments = l.ParentComments,
                    houseMasterComments = l.HouseMasterComments,
                    hasCalendarConflict = l.HasCalendarConflict,
                    submitted = l.SubmittedAt.ToString("ddd dd MMM yyyy, HH:mm")
                })
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // GET /api/leave-requests/conflicts — LeaveRequest/CheckDateConflicts
        // ============================================================

        [HttpGet]
        [Route("conflicts")]
        public JsonResult Conflicts(DateTime? start, DateTime? end)
        {
            if (!start.HasValue || !end.HasValue)
                return Json(new { ok = true, hasConflict = false }, JsonRequestBehavior.AllowGet);

            var conflicts = CalendarConflictService.GetConflicts(db, start.Value, end.Value);

            return Json(new
            {
                ok = true,
                hasConflict = conflicts.Any(),
                message = CalendarConflictService.BuildRecommendationMessage(conflicts),
                conflicts = conflicts.Select(c => new
                {
                    title = c.Title,
                    start = c.StartDate.ToString("dd MMM"),
                    end = c.EndDate.ToString("dd MMM"),
                    category = CalendarEventCategory.DisplayName(c.Category)
                })
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // POST /api/leave-requests — LeaveRequest/Create (POST)
        // ============================================================

        [HttpPost]
        [Route("")]
        public JsonResult Create()
        {
            int studentId = CurrentStudentId();
            if (studentId == 0) return Json(new { ok = false, error = "Please log in as a student to request permission to leave." });

            var input = ReadBody<LeaveRequestInput>();
            if (input == null) return Json(new { ok = false, error = "The request could not be read." });

            var errors = new List<string>();

            var student = db.Students.Include(s => s.Parent).FirstOrDefault(s => s.StudentId == studentId);
            if (student == null) return Json(new { ok = false, error = "Student record not found." });

            if (student.Parent == null)
                errors.Add("No parent/guardian is linked to your record. Please contact administration before submitting a leave request.");

            var assignment = GetActiveAssignment(studentId);
            if (assignment == null || assignment.Residence == null)
                errors.Add("You do not have an active residence assignment, so this request cannot be routed to a house master.");
            else if (assignment.Residence.HouseMasterId == null)
                errors.Add("Your residence does not currently have a house master assigned. Please contact administration.");

            // Model validation ([Required], [StringLength] on LeaveRequest)
            var destination = (input.Destination ?? "").Trim();
            var reason = (input.Reason ?? "").Trim();
            if (destination.Length == 0) errors.Add("The Destination field is required.");
            else if (destination.Length > 300) errors.Add("The Destination must be 300 characters or fewer.");
            if (reason.Length == 0) errors.Add("The Reason for Leaving field is required.");
            else if (reason.Length > 500) errors.Add("The Reason for Leaving must be 500 characters or fewer.");
            if (!input.DepartureDateTime.HasValue) errors.Add("The Departure Date & Time field is required.");
            if (!input.ExpectedReturnDateTime.HasValue) errors.Add("The Expected Return Date & Time field is required.");

            List<CalendarConflict> conflicts = new List<CalendarConflict>();
            if (input.DepartureDateTime.HasValue && input.ExpectedReturnDateTime.HasValue)
            {
                bool datesValid = true;
                if (input.DepartureDateTime.Value <= DateTime.Now)
                {
                    errors.Add("Departure date and time must be in the future.");
                    datesValid = false;
                }
                if (input.ExpectedReturnDateTime.Value <= input.DepartureDateTime.Value)
                {
                    errors.Add("Expected return must be after the departure date and time.");
                    datesValid = false;
                }

                // Recomputed here, never trusted from the app
                if (datesValid)
                    conflicts = CalendarConflictService.GetConflicts(db, input.DepartureDateTime.Value, input.ExpectedReturnDateTime.Value);
            }

            if (conflicts.Any() && !input.AcknowledgeConflict)
                errors.Add("Your selected dates overlap with a flagged school event. Please tick the acknowledgement box if you still wish to submit.");

            if (errors.Count > 0)
                return Json(new { ok = false, error = string.Join(" ", errors), errors, hasConflict = conflicts.Any() });

            var request = new LeaveRequest
            {
                StudentId = studentId,
                ParentId = student.Parent.ParentId,
                ResidenceId = assignment.ResidenceId,
                HouseMasterId = assignment.Residence.HouseMasterId,
                Destination = destination,
                DepartureDateTime = input.DepartureDateTime.Value,
                ExpectedReturnDateTime = input.ExpectedReturnDateTime.Value,
                Reason = reason,
                Status = LeaveRequestStatus.PendingParentApproval,
                SubmittedAt = DateTime.Now,
                HasCalendarConflict = conflicts.Any(),
                CalendarConflictSummary = CalendarConflictService.BuildSummary(conflicts)
            };

            db.LeaveRequests.Add(request);
            db.SaveChanges();

            return Json(new
            {
                ok = true,
                id = request.LeaveRequestId,
                message = request.HasCalendarConflict
                    ? "Your leave request has been submitted (flagged as overlapping a school event) and sent to your parent/guardian for approval."
                    : "Your leave request has been submitted and sent to your parent/guardian for approval."
            });
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static string StatusLabel(string status)
        {
            switch (status)
            {
                case LeaveRequestStatus.PendingParentApproval: return "Waiting for parent";
                case LeaveRequestStatus.PendingHouseMasterApproval: return "Waiting for house master";
                case LeaveRequestStatus.Approved: return "Approved";
                case LeaveRequestStatus.Rejected: return "Rejected";
                default: return status;
            }
        }

        // Same as LeaveRequestController.GetActiveAssignment
        private ResidenceAssignment GetActiveAssignment(int studentId)
        {
            return db.ResidenceAssignments
                .Include(a => a.Residence)
                .Include(a => a.Residence.HouseMaster)
                .Where(a => a.StudentId == studentId && a.IsActive && !a.IsArchived)
                .OrderByDescending(a => a.MoveInDate)
                .FirstOrDefault();
        }

        private int CurrentStudentId()
        {
            int id;
            return Session["StudentId"] != null && int.TryParse(Session["StudentId"].ToString(), out id) ? id : 0;
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

    public class LeaveRequestInput
    {
        public string Destination { get; set; }
        public DateTime? DepartureDateTime { get; set; }
        public DateTime? ExpectedReturnDateTime { get; set; }
        public string Reason { get; set; }
        public bool AcknowledgeConflict { get; set; }
    }
}
