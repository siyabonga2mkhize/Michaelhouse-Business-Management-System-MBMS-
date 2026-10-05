using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // UC18 Mobile — logged-in RSVP ("My Events")
    //
    // Same as the web pages Rsvp/MyEvents and Rsvp/Event for a
    // logged-in student, parent or staff member: attending or not,
    // their meal when the event offers a choice, dietary needs
    // (parents / staff; students use their profile) and guests
    // (parents / staff, when the event allows). Who they are comes
    // from the login session; every answer is checked by
    // EventRsvpService.SubmitInviteeResponse.
    //
    //   GET  /api/my-events                    → my invitations
    //   GET  /api/my-events/{id}               → one event's RSVP form
    //   POST /api/my-events/{id}/meal-options  { dietary } → meals that suit
    //   POST /api/my-events/{id}               { attending, menuItemId,
    //                                            dietary, guests, comments }
    //
    // (RsvpApiController is the anonymous guest form and stays as is.)
    // ============================================================
    [RoutePrefix("api/my-events")]
    [Authorize]
    public class MyEventsApiController : Controller
    {
        private readonly DBContextClass _db;
        private readonly EventRsvpService _rsvp;

        public MyEventsApiController()
        {
            _db = new DBContextClass();
            _rsvp = new EventRsvpService(_db);
        }

        // Scheduled openings first, like RsvpController
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
        // GET /api/my-events
        // ============================================================

        [HttpGet]
        [Route("")]
        public JsonResult Index()
        {
            int userId = CurrentUserId();
            if (userId <= 0) return Json(new { ok = false, error = "Please log in again." }, JsonRequestBehavior.AllowGet);

            var invitations = _rsvp.GetInvitations(userId, CurrentRole());

            return Json(new
            {
                ok = true,
                events = invitations.Select(inv => new
                {
                    eventId = inv.Event.Id,
                    eventName = inv.Event.EventName,
                    dateLabel = inv.Event.EventDate.ToString("dddd dd MMMM yyyy"),
                    timeLabel = inv.Event.StartTime.ToString(@"hh\:mm") + "–" + inv.Event.EndTime.ToString(@"hh\:mm"),
                    venueName = inv.Event.Venue != null ? inv.Event.Venue.Name : "",
                    isOpen = inv.Status.AcceptingResponses,
                    isScheduled = inv.Status.State == RsvpState.ScheduledToOpen,
                    statusLabel = "RSVP " + inv.Status.Label + " — " + inv.Status.Detail,
                    hasResponded = inv.Response != null,
                    answer = inv.Response == null ? null
                        : inv.Response.ResponseStatus == EventRsvpService.ResponseAttending ? "You're attending" : "You're not attending"
                }).ToList()
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // GET /api/my-events/5
        // Everything Rsvp/Event shows a logged-in invitee
        // ============================================================

        [HttpGet]
        [Route("{id:int}")]
        public JsonResult Details(int id)
        {
            var evt = _db.CafeteriaEvents.Include("Venue").FirstOrDefault(e => e.Id == id);
            if (evt == null) return Json(new { ok = false, error = "Event not found." }, JsonRequestBehavior.AllowGet);

            var invitee = CurrentInvitee(evt);
            if (invitee == null)
            {
                return Json(new { ok = false, error = "Your account can't RSVP for itself. Please use the guest RSVP form on the website." },
                    JsonRequestBehavior.AllowGet);
            }

            var status = _rsvp.GetStatus(evt);
            var response = _rsvp.FindInviteeResponse(evt.Id, invitee.UserId);

            // Students: their profile. Parents / staff: what they gave on
            // their RSVP to this event (same as RsvpController.PopulateInvitee)
            StudentDietaryProfile profile = invitee.Type == AttendeeType.Student
                ? invitee.ProfileDietary
                : EventRsvpService.RsvpDietary(response);

            var mealOptions = evt.OffersMealChoice ? _rsvp.MealOptionsFor(evt, profile) : new List<RsvpMealOption>();

            return Json(new
            {
                ok = true,
                eventId = evt.Id,
                eventName = evt.EventName,
                dateLabel = evt.EventDate.ToString("dddd, dd MMMM yyyy"),
                timeLabel = evt.StartTime.ToString(@"hh\:mm") + " – " + evt.EndTime.ToString(@"hh\:mm"),
                venueName = evt.Venue != null ? evt.Venue.Name : "",
                rsvpDeadline = evt.RsvpDeadline.HasValue ? evt.RsvpDeadline.Value.ToString("dddd, dd MMMM yyyy") : null,

                isOpen = status.AcceptingResponses,
                closedReason = ClosedReason(evt, status),

                invitee = new
                {
                    name = invitee.Name,
                    type = invitee.Type,
                    isInvited = invitee.IsInvited,
                    notInvitedReason = invitee.NotInvitedReason,
                    canBringGuests = invitee.CanBringGuests
                },
                maxGuests = invitee.CanBringGuests ? evt.MaxGuestsPerInvitee : 0,
                offersMealChoice = evt.OffersMealChoice && mealOptions.Count > 0,

                response = response == null ? null : new
                {
                    attending = response.ResponseStatus == EventRsvpService.ResponseAttending,
                    menuItemId = response.MenuItemId,
                    comments = response.Comments,
                    guests = response.Guests.OrderBy(g => g.Id).Select(g => new
                    {
                        dietaryPreference = g.DietaryPreference,
                        dietaryNotes = g.DietaryNotes,
                        menuItemId = g.MenuItemId
                    }).ToList()
                },

                // Students: shown read-only
                profileSummary = new
                {
                    preference = profile.PreferenceSummary,
                    allergies = profile.AllergySummary,
                    medical = profile.MedicalRestrictionSummary
                },

                // Parents / staff: their answers last time, to pre-fill
                dietary = invitee.Type == AttendeeType.Student ? null : new
                {
                    dietaryPreference = profile.Preference ?? DietaryProfileService.PreferenceNone,
                    dietaryPreferenceOther = profile.PreferenceOther,
                    selectedAllergies = profile.Allergies.Concat(profile.OtherAllergies.Count > 0
                        ? new[] { DietaryProfileService.OtherCode } : new string[0]).ToList(),
                    otherAllergies = string.Join(", ", profile.OtherAllergies),
                    selectedMedicalRestrictions = profile.MedicalRestrictions.ToList(),
                    medicalRestrictionOther = profile.MedicalRestrictionOther,
                    dietaryNotes = response != null ? response.DietaryNotes : null
                },

                // The same checklists as the web form
                options = new
                {
                    preferences = Options(DietaryProfileService.PreferenceOptions),
                    allergies = Options(DietaryProfileService.AllergyOptions),
                    medicalRestrictions = Options(DietaryProfileService.MedicalRestrictionOptions)
                },

                meals = MealsJson(mealOptions)
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // POST /api/my-events/5/meal-options
        // Which meals suit the dietary needs a parent / staff member
        // has entered so far (like the web's live check). The same
        // check runs again on submit.
        // ============================================================

        [HttpPost]
        [Route("{id:int}/meal-options")]
        public JsonResult MealOptions(int id)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            var invitee = evt != null ? CurrentInvitee(evt) : null;

            if (invitee == null || !invitee.IsInvited)
            {
                return Json(new { ok = false, error = "You're not invited to this event." });
            }

            StudentDietaryProfile profile = invitee.ProfileDietary;
            if (invitee.Type != AttendeeType.Student)
            {
                var data = ReadBody<MealOptionsRequestModel>();
                var form = data != null && data.Dietary != null ? data.Dietary : new DietaryProfileViewModel();
                var given = new StudentProfile();
                if (new DietaryProfileService(_db).Validate(form).Count == 0)
                {
                    DietaryProfileService.ApplyTo(given, form);
                }
                profile = DietaryProfileService.FromStudentProfile(given);
            }

            var meals = evt.OffersMealChoice ? _rsvp.MealOptionsFor(evt, profile) : new List<RsvpMealOption>();
            return Json(new { ok = true, meals = MealsJson(meals) });
        }

        // ============================================================
        // POST /api/my-events/5
        // Body: { attending, menuItemId, comments,
        //         dietary: { dietaryPreference, ... },
        //         guests: [ { dietaryPreference, dietaryNotes, menuItemId } ] }
        // ============================================================

        [HttpPost]
        [Route("{id:int}")]
        public JsonResult Submit(int id)
        {
            int userId = CurrentUserId();
            if (userId <= 0) return Json(new { ok = false, error = "Please log in again." });

            var data = ReadBody<InviteeRsvpInput>();
            if (data == null) return Json(new { ok = false, error = "Please answer the RSVP." });

            // Never more rows than could be valid; the service checks the event's limit
            if (data.Guests != null && data.Guests.Count > 20) data.Guests = data.Guests.Take(20).ToList();

            var result = _rsvp.SubmitInviteeResponse(id, userId, CurrentRole(), data);

            if (!result.Success)
            {
                return Json(new
                {
                    ok = false,
                    error = "Please check your RSVP: " + string.Join(" ", result.Errors),
                    errors = result.Errors
                });
            }

            return Json(new { ok = true, attending = result.Attending, message = result.Message });
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static object Options(IEnumerable<DietaryOption> options)
        {
            return options.Select(o => new
            {
                code = o.Code,
                // Same wording as the web form
                label = o.Code == DietaryProfileService.PreferenceNone ? "No special requirement" : o.Label
            }).ToList();
        }

        private static object MealsJson(IEnumerable<RsvpMealOption> meals)
        {
            return meals.Select(m => new
            {
                menuItemId = m.MenuItemId,
                name = m.Name,
                description = m.Description,
                allergens = m.Allergens,
                suitableFor = m.SuitableFor,
                isSuitable = m.IsSuitable,
                unsuitableReason = m.UnsuitableReason
            }).ToList();
        }

        // Same reasons as RsvpController.ShowForm
        private static string ClosedReason(CafeteriaEvent evt, RsvpStatusInfo status)
        {
            if (status.AcceptingResponses) return null;
            if (evt.Status == EventStatus.Cancelled) return "This event has been cancelled.";
            if (status.State == RsvpState.ScheduledToOpen)
                return "RSVPs for this event open on " + evt.RsvpOpensAt.Value.ToString("dddd dd MMMM 'at' HH:mm") + ".";
            if (status.State == RsvpState.NotOpen) return "RSVPs have not opened yet for this event.";
            if (evt.Status == EventStatus.RsvpOpen) return "The RSVP deadline for this event has passed.";
            return "RSVPs for this event are closed.";
        }

        private int CurrentUserId()
        {
            int userId;
            if (Session == null || Session["UserId"] == null || !int.TryParse(Session["UserId"].ToString(), out userId)) return 0;
            return userId;
        }

        private string CurrentRole()
        {
            return Session != null ? Session["UserRole"] as string : null;
        }

        private Invitee CurrentInvitee(CafeteriaEvent evt)
        {
            int userId = CurrentUserId();
            return userId > 0 ? _rsvp.ResolveInvitee(evt, userId, CurrentRole()) : null;
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

    public class MealOptionsRequestModel
    {
        public DietaryProfileViewModel Dietary { get; set; }
    }
}
