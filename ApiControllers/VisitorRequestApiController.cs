using Michaelhouse.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Mobile — Request Visitor Access (Student)
    //
    //   GET  /api/visitor-requests/form   access zones, defaults, my visits
    //   POST /api/visitor-requests        the visitor's details and visit
    //
    // The student requests a visit to themselves. Same checks as the
    // public web form VisitorAccess/Create: required fields, start
    // before end, closed weekends refused, campus rules (strict ones
    // refuse, others are flagged for the house master) and zone
    // safeguarding (non-parents can't use the House Common Room).
    //
    // Unlike the public form, a student's request is NEVER auto-approved
    // (even for a parent/guardian): it always waits for the house master
    // (VisitorAccess/HousemasterQueue on the website), who approves it
    // there — that's when the gate pass is emailed to the visitor.
    // ============================================================
    [RoutePrefix("api/visitor-requests")]
    [Authorize(Roles = "Student")]
    public class VisitorRequestApiController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // Display names from VisitorAccessZone's [Display] attributes
        private static readonly Dictionary<VisitorAccessZone, string> ZoneNames = new Dictionary<VisitorAccessZone, string>
        {
            { VisitorAccessZone.PublicCampusGrounds, "Public Campus Grounds (Makan / Oval)" },
            { VisitorAccessZone.HouseCommonRoom, "House Common Room (Parents/Guardians Only)" },
            { VisitorAccessZone.RestrictedHouseBounds, "Restricted House Bounds" }
        };

        // ============================================================
        // GET /api/visitor-requests/form
        // ============================================================

        [HttpGet]
        [Route("form")]
        public JsonResult Form()
        {
            var student = CurrentStudent();
            if (student == null) return Json(new { ok = false, error = "Please log in as a student." }, JsonRequestBehavior.AllowGet);

            var visits = db.VisitorAccessRequests
                .Where(r => r.StudentId == student.StudentId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .ToList();

            return Json(new
            {
                ok = true,
                boardingHouse = BoardingHouseName(student),
                zones = ZoneNames.Select(z => new { value = z.Key.ToString(), label = z.Value }),
                // Same defaults as the web form: tomorrow, 14:00–16:00
                defaultDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
                defaultStart = "14:00",
                defaultEnd = "16:00",
                myVisits = visits.Select(r => new
                {
                    id = r.RequestId,
                    visitor = r.VisitorFullName,
                    relationship = r.RelationshipToBoy,
                    date = r.VisitDate.ToString("ddd dd MMM yyyy"),
                    time = r.StartTime.ToString(@"hh\:mm") + "–" + r.EndTime.ToString(@"hh\:mm"),
                    zone = ZoneNames[r.FinalAssignedZone ?? r.RequestedZone],
                    purpose = r.PurposeOfVisit,
                    status = r.Status.ToString(),
                    statusLabel = StatusLabel(r.Status),
                    remarks = r.HousemasterRemarks,
                    policyFlag = r.HasPolicyConflict ? r.PolicyFlagDetails : null,
                    // The pass is only valid once approved
                    gatePass = r.Status == VisitorRequestStatus.Approved || r.Status == VisitorRequestStatus.AutoApprovedWeekend
                        ? r.AccessGatePassCode
                        : null
                })
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // POST /api/visitor-requests — VisitorAccess/Create rules
        // ============================================================

        [HttpPost]
        [Route("")]
        public JsonResult Create()
        {
            var student = CurrentStudent();
            if (student == null) return Json(new { ok = false, error = "Please log in as a student." });

            var input = ReadBody<VisitorRequestInput>();
            if (input == null) return Json(new { ok = false, error = "The request could not be read." });

            // ── Model validation ([Required], [StringLength], [EmailAddress]) ──
            var errors = new List<string>();
            var name = (input.VisitorFullName ?? "").Trim();
            var phone = (input.VisitorPhone ?? "").Trim();
            var idNumber = (input.VisitorIdOrPassport ?? "").Trim();
            var relationship = (input.RelationshipToStudent ?? "").Trim();
            var email = (input.VisitorEmail ?? "").Trim();
            var purpose = (input.PurposeOfVisit ?? "").Trim();

            Required(errors, name, 100, "Visitor Full Name");
            Required(errors, phone, 15, "Visitor Phone Number");
            if (idNumber.Length > 13) errors.Add("The ID or Passport Number must be 13 characters or fewer.");
            Required(errors, relationship, 50, "Relationship to Student");
            Required(errors, email, 100, "Visitor Email Address");
            if (email.Length > 0 && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                errors.Add("The Visitor Email Address field is not a valid e-mail address.");
            Required(errors, purpose, 250, "Purpose of Visit");

            VisitorAccessZone zone;
            if (!Enum.TryParse(input.RequestedZone ?? "", out zone) || !Enum.IsDefined(typeof(VisitorAccessZone), zone))
                errors.Add("Choose the requested access zone.");

            if (!input.VisitDate.HasValue) errors.Add("The Visit Date field is required.");
            TimeSpan start, end;
            bool timesOk = TimeSpan.TryParse(input.StartTime ?? "", out start) & TimeSpan.TryParse(input.EndTime ?? "", out end);
            if (!timesOk) errors.Add("Choose the start and end time.");

            if (errors.Count > 0) return Json(new { ok = false, error = string.Join(" ", errors), errors });

            var visitDate = input.VisitDate.Value.Date;

            // ── Schedule ──
            if (start >= end)
                return Json(new { ok = false, error = "Invalid Schedule: Start Time must precede End Time." });

            // ── Closed weekend (auto-reject rule) ──
            bool isClosedWeekend = db.TermCalendars.Any(c => c.IsClosedWeekend && visitDate >= c.StartDate && visitDate <= c.EndDate);
            if (isClosedWeekend)
                return Json(new { ok = false, error = "Access Denied: The requested date falls on an official Closed Weekend." });

            // ── Campus rules ──
            var detectedConflicts = new List<string>();
            foreach (var rule in db.CampusRules.Where(r => r.IsActive).ToList())
            {
                bool overlaps = start < rule.EndTime && end > rule.StartTime;
                if (!overlaps) continue;

                if (rule.IsStrictBlock)
                    return Json(new { ok = false, error = string.Format("Access Denied: Requested window conflicts with {0} ({1:hh\\:mm} - {2:hh\\:mm}).", rule.RuleName, rule.StartTime, rule.EndTime) });

                detectedConflicts.Add(string.Format("{0} ({1:hh\\:mm} - {2:hh\\:mm})", rule.RuleName, rule.StartTime, rule.EndTime));
            }

            var request = new VisitorAccessRequest
            {
                StudentId = student.StudentId,
                BoardingHouseName = BoardingHouseName(student),
                VisitorFullName = name,
                VisitorPhone = phone,
                VisitorIdOrPassport = idNumber.Length == 0 ? null : idNumber,
                RelationshipToBoy = relationship,
                IsParentOrGuardian = input.IsParentOrGuardian,
                VisitorEmail = email,
                VisitDate = visitDate,
                StartTime = start,
                EndTime = end,
                RequestedZone = zone,
                PurposeOfVisit = purpose,
                HasPolicyConflict = detectedConflicts.Any(),
                PolicyFlagDetails = detectedConflicts.Any()
                    ? "Warning: Overlaps with " + string.Join(", ", detectedConflicts)
                    : "Complies with all campus schedule rules."
            };

            // ── Safeguarding: only parents/guardians in the House Common Room ──
            request.FinalAssignedZone = request.RequestedZone;
            string zoneMessage = "";
            if (!request.IsParentOrGuardian && request.RequestedZone == VisitorAccessZone.HouseCommonRoom)
            {
                request.FinalAssignedZone = VisitorAccessZone.PublicCampusGrounds;
                zoneMessage = " (Zone adjusted to Public Campus Grounds per safeguarding policy)";
            }

            request.AccessGatePassCode = "MH-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            request.CreatedAt = DateTime.Now;

            // Always reviewed by the house master when a student asks
            request.Status = VisitorRequestStatus.PendingHousemaster;

            db.VisitorAccessRequests.Add(request);
            db.SaveChanges();

            return Json(new
            {
                ok = true,
                id = request.RequestId,
                message = "Request submitted successfully. Pending Housemaster review." + zoneMessage +
                          " The gate pass is emailed to your visitor once it's approved."
            });
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static void Required(List<string> errors, string value, int max, string label)
        {
            if (value.Length == 0) errors.Add("The " + label + " field is required.");
            else if (value.Length > max) errors.Add("The " + label + " must be " + max + " characters or fewer.");
        }

        // Same as VisitorAccess/Create
        private string BoardingHouseName(Student student)
        {
            if (student.ResidenceId.HasValue)
            {
                var residence = db.Residences.Find(student.ResidenceId.Value);
                return residence != null ? residence.Name : "Main Campus";
            }
            return "Main Campus";
        }

        private static string StatusLabel(VisitorRequestStatus status)
        {
            switch (status)
            {
                case VisitorRequestStatus.PendingHousemaster: return "Waiting for house master";
                case VisitorRequestStatus.Approved: return "Approved";
                case VisitorRequestStatus.AutoApprovedWeekend: return "Approved";
                case VisitorRequestStatus.RejectedClosedWeekend: return "Rejected (closed weekend)";
                case VisitorRequestStatus.RejectedPolicyViolation: return "Rejected";
                case VisitorRequestStatus.Cancelled: return "Cancelled";
                default: return status.ToString();
            }
        }

        private Student CurrentStudent()
        {
            int id;
            if (Session["StudentId"] == null || !int.TryParse(Session["StudentId"].ToString(), out id)) return null;
            return db.Students.Find(id);
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

    public class VisitorRequestInput
    {
        public string VisitorFullName { get; set; }
        public string VisitorPhone { get; set; }
        public string VisitorIdOrPassport { get; set; }
        public string RelationshipToStudent { get; set; }
        public bool IsParentOrGuardian { get; set; }
        public string VisitorEmail { get; set; }
        public DateTime? VisitDate { get; set; }
        public string StartTime { get; set; }      // "14:00"
        public string EndTime { get; set; }        // "16:00"
        public string RequestedZone { get; set; }  // VisitorAccessZone name
        public string PurposeOfVisit { get; set; }
    }
}
