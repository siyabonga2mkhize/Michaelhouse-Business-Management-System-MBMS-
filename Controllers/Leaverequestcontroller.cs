using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using static Michaelhouse.Models.Schoolcalendarevent;
using static Michaelhouse.Services.Calendarconflictservice;

namespace Michaelhouse.Controllers
{
    /// <summary>
    /// Use Case 27 - Request Permission to Leave Residence.
    ///
    /// Workflow:
    ///   Student submits  -> PendingParentApproval
    ///   Parent decides    -> Rejected (stop) OR PendingHouseMasterApproval
    ///   House Master decides -> Approved OR Rejected
    ///
    /// The House Master is never chosen by the student. It is always
    /// resolved server-side from the student's active ResidenceAssignment
    /// (Student -> ResidenceAssignment -> Residence -> HouseMaster), so the
    /// request is guaranteed to go to the house master of the house the
    /// student is actually allocated in.
    /// </summary>
    public class LeaveRequestController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        // ─── Session helpers (same pattern used across the codebase) ──────

        private int GetCurrentStudentId()
        {
            if (Session["StudentId"] == null) return 0;
            return (int)Session["StudentId"];
        }

        private int GetCurrentParentId()
        {
            if (Session["ParentId"] == null) return 0;
            return (int)Session["ParentId"];
        }

        private int? GetCurrentHouseMasterId()
        {
            return Session["HouseMasterId"] as int?;
        }

        private bool IsAdmin()
        {
            var role = (Session["UserRole"] as string) ?? string.Empty;
            return role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The student's current, active, non-archived residence assignment.
        /// This is the single source of truth for "which house is this
        /// student in right now" and therefore "which house master should
        /// approve this leave request".
        /// </summary>
        private ResidenceAssignment GetActiveAssignment(int studentId)
        {
            return db.ResidenceAssignments
                .Include(a => a.Residence)
                .Include(a => a.Residence.HouseMaster)
                .Where(a => a.StudentId == studentId && a.IsActive && !a.IsArchived)
                .OrderByDescending(a => a.MoveInDate)
                .FirstOrDefault();
        }

        private bool CanView(LeaveRequest request)
        {
            if (IsAdmin()) return true;

            int studentId = GetCurrentStudentId();
            if (studentId != 0 && studentId == request.StudentId) return true;

            int parentId = GetCurrentParentId();
            if (parentId != 0 && parentId == request.ParentId) return true;

            var houseMasterId = GetCurrentHouseMasterId();
            if (houseMasterId.HasValue && request.HouseMasterId.HasValue && houseMasterId.Value == request.HouseMasterId.Value) return true;

            return false;
        }

        // ════════════════════════════════════════════════════════════════
        // STUDENT: create + view own requests
        // ════════════════════════════════════════════════════════════════

        // GET: LeaveRequest/Create
        [RequireLogin]
        public ActionResult Create()
        {
            int studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                TempData["Error"] = "Please log in as a student to request permission to leave.";
                return RedirectToAction("Login", "Account");
            }

            var assignment = GetActiveAssignment(studentId);
            if (assignment == null || assignment.Residence == null)
            {
                TempData["Error"] = "You do not have an active residence assignment, so a leave request cannot be created. Please contact your house master or administration.";
                return RedirectToAction("Dashboard", "Students");
            }

            ViewBag.ResidenceName = assignment.Residence.Name;
            ViewBag.HouseMasterName = assignment.Residence.HouseMaster?.FullName ?? "Not yet assigned";

            return View(new LeaveRequest
            {
                DepartureDateTime = DateTime.Now.AddHours(1),
                ExpectedReturnDateTime = DateTime.Now.AddHours(4)
            });
        }

        // POST: LeaveRequest/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public ActionResult Create([Bind(Include = "Destination,DepartureDateTime,ExpectedReturnDateTime,Reason")] LeaveRequest model, bool acknowledgeConflict = false)
        {
            int studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                TempData["Error"] = "Please log in as a student to request permission to leave.";
                return RedirectToAction("Login", "Account");
            }

            var student = db.Students.Include(s => s.Parent).FirstOrDefault(s => s.StudentId == studentId);
            if (student == null)
            {
                TempData["Error"] = "Student record not found.";
                return RedirectToAction("Dashboard", "Students");
            }

            if (student.Parent == null)
            {
                ModelState.AddModelError("", "No parent/guardian is linked to your record. Please contact administration before submitting a leave request.");
            }

            // Resolve residence + house master server-side. This is what
            // guarantees the request always reaches the correct house master.
            var assignment = GetActiveAssignment(studentId);
            if (assignment == null || assignment.Residence == null)
            {
                ModelState.AddModelError("", "You do not have an active residence assignment, so this request cannot be routed to a house master.");
            }
            else if (assignment.Residence.HouseMasterId == null)
            {
                ModelState.AddModelError("", "Your residence does not currently have a house master assigned. Please contact administration.");
            }

            if (model.DepartureDateTime <= DateTime.Now)
                ModelState.AddModelError("DepartureDateTime", "Departure date and time must be in the future.");

            if (model.ExpectedReturnDateTime <= model.DepartureDateTime)
                ModelState.AddModelError("ExpectedReturnDateTime", "Expected return must be after the departure date and time.");

            // Calendar conflict check - never trust the client's own
            // AJAX result, recompute here. This is a recommendation, not a
            // hard block: if a conflict exists the student must explicitly
            // acknowledge it (tick the box) before the request goes through.
            var conflicts = new List<CalendarConflict>();
            if (ModelState.IsValid || (ModelState.IsValidField("DepartureDateTime") && ModelState.IsValidField("ExpectedReturnDateTime")))
            {
                conflicts = CalendarConflictService.GetConflicts(db, model.DepartureDateTime, model.ExpectedReturnDateTime);
            }

            if (conflicts.Any() && !acknowledgeConflict)
            {
                ModelState.AddModelError("", "Your selected dates overlap with a flagged school event (see the warning above). Please tick the acknowledgement box if you still wish to submit.");
            }

            if (!ModelState.IsValid)
            {
                if (assignment?.Residence != null)
                {
                    ViewBag.ResidenceName = assignment.Residence.Name;
                    ViewBag.HouseMasterName = assignment.Residence.HouseMaster?.FullName ?? "Not yet assigned";
                }
                return View(model);
            }

            model.StudentId = studentId;
            model.ParentId = student.Parent.ParentId;
            model.ResidenceId = assignment.ResidenceId;
            model.HouseMasterId = assignment.Residence.HouseMasterId;
            model.Status = LeaveRequestStatus.PendingParentApproval;
            model.SubmittedAt = DateTime.Now;
            model.HasCalendarConflict = conflicts.Any();
            model.CalendarConflictSummary = CalendarConflictService.BuildSummary(conflicts);

            db.LeaveRequests.Add(model);
            db.SaveChanges();

            TempData["Success"] = model.HasCalendarConflict
                ? "Your leave request has been submitted (flagged as overlapping a school event) and sent to your parent/guardian for approval."
                : "Your leave request has been submitted and sent to your parent/guardian for approval.";
            return RedirectToAction("MyRequests");
        }

        // GET: LeaveRequest/CheckDateConflicts?start=...&end=...
        // AJAX endpoint used by the Create page to give the student a live
        // recommendation as they pick dates. Purely advisory - the same
        // check is re-run server-side in Create(POST) regardless of what
        // this returns.
        [RequireLogin]
        public JsonResult CheckDateConflicts(DateTime? start, DateTime? end)
        {
            if (!start.HasValue || !end.HasValue)
                return Json(new { hasConflict = false }, JsonRequestBehavior.AllowGet);

            var conflicts = CalendarConflictService.GetConflicts(db, start.Value, end.Value);

            var result = new
            {
                hasConflict = conflicts.Any(),
                message = CalendarConflictService.BuildRecommendationMessage(conflicts),
                conflicts = conflicts.Select(c => new
                {
                    c.Title,
                    start = c.StartDate.ToString("dd MMM"),
                    end = c.EndDate.ToString("dd MMM"),
                    category = CalendarEventCategory.DisplayName(c.Category)
                })
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // GET: LeaveRequest/MyRequests
        [RequireLogin]
        public ActionResult MyRequests()
        {
            int studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                TempData["Error"] = "Please log in as a student.";
                return RedirectToAction("Login", "Account");
            }

            var requests = db.LeaveRequests
                .Include(l => l.HouseMaster)
                .Include(l => l.Residence)
                .Where(l => l.StudentId == studentId && !l.IsArchived)
                .OrderByDescending(l => l.SubmittedAt)
                .ToList();

            return View(requests);
        }

        // ════════════════════════════════════════════════════════════════
        // SHARED: details (visible to the student, the parent, the
        // assigned house master, or an admin)
        // ════════════════════════════════════════════════════════════════

        // GET: LeaveRequest/Details/5
        [RequireLogin]
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var request = db.LeaveRequests
                .Include(l => l.Student)
                .Include(l => l.Parent)
                .Include(l => l.HouseMaster)
                .Include(l => l.Residence)
                .FirstOrDefault(l => l.LeaveRequestId == id);

            if (request == null) return HttpNotFound();
            if (!CanView(request)) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            return View(request);
        }

        // ════════════════════════════════════════════════════════════════
        // PARENT/GUARDIAN: review + decide
        // ════════════════════════════════════════════════════════════════

        // GET: LeaveRequest/ParentPending
        [RequireLogin]
        public ActionResult ParentPending()
        {
            int parentId = GetCurrentParentId();
            if (parentId == 0)
            {
                TempData["Error"] = "Please log in as a parent/guardian.";
                return RedirectToAction("Login", "Account");
            }

            var requests = db.LeaveRequests
                .Include(l => l.Student)
                .Include(l => l.Residence)
                .Where(l => l.ParentId == parentId
                            && l.Status == LeaveRequestStatus.PendingParentApproval
                            && !l.IsArchived)
                .OrderBy(l => l.DepartureDateTime)
                .ToList();

            return View(requests);
        }

        // GET: LeaveRequest/ParentHistory
        [RequireLogin]
        public ActionResult ParentHistory()
        {
            int parentId = GetCurrentParentId();
            if (parentId == 0)
            {
                TempData["Error"] = "Please log in as a parent/guardian.";
                return RedirectToAction("Login", "Account");
            }

            var requests = db.LeaveRequests
                .Include(l => l.Student)
                .Include(l => l.HouseMaster)
                .Where(l => l.ParentId == parentId && !l.IsArchived)
                .OrderByDescending(l => l.SubmittedAt)
                .ToList();

            return View(requests);
        }

        // POST: LeaveRequest/ParentDecision
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public ActionResult ParentDecision(int id, bool approve, string comments)
        {
            int parentId = GetCurrentParentId();
            var request = db.LeaveRequests.Include(l => l.HouseMaster).FirstOrDefault(l => l.LeaveRequestId == id);
            if (request == null) return HttpNotFound();

            if (parentId == 0 || request.ParentId != parentId)
            {
                TempData["Error"] = "You are not authorised to act on this request.";
                return RedirectToAction("ParentPending");
            }

            if (request.Status != LeaveRequestStatus.PendingParentApproval)
            {
                TempData["Error"] = "This request is no longer awaiting your approval.";
                return RedirectToAction("ParentPending");
            }

            request.ParentDecision = approve ? "Approved" : "Rejected";
            request.ParentComments = comments;
            request.ParentDecisionAt = DateTime.Now;

            if (!approve)
            {
                request.Status = LeaveRequestStatus.Rejected;
                TempData["Success"] = "Leave request rejected. The student has been notified.";
            }
            else if (request.HouseMasterId.HasValue)
            {
                request.Status = LeaveRequestStatus.PendingHouseMasterApproval;
                TempData["Success"] = "Leave request approved and forwarded to the house master.";
            }
            else
            {
                // Safety net: residence lost its house master between submission and approval.
                request.Status = LeaveRequestStatus.Rejected;
                request.HouseMasterComments = "Auto-rejected: no house master is currently assigned to the student's residence.";
                TempData["Error"] = "You approved this request, but no house master is currently linked to the student's residence, so it could not be forwarded. Please contact administration.";
            }

            db.SaveChanges();
            return RedirectToAction("ParentPending");
        }

        // ════════════════════════════════════════════════════════════════
        // HOUSE MASTER: review + decide (scoped to the house master's own
        // residence(s); admins can see and act on all requests)
        // ════════════════════════════════════════════════════════════════

        // GET: LeaveRequest/HouseMasterPending
        [AdminOrHouseMasterOnly]
        public ActionResult HouseMasterPending()
        {
            var query = db.LeaveRequests
                .Include(l => l.Student)
                .Include(l => l.Residence)
                .Include(l => l.Parent)
                .Where(l => l.Status == LeaveRequestStatus.PendingHouseMasterApproval && !l.IsArchived);

            if (!IsAdmin())
            {
                var houseMasterId = GetCurrentHouseMasterId();
                if (!houseMasterId.HasValue)
                {
                    TempData["Error"] = "No house master profile is linked to this account.";
                    return View(new List<LeaveRequest>());
                }
                query = query.Where(l => l.HouseMasterId == houseMasterId.Value);
            }

            var requests = query.OrderBy(l => l.DepartureDateTime).ToList();
            return View(requests);
        }

        // GET: LeaveRequest/HouseMasterHistory
        [AdminOrHouseMasterOnly]
        public ActionResult HouseMasterHistory()
        {
            var query = db.LeaveRequests
                .Include(l => l.Student)
                .Include(l => l.Residence)
                .Where(l => !l.IsArchived
                            && (l.Status == LeaveRequestStatus.Approved || l.Status == LeaveRequestStatus.Rejected));

            if (!IsAdmin())
            {
                var houseMasterId = GetCurrentHouseMasterId();
                if (!houseMasterId.HasValue) return View(new List<LeaveRequest>());
                query = query.Where(l => l.HouseMasterId == houseMasterId.Value);
            }

            var requests = query.OrderByDescending(l => l.HouseMasterDecisionAt).ToList();
            return View(requests);
        }

        // POST: LeaveRequest/HouseMasterDecision
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminOrHouseMasterOnly]
        public ActionResult HouseMasterDecision(int id, bool approve, string comments)
        {
            var request = db.LeaveRequests.Find(id);
            if (request == null) return HttpNotFound();

            var houseMasterId = GetCurrentHouseMasterId();
            bool authorised = IsAdmin() || (houseMasterId.HasValue && request.HouseMasterId == houseMasterId.Value);

            if (!authorised)
            {
                TempData["Error"] = "You are not authorised to act on this request. Only the house master of the student's residence (or an admin) may decide it.";
                return RedirectToAction("HouseMasterPending");
            }

            if (request.Status != LeaveRequestStatus.PendingHouseMasterApproval)
            {
                TempData["Error"] = "This request is no longer awaiting house master approval.";
                return RedirectToAction("HouseMasterPending");
            }

            request.HouseMasterDecision = approve ? "Approved" : "Rejected";
            request.HouseMasterComments = comments;
            request.HouseMasterDecisionAt = DateTime.Now;
            request.Status = approve ? LeaveRequestStatus.Approved : LeaveRequestStatus.Rejected;

            db.SaveChanges();
            TempData["Success"] = approve
                ? "Leave request approved. The student has been notified."
                : "Leave request rejected. The student has been notified.";
            return RedirectToAction("HouseMasterPending");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}