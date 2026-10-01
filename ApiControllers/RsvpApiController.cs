using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
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
    // UC18 Mobile — RSVP endpoints
    //
    //  GET  /api/rsvp/current-event  → returns the open RSVP event
    //  POST /api/rsvp/submit         → creates an EventRsvp row
    //
    // [AllowAnonymous] — the mobile app doesn't log in for RSVP.
    // ============================================================
    [AllowAnonymous]
    [RoutePrefix("api/rsvp")]
    public class RsvpApiController : Controller
    {
        private readonly DBContextClass _db;

        public RsvpApiController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET /api/rsvp/current-event
        // Returns the most recent event with Status = RsvpOpen.
        // ============================================================

        [HttpGet]
        [Route("current-event")]
        public JsonResult GetCurrentEvent()
        {
            var rsvp = new EventRsvpService(_db);
            rsvp.OpenDueRsvps(null);

            // Most recently opened event still accepting responses
            var evt = _db.CafeteriaEvents
                .Include("Venue")
                .Where(e => e.Status == EventStatus.RsvpOpen)
                .OrderByDescending(e => e.RsvpOpenedAt)
                .ToList()
                .FirstOrDefault(e => rsvp.GetStatus(e).AcceptingResponses);

            if (evt == null)
            {
                return Json(new
                {
                    ok = false,
                    error = "There are no events currently accepting RSVPs."
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                ok = true,
                eventId = evt.Id,
                eventName = evt.EventName,
                eventDate = evt.EventDate.ToString("yyyy-MM-dd"),
                eventDateLabel = evt.EventDate.ToString("dddd, dd MMMM yyyy"),
                startTime = evt.StartTime.ToString(@"hh\:mm"),
                endTime = evt.EndTime.ToString(@"hh\:mm"),
                venueName = evt.Venue != null ? evt.Venue.Name : "",
                rsvpDeadline = evt.RsvpDeadline.HasValue
                    ? evt.RsvpDeadline.Value.ToString("dddd, dd MMMM yyyy")
                    : "no deadline",
                expectedHeadcount = evt.ExpectedHeadcount
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // POST /api/rsvp/submit
        // Body: JSON matching RsvpSubmitModel below
        // ============================================================

        [HttpPost]
        [Route("submit")]
        public JsonResult Submit()
        {
            try
            {
                // Read the raw JSON body
                string body;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    body = reader.ReadToEnd();
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    return Json(new { ok = false, error = "Empty request body." });
                }

                var data = JsonConvert.DeserializeObject<RsvpSubmitModel>(body);

                if (data == null)
                {
                    return Json(new { ok = false, error = "Could not parse JSON." });
                }

                // ── Validation ──
                if (data.EventId <= 0)
                {
                    return Json(new { ok = false, error = "Missing event." });
                }

                if (string.IsNullOrWhiteSpace(data.ResponderName))
                {
                    return Json(new { ok = false, error = "Please enter your name." });
                }

                var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == data.EventId);
                if (evt == null)
                {
                    return Json(new { ok = false, error = "Event not found." });
                }

                if (!new EventRsvpService(_db).GetStatus(evt).AcceptingResponses)
                {
                    return Json(new { ok = false, error = "This event is no longer accepting RSVPs." });
                }

                // ── Normalise meal counts ──
                string status = string.IsNullOrWhiteSpace(data.ResponseStatus)
                    ? "Attending"
                    : data.ResponseStatus;

                int total = data.TotalAttendees;
                int std = data.StandardCount;
                int veg = data.VegetarianCount;
                int oth = data.OtherDietCount;

                if (total < 0) total = 0;
                if (std < 0) std = 0;
                if (veg < 0) veg = 0;
                if (oth < 0) oth = 0;

                if (status == "Attending")
                {
                    if (total < 1)
                    {
                        return Json(new { ok = false, error = "Please tell us how many are attending." });
                    }

                    if (std + veg + oth > total)
                    {
                        return Json(new { ok = false, error = "Meal counts exceed total attendees." });
                    }

                    // Remainder = standard
                    if (std + veg + oth < total)
                    {
                        std += total - (std + veg + oth);
                    }
                }
                else
                {
                    // Declined / Maybe → zero out attendee counts
                    total = 0;
                    std = 0;
                    veg = 0;
                    oth = 0;
                }

                // ── Create the row ──
                var rsvp = new EventRsvp
                {
                    EventId = evt.Id,
                    ResponderName = data.ResponderName.Trim(),
                    ResponderEmail = (data.ResponderEmail ?? "").Trim(),
                    ResponderPhone = (data.ResponderPhone ?? "").Trim(),
                    ResponderGroup = Michaelhouse.Controllers.RsvpController.GuestGroup(data.ResponderGroup),
                    ResponseStatus = status,
                    TotalAttendees = total,
                    StandardCount = std,
                    VegetarianCount = veg,
                    OtherDietCount = oth,
                    DietaryNotes = (data.DietaryNotes ?? "").Trim(),
                    Comments = (data.Comments ?? "").Trim(),
                    RespondedAt = DateTime.UtcNow
                };

                _db.EventRsvps.Add(rsvp);
                _db.SaveChanges();

                return Json(new
                {
                    ok = true,
                    rsvpId = rsvp.Id,
                    message = "Thank you. Your RSVP has been recorded."
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = "Server error: " + ex.Message });
            }
        }
    }

    // ============================================================
    // Request body shape for POST /api/rsvp/submit
    // ============================================================
    public class RsvpSubmitModel
    {
        public int EventId { get; set; }
        public string ResponderName { get; set; }
        public string ResponderEmail { get; set; }
        public string ResponderPhone { get; set; }
        public string ResponderGroup { get; set; }
        public string ResponseStatus { get; set; }
        public int TotalAttendees { get; set; }
        public int StandardCount { get; set; }
        public int VegetarianCount { get; set; }
        public int OtherDietCount { get; set; }
        public string DietaryNotes { get; set; }
        public string Comments { get; set; }
    }
}