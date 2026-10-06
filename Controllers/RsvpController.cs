using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
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
    // Invited students, parents and staff RSVP while logged in
    // (EventRsvpService.SubmitInviteeResponse): attending or not,
    // their meal when the event offers a choice, and — parents and
    // staff, when allowed — their guests. Students' dietary needs
    // come from their profile; parents and staff give theirs here.
    // Anyone else uses the public form and counts as a guest.
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
        private readonly EventRsvpService _rsvp;

        public RsvpController()
        {
            _db = new DBContextClass();
            _rsvp = new EventRsvpService(_db);
        }

        // Scheduled openings first, so the form reflects them
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            _rsvp.OpenDueRsvps(eventId => Url.Action("Event", "Rsvp", new { id = eventId }, Request.Url.Scheme));
            base.OnActionExecuting(filterContext);
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
            var status = _rsvp.GetStatus(evt);
            ViewBag.IsOpen = status.AcceptingResponses;

            // Determine why it's closed, if it is
            if (evt.Status == EventStatus.Cancelled)
            {
                ViewBag.ClosedReason = "This event has been cancelled.";
            }
            else if (status.State == RsvpState.ScheduledToOpen)
            {
                ViewBag.ClosedReason = "RSVPs for this event open on "
                    + evt.RsvpOpensAt.Value.ToString("dddd dd MMMM 'at' HH:mm") + ".";
            }
            else if (status.State == RsvpState.NotOpen)
            {
                ViewBag.ClosedReason = "RSVPs have not opened yet for this event.";
            }
            else if (evt.Status == EventStatus.RsvpOpen && !status.AcceptingResponses)
            {
                ViewBag.ClosedReason = "The RSVP deadline for this event has passed.";
            }
            else if (!status.AcceptingResponses)
            {
                ViewBag.ClosedReason = "RSVPs for this event are closed.";
            }

            // Logged-in student / parent / staff: answer for themselves
            var invitee = CurrentInvitee(evt);
            if (invitee != null)
            {
                PopulateInvitee(evt, invitee, null, null);
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

            if (!_rsvp.GetStatus(evt).AcceptingResponses)
            {
                TempData["Error"] = "This event is not accepting RSVPs.";
                return RedirectToAction("Event", new { id = id.Value });
            }

            // Logged-in student / parent / staff
            var invitee = CurrentInvitee(evt);
            if (invitee != null)
            {
                var input = ReadInviteeInput(evt);
                var result = _rsvp.SubmitInviteeResponse(id.Value, invitee.UserId, CurrentRole(), input);

                if (!result.Success)
                {
                    // Show the form again with what they entered
                    var withVenue = _db.CafeteriaEvents.Include("Venue").First(e => e.Id == id.Value);
                    ViewBag.IsOpen = true;
                    ViewBag.FormErrors = result.Errors;
                    PopulateInvitee(withVenue, invitee, null, input);
                    return View("Event", withVenue);
                }

                TempData["Success"] = result.Message;
                return RedirectToAction("ThankYou", new { id = id.Value });
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
                ResponderGroup = GuestGroup(group),
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

            var invitee = CurrentInvitee(evt);
            if (invitee != null)
            {
                var response = _rsvp.FindInviteeResponse(evt.Id, invitee.UserId);
                bool attending = response != null && response.ResponseStatus == EventRsvpService.ResponseAttending;

                ViewBag.CalendarUrl = attending ? EventRsvpService.GoogleCalendarUrl(evt, RsvpUrl(evt.Id)) : null;
                ViewBag.Declined = response != null && !attending;
            }

            return View(evt);
        }

        // ============================================================
        // GET: /Rsvp/MyEvents
        // The events the logged-in student / parent / staff member is
        // invited to — a way in that doesn't depend on notifications.
        // ============================================================

        [HttpGet]
        public ActionResult MyEvents()
        {
            int userId;
            if (Session == null || Session["UserId"] == null || !int.TryParse(Session["UserId"].ToString(), out userId))
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("MyEvents", "Rsvp") });
            }

            return View(_rsvp.GetInvitations(userId, CurrentRole()));
        }

        // ============================================================
        // POST: /Rsvp/MealOptions/5
        // Which of the event's meals suit the dietary needs a parent
        // or staff member has ticked so far (live, on the form). The
        // same check runs again on submit.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MealOptions(int id)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            var invitee = evt != null ? CurrentInvitee(evt) : null;

            if (invitee == null || !invitee.IsInvited)
            {
                Response.StatusCode = 403;
                return Json(new { ok = false });
            }

            StudentDietaryProfile profile = invitee.ProfileDietary;
            if (invitee.Type != AttendeeType.Student)
            {
                var form = ReadDietaryForm();
                var given = new StudentProfile();
                if (new DietaryProfileService(_db).Validate(form).Count == 0)
                {
                    DietaryProfileService.ApplyTo(given, form);
                }
                profile = DietaryProfileService.FromStudentProfile(given);
            }

            return Json(new
            {
                ok = true,
                meals = _rsvp.MealOptionsFor(evt, profile).Select(m => new { id = m.MenuItemId, suitable = m.IsSuitable, reason = m.UnsuitableReason })
            });
        }

        // Students RSVP through their own login so their profile is
        // used; an anonymous "Student" response is kept as a guest
        public static string GuestGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group)) return "Guest";
            group = group.Trim();
            return string.Equals(group, EventRsvpService.GroupStudent, StringComparison.OrdinalIgnoreCase) ? "Guest" : group;
        }

        private string CurrentRole()
        {
            return Session != null ? Session["UserRole"] as string : null;
        }

        // The logged-in student / parent / staff member, or null
        // (not logged in, or a role that uses the public form)
        private Invitee CurrentInvitee(CafeteriaEvent evt)
        {
            if (Session == null || Session["UserId"] == null) return null;

            int userId;
            if (!int.TryParse(Session["UserId"].ToString(), out userId)) return null;

            return _rsvp.ResolveInvitee(evt, userId, CurrentRole());
        }

        private string RsvpUrl(int eventId)
        {
            return Url.Action("Event", "Rsvp", new { id = eventId }, Request.Url.Scheme);
        }

        // Everything the invitee form needs. "posted" is the input
        // being shown again after a validation error.
        private void PopulateInvitee(CafeteriaEvent evt, Invitee invitee, EventRsvp response, InviteeRsvpInput posted)
        {
            response = response ?? _rsvp.FindInviteeResponse(evt.Id, invitee.UserId);

            ViewBag.Invitee = invitee;
            ViewBag.Response = response;

            // Parents / staff: their dietary needs — as just posted, or
            // from their RSVP to this event
            DietaryProfileViewModel dietaryForm = null;
            StudentDietaryProfile profile = invitee.ProfileDietary;

            if (invitee.Type != AttendeeType.Student)
            {
                if (posted != null && posted.Dietary != null)
                {
                    dietaryForm = posted.Dietary;
                    var given = new StudentProfile();
                    if (new DietaryProfileService(_db).Validate(dietaryForm).Count == 0)
                        DietaryProfileService.ApplyTo(given, dietaryForm);
                    profile = DietaryProfileService.FromStudentProfile(given);
                }
                else
                {
                    profile = EventRsvpService.RsvpDietary(response);
                    dietaryForm = new DietaryProfileViewModel
                    {
                        DietaryPreference = profile.Preference,
                        DietaryPreferenceOther = profile.PreferenceOther,
                        SelectedAllergies = profile.Allergies.ToList(),
                        OtherAllergies = string.Join(", ", profile.OtherAllergies),
                        SelectedMedicalRestrictions = profile.MedicalRestrictions.ToList(),
                        MedicalRestrictionOther = profile.MedicalRestrictionOther,
                        DietaryNotes = response != null ? response.DietaryNotes : null
                    };
                    if (profile.OtherAllergies.Count > 0) dietaryForm.SelectedAllergies.Add(DietaryProfileService.OtherCode);
                }
            }

            ViewBag.DietaryForm = dietaryForm;
            ViewBag.Dietary = profile;
            ViewBag.MealOptions = evt.OffersMealChoice ? _rsvp.MealOptionsFor(evt, profile) : new List<RsvpMealOption>();

            // What to pre-select
            ViewBag.FormAttending = posted != null ? (bool?)posted.Attending
                : response != null ? (bool?)(response.ResponseStatus == EventRsvpService.ResponseAttending) : null;
            ViewBag.FormMeal = posted != null ? posted.MenuItemId : response != null ? response.MenuItemId : null;
            ViewBag.FormComments = posted != null ? posted.Comments : response != null ? response.Comments : "";
            ViewBag.FormGuests = posted != null
                ? posted.Guests ?? new List<GuestInput>()
                : response != null
                    ? response.Guests.OrderBy(g => g.Id).Select(g => new GuestInput { DietaryPreference = g.DietaryPreference, DietaryNotes = g.DietaryNotes, MenuItemId = g.MenuItemId, CulturalFavoriteDish = g.CulturalFavoriteDish }).ToList()
                    : new List<GuestInput>();

            bool attending = response != null && response.ResponseStatus == EventRsvpService.ResponseAttending;
            ViewBag.CalendarUrl = attending ? EventRsvpService.GoogleCalendarUrl(evt, RsvpUrl(evt.Id)) : null;
        }

        private DietaryProfileViewModel ReadDietaryForm()
        {
            return new DietaryProfileViewModel
            {
                DietaryPreference = Request.Form["DietaryPreference"],
                DietaryPreferenceOther = Request.Form["DietaryPreferenceOther"],
                SelectedAllergies = (Request.Form.GetValues("SelectedAllergies") ?? new string[0]).ToList(),
                OtherAllergies = Request.Form["OtherAllergies"],
                SelectedMedicalRestrictions = (Request.Form.GetValues("SelectedMedicalRestrictions") ?? new string[0]).ToList(),
                MedicalRestrictionOther = Request.Form["MedicalRestrictionOther"],
                DietaryNotes = Request.Form["DietaryNotes"]
            };
        }

        private InviteeRsvpInput ReadInviteeInput(CafeteriaEvent evt)
        {
            var input = new InviteeRsvpInput
            {
                Attending = Request.Form["ResponseStatus"] == EventRsvpService.ResponseAttending,
                Dietary = ReadDietaryForm(),
                MenuItemId = ParseInt(Request.Form["MenuItemId"]),
                Comments = Request.Form["Comments"],
                Guests = new List<GuestInput>()
            };

            // Never read more rows than could be valid; the service
            // checks the event's limit
            int guestCount = ParseInt(Request.Form["GuestCount"]) ?? 0;
            guestCount = Math.Max(0, Math.Min(guestCount, 20));

            for (int i = 0; i < guestCount; i++)
            {
                input.Guests.Add(new GuestInput
                {
                    DietaryPreference = Request.Form["GuestPref_" + i],
                    DietaryNotes = Request.Form["GuestNotes_" + i],
                    MenuItemId = ParseInt(Request.Form["GuestMeal_" + i]),
                    CulturalFavoriteDish = Request.Form["GuestFavourite_" + i]
                });
            }

            return input;
        }

        private static int? ParseInt(string raw)
        {
            int value;
            return int.TryParse(raw, out value) ? (int?)value : null;
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