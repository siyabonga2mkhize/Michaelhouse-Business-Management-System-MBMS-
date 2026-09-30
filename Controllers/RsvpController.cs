using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // UC18 — Public RSVP form.
    //
    // No login required. The URL contains a numeric event id;
    // anyone with the link can respond.
    //
    // URL pattern:
    //   GET  /Rsvp/Event/5    → show form for event 5
    //   POST /Rsvp/Event/5    → record a response
    //   GET  /Rsvp/ThankYou/5 → confirmation page
    // ============================================================
    [AllowAnonymous]
    public class RsvpController : Controller
    {
        private readonly DBContextClass _db;

        public RsvpController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: /Rsvp/Event/5
        // ============================================================

        [HttpGet]
        [ActionName("Event")]
        public ActionResult ShowForm(int? id)
        {
            if (!id.HasValue) return MissingLink();

            var evt = _db.CafeteriaEvents
                .Include("Venue")
                .FirstOrDefault(e => e.Id == id.Value);

            if (evt == null) return MissingLink();

            // Is the event accepting RSVPs right now?
            ViewBag.IsOpen = evt.Status == EventStatus.RsvpOpen;

            // Determine why it's closed, if it is
            if (evt.Status == EventStatus.RsvpClosed
                || evt.Status == EventStatus.FeastPlanGenerated
                || evt.Status == EventStatus.Completed)
            {
                ViewBag.ClosedReason = "RSVPs for this event are closed.";
            }
            else if (evt.Status == EventStatus.Cancelled)
            {
                ViewBag.ClosedReason = "This event has been cancelled.";
            }
            else if (evt.Status == EventStatus.Draft || evt.Status == EventStatus.Confirmed)
            {
                ViewBag.ClosedReason = "RSVPs have not opened yet for this event.";
            }

            return View(evt);
        }

        // ============================================================
        // POST: /Rsvp/Event/5
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Event")]
        public ActionResult SubmitForm(int? id)
        {
            if (!id.HasValue) return MissingLink();

            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id.Value);
            if (evt == null) return MissingLink();

            if (evt.Status != EventStatus.RsvpOpen)
            {
                TempData["Error"] = "This event is not accepting RSVPs.";
                return RedirectToAction("Event", new { id = id.Value });
            }

            // ── Pull form values ──
            var name = Request.Form["ResponderName"];
            var email = Request.Form["ResponderEmail"];
            var phone = Request.Form["ResponderPhone"];
            var group = Request.Form["ResponderGroup"];
            var status = Request.Form["ResponseStatus"];
            var totalRaw = Request.Form["TotalAttendees"];
            var stdRaw = Request.Form["StandardCount"];
            var vegRaw = Request.Form["VegetarianCount"];
            var othRaw = Request.Form["OtherDietCount"];
            var notes = Request.Form["DietaryNotes"];
            var comments = Request.Form["Comments"];

            // ── Validation ──
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Please enter your name.";
                return RedirectToAction("Event", new { id = id.Value });
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                status = "Attending";
            }

            int total = 0, std = 0, veg = 0, oth = 0;
            int.TryParse(totalRaw, out total);
            int.TryParse(stdRaw, out std);
            int.TryParse(vegRaw, out veg);
            int.TryParse(othRaw, out oth);

            if (total < 0) total = 0;
            if (std < 0) std = 0;
            if (veg < 0) veg = 0;
            if (oth < 0) oth = 0;

            if (status == "Attending")
            {
                if (total < 1)
                {
                    TempData["Error"] = "Please tell us how many are attending.";
                    return RedirectToAction("Event", new { id = id.Value });
                }

                if (std + veg + oth > total)
                {
                    TempData["Error"] = "Meal counts exceed the total attendees.";
                    return RedirectToAction("Event", new { id = id.Value });
                }

                // Remaining meals count as standard
                if (std + veg + oth < total)
                {
                    std += total - (std + veg + oth);
                }
            }
            else
            {
                total = 0;
                std = 0;
                veg = 0;
                oth = 0;
            }

            // ── Persist ──
            var rsvp = new EventRsvp
            {
                EventId = id.Value,
                ResponderName = name.Trim(),
                ResponderEmail = (email ?? "").Trim(),
                ResponderPhone = (phone ?? "").Trim(),
                ResponderGroup = string.IsNullOrWhiteSpace(group) ? "Guest" : group.Trim(),
                ResponseStatus = status,
                TotalAttendees = total,
                StandardCount = std,
                VegetarianCount = veg,
                OtherDietCount = oth,
                DietaryNotes = (notes ?? "").Trim(),
                Comments = (comments ?? "").Trim(),
                RespondedAt = DateTime.UtcNow
            };

            _db.EventRsvps.Add(rsvp);
            _db.SaveChanges();

            return RedirectToAction("ThankYou", new { id = id.Value });
        }

        // ============================================================
        // GET: /Rsvp/ThankYou/5
        // ============================================================

        [HttpGet]
        public ActionResult ThankYou(int? id)
        {
            if (!id.HasValue) return MissingLink();

            var evt = _db.CafeteriaEvents
                .Include("Venue")
                .FirstOrDefault(e => e.Id == id.Value);

            if (evt == null) return MissingLink();

            return View(evt);
        }

        // ============================================================
        // Fallback — someone hit an RSVP URL without a valid event
        // ============================================================

        private ActionResult MissingLink()
        {
            Response.StatusCode = 400;
            return Content(
                "<!DOCTYPE html><html><head><title>RSVP</title>" +
                "<style>body{font-family:sans-serif;padding:3rem;text-align:center;color:#555;}" +
                "h1{color:#C21E2E;font-family:Georgia,serif;}</style></head><body>" +
                "<h1>Link not found</h1>" +
                "<p>This RSVP link is missing or invalid.</p>" +
                "<p>Please check the URL you were given, or contact the Michaelhouse cafeteria.</p>" +
                "</body></html>",
                "text/html");
        }
    }
}