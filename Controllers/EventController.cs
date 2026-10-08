using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // Class allows Chef through so they can reach Queue + FeastPlan.
    // Coordinator-only actions re-restrict themselves individually below.
    [Authorize(Roles = "CafeteriaManager, Admin, Chef")]
    public class EventController : Controller
    {
        private readonly DBContextClass _db;
        private readonly EventRsvpService _rsvp;
        private readonly EventBuffetService _buffet;

        public EventController()
        {
            _db = new DBContextClass();
            _rsvp = new EventRsvpService(_db);
            _buffet = new EventBuffetService(_db, _rsvp);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // Scheduled RSVP openings and deadline closings are applied
        // before any event page shows a status (the background timer
        // normally got there first)
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            _rsvp.OpenDueRsvps(RsvpLink);
            _rsvp.CloseDueRsvps();
            base.OnActionExecuting(filterContext);
        }

        // ============================================================
        // GET: Event/Index  (Coordinator only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Index()
        {
            var today = SchoolClock.Today;

            var upcoming = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .Where(e => e.EventDate >= today && e.Status != EventStatus.Cancelled)
                .OrderBy(e => e.EventDate)
                .ToList();

            ViewBag.Upcoming = upcoming;
            ViewBag.RsvpStatuses = upcoming.ToDictionary(e => e.Id, e => _rsvp.GetStatus(e));

            ViewBag.Past = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .Where(e => e.EventDate < today || e.Status == EventStatus.Cancelled)
                .OrderByDescending(e => e.EventDate)
                .Take(20)
                .ToList();

            return View();
        }

        // ============================================================
        // GET: Event/Create  (Coordinator only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Create()
        {
            var model = new CafeteriaEvent
            {
                EventDate = SchoolClock.Today.AddDays(14),
                StartTime = new TimeSpan(12, 30, 0),
                EndTime = new TimeSpan(15, 0, 0),
                ExpectedHeadcount = 50,
                Status = EventStatus.Draft,
                InviteParents = true,
                InviteStaff = false,
                InviteStudents = false
            };

            PopulateForm(model, new Dictionary<int, decimal>());
            return View(model);
        }

        // ============================================================
        // POST: Event/Create  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Create([Bind(Include = "EventName,Description,EventType,EventDate,StartTime,EndTime,VenueId,ExpectedHeadcount,MenuTemplateId,SpecialInstructions,InternalNotes,InviteParents,InviteStaff,InviteStudents,RsvpOpensAt,RsvpDeadline,OffersMealChoice,AllowGuests,MaxGuestsPerInvitee")] CafeteriaEvent model)
        {
            ReadAudience(model);
            var meals = ReadBuffetMeals();

            Validate(model, null);

            if (!ModelState.IsValid)
            {
                PopulateForm(model, meals);
                return View(model);
            }

            model.EventDate = model.EventDate.Date;
            model.RsvpDeadline = model.RsvpDeadline.HasValue ? model.RsvpDeadline.Value.Date : (DateTime?)null;
            if (!model.AllowGuests) model.MaxGuestsPerInvitee = 0;
            model.Status = EventStatus.Confirmed;
            model.CreatedByUserId = ResolveUserId();
            model.CreatedAt = DateTime.UtcNow;

            _db.CafeteriaEvents.Add(model);
            _db.SaveChanges();

            _buffet.SetBuffetMeals(model, meals);

            TempData["Success"] = "Event \"" + model.EventName + "\" scheduled for "
                + model.EventDate.ToString("dd MMM yyyy") + ". " + AfterSaveMessage(model);
            return RedirectToAction("Details", new { id = model.Id });
        }

        // ============================================================
        // GET: Event/Edit/5  (Coordinator only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Edit(int id)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            if (!CanEdit(evt))
            {
                TempData["Error"] = "Cancelled, completed or past events can't be edited.";
                return RedirectToAction("Details", new { id = id });
            }

            var meals = _db.CafeteriaEventMenuItems
                .Where(b => b.EventId == id)
                .ToList()
                .ToDictionary(b => b.MenuItemId, b => b.QuantityPerGuest);

            PopulateForm(evt, meals);
            return View("Create", evt);
        }

        // ============================================================
        // POST: Event/Edit/5  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Edit(int id, [Bind(Include = "EventName,Description,EventType,EventDate,StartTime,EndTime,VenueId,ExpectedHeadcount,MenuTemplateId,SpecialInstructions,InternalNotes,InviteParents,InviteStaff,InviteStudents,RsvpOpensAt,RsvpDeadline,OffersMealChoice,AllowGuests,MaxGuestsPerInvitee")] CafeteriaEvent model)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            if (!CanEdit(evt))
            {
                TempData["Error"] = "Cancelled, completed or past events can't be edited.";
                return RedirectToAction("Details", new { id = id });
            }

            ReadAudience(model);
            var meals = ReadBuffetMeals();

            // Once RSVPs have opened, the opening time is history
            bool rsvpStarted = evt.Status != EventStatus.Confirmed && evt.Status != EventStatus.Draft;
            if (rsvpStarted)
            {
                model.RsvpOpensAt = evt.RsvpOpensAt;
            }

            model.Id = evt.Id;
            model.Status = evt.Status;
            Validate(model, evt);

            if (!ModelState.IsValid)
            {
                PopulateForm(model, meals);
                return View("Create", model);
            }

            evt.EventName = model.EventName;
            evt.Description = model.Description;
            evt.EventType = model.EventType;
            evt.EventDate = model.EventDate.Date;
            evt.StartTime = model.StartTime;
            evt.EndTime = model.EndTime;
            evt.VenueId = model.VenueId;
            evt.ExpectedHeadcount = model.ExpectedHeadcount;
            evt.MenuTemplateId = model.MenuTemplateId;
            evt.SpecialInstructions = model.SpecialInstructions;
            evt.InternalNotes = model.InternalNotes;
            evt.InviteParents = model.InviteParents;
            evt.InviteStaff = model.InviteStaff;
            evt.InviteStudents = model.InviteStudents;
            evt.InviteParentResidenceIds = model.InviteParentResidenceIds;
            evt.InviteStudentResidenceIds = model.InviteStudentResidenceIds;
            // What attendees have in their calendars
            bool detailsChanged = evt.EventName != model.EventName
                || evt.EventDate.Date != model.EventDate.Date
                || evt.StartTime != model.StartTime
                || evt.EndTime != model.EndTime
                || evt.VenueId != model.VenueId;

            evt.RsvpOpensAt = model.RsvpOpensAt;
            evt.RsvpDeadline = model.RsvpDeadline.HasValue ? model.RsvpDeadline.Value.Date : (DateTime?)null;
            evt.OffersMealChoice = model.OffersMealChoice;
            evt.AllowGuests = model.AllowGuests;
            evt.MaxGuestsPerInvitee = model.AllowGuests ? model.MaxGuestsPerInvitee : 0;
            evt.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            _buffet.SetBuffetMeals(evt, meals);

            int told = 0;
            if (detailsChanged)
            {
                var venue = evt.VenueId.HasValue ? _db.EventVenues.Find(evt.VenueId.Value) : null;
                told = _rsvp.NotifyAttendees(evt, string.Format(
                    "{0} has changed: now {1} {2:hh\\:mm}–{3:hh\\:mm}{4}. If it's in your Google Calendar, open your RSVP to add the updated event and remove the old one.",
                    evt.EventName, evt.EventDate.ToString("ddd dd MMM"), evt.StartTime, evt.EndTime,
                    venue != null ? ", " + venue.Name : ""));
            }

            TempData["Success"] = "Event updated. " + AfterSaveMessage(evt)
                + (told > 0 ? string.Format(" {0} attendee{1} told about the change.", told, told == 1 ? "" : "s") : "");
            return RedirectToAction("Details", new { id = id });
        }

        // ============================================================
        // POST: Event/CheckBuffet  (Coordinator only)
        // Live dietary coverage for the event form. Same check as on
        // save and on the Details page.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public JsonResult CheckBuffet(int? eventId, bool inviteStudents = false)
        {
            var houses = Request.Form.GetValues("InviteStudentHouses");
            var meals = ReadBuffetMeals().Keys.ToList();

            var report = _buffet.Preview(eventId, inviteStudents,
                houses != null && houses.Length > 0 ? string.Join(",", houses) : null, meals);

            return Json(new
            {
                basis = report.Basis,
                studentCount = report.StudentCount,
                noun = report.Noun,
                allCovered = report.AllGroupsCovered,
                warnings = report.Warnings,
                groups = report.Groups.Select(g => new
                {
                    label = g.Label,
                    students = g.StudentCount,
                    covered = g.IsCovered,
                    meals = g.SuitableMeals
                })
            });
        }

        // ============================================================
        // GET: Event/Details/5  (Coordinator only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Details(int id)
        {
            var evt = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate.Items.MenuItem")
                .Include("StaffAssignments")
                .Include("Rsvps")
                .FirstOrDefault(e => e.Id == id);

            if (evt == null) return HttpNotFound();

            ViewBag.RsvpStatus = _rsvp.GetStatus(evt);
            ViewBag.Coverage = _buffet.Analyse(evt, _buffet.SelectedMealIds(evt));
            ViewBag.Attendance = _rsvp.GetAttendance(id);
            ViewBag.Invited = _rsvp.GetInvitedSummary(evt);
            ViewBag.Requirements = _buffet.CalculateRequirements(evt);
            ViewBag.MealNames = _db.MenuItems.ToList().ToDictionary(m => m.Id, m => m.Name);
            ViewBag.UsesTemplateMenu = !_db.CafeteriaEventMenuItems.Any(b => b.EventId == id) && evt.MenuTemplateId.HasValue;
            ViewBag.CanEdit = CanEdit(evt);
            ViewBag.Houses = _db.Residences.ToList().ToDictionary(r => r.ResidenceId, r => r.Name);
            ViewBag.FeastPlan = new FeastPlanService(_db).LivePlanFor(id);   // UC19, or null

            return View(evt);
        }

        // ============================================================
        // POST: Event/Cancel/5  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Cancel(int id, string reason)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            evt.Status = EventStatus.Cancelled;
            evt.InternalNotes = (evt.InternalNotes ?? "")
                + "\n[CANCELLED " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")
                + "] " + (reason ?? "No reason given");
            evt.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            _rsvp.NotifyAttendees(evt, string.Format(
                "{0} on {1} has been cancelled. If you added it to your Google Calendar, please remove it.",
                evt.EventName, evt.EventDate.ToString("ddd dd MMM")));

            TempData["Success"] = "Event cancelled.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: Event/OpenRsvp/5  (Coordinator only)
        // Opens RSVPs now — earlier than scheduled, or when no opening
        // time was set. The deadline is optional if one is already set.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult OpenRsvp(int id, DateTime? rsvpDeadline)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            if (!_rsvp.GetStatus(evt).CanOpenNow)
            {
                TempData["Error"] = "RSVPs can't be opened for this event.";
                return RedirectToAction("Details", new { id = id });
            }

            if (rsvpDeadline.HasValue)
            {
                if (rsvpDeadline.Value.Date < SchoolClock.Today)
                {
                    TempData["Error"] = "RSVP deadline cannot be in the past.";
                    return RedirectToAction("Details", new { id = id });
                }

                if (rsvpDeadline.Value.Date > evt.EventDate.Date)
                {
                    TempData["Error"] = "RSVP deadline cannot be after the event.";
                    return RedirectToAction("Details", new { id = id });
                }
            }

            int notified;
            if (!_rsvp.OpenRsvp(evt, rsvpDeadline, RsvpLink, out notified))
            {
                TempData["Error"] = "RSVPs were already opened.";
                return RedirectToAction("Details", new { id = id });
            }

            TempData["Success"] = string.Format(
                "RSVPs are now open. {0} users notified. Share the link with anyone else.",
                notified);

            if (notified == 0)
            {
                TempData["Error"] = "Nobody was notified: none of the invited students, parents or staff has a login. "
                    + "Check who is invited (and the houses chosen) under Event Details.";
            }

            return RedirectToAction("Details", new { id = id });
        }

        // ============================================================
        // POST: Event/CloseRsvp/5  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult CloseRsvp(int id)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            if (evt.Status != EventStatus.RsvpOpen)
            {
                TempData["Error"] = "Only events with open RSVPs can be closed.";
                return RedirectToAction("Details", new { id = id });
            }

            if (!_rsvp.CloseRsvp(evt))
            {
                TempData["Error"] = "RSVPs were already closed.";
                return RedirectToAction("Details", new { id = id });
            }

            TempData["Success"] = string.Format(
                "RSVPs closed. {0} attending — the kitchen plan has been sent to the kitchen.",
                evt.GuaranteedHeadcount ?? 0);

            return RedirectToAction("Details", new { id = id });
        }

        // ============================================================
        // GET: Event/FeastPlan/5  (Coordinator + Chef)
        // ============================================================

        [HttpGet]
        public ActionResult FeastPlan(int id)
        {
            var evt = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .FirstOrDefault(e => e.Id == id);

            if (evt == null) return HttpNotFound();

            if (evt.Status != EventStatus.RsvpClosed
                && evt.Status != EventStatus.FeastPlanGenerated)
            {
                TempData["Error"] = "Close RSVPs first, then generate the feast plan.";
                return RedirectToAction("Details", new { id = id });
            }

            var vm = _buffet.BuildFeastPlan(evt);

            // Events closed before the automatic handoff existed
            if (evt.Status == EventStatus.RsvpClosed)
            {
                evt.Status = EventStatus.FeastPlanGenerated;
                evt.UpdatedAt = DateTime.UtcNow;
                _db.SaveChanges();

                _rsvp.NotifyKitchen(evt);
            }

            return View(vm);
        }

        // ============================================================
        // GET: Event/IssueIngredients/5 — the event's ingredients from
        // its kitchen plan (RSVPs), against stock.
        // POST: the Chef confirms what was used — stock goes down.
        // ============================================================

        [HttpGet]
        public ActionResult IssueIngredients(int id)
        {
            try
            {
                var vm = new KitchenIssueService(_db).ForEvent(id);
                ViewBag.PostUrl = Url.Action("IssueIngredients", "Event", new { id });
                ViewBag.BackUrl = Url.Action("FeastPlan", "Event", new { id });
                ViewBag.BackLabel = "Back to Kitchen Plan";
                ViewBag.CanSubmit = User.IsInRole("Chef") || User.IsInRole("Admin");
                return View("~/Views/Shared/KitchenIssue.cshtml", vm);
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("FeastPlan", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult IssueIngredients(int id, string notes)
        {
            var service = new KitchenIssueService(_db);

            try
            {
                var vm = service.ForEvent(id);
                var quantities = KitchenIssueService.ReadQuantities(k => Request.Form[k], vm);
                service.IssueEvent(id, quantities, notes, ResolveUserId());

                TempData["Success"] = "Event ingredients issued — stock updated.";
                return RedirectToAction("FeastPlan", new { id });
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("IssueIngredients", new { id });
            }
        }

        // ============================================================
        // GET: Event/Queue  (Coordinator + Chef)
        // ============================================================

        [HttpGet]
        public ActionResult Queue()
        {
            var today = DateTime.Today;

            var events = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .Where(e => e.EventDate >= today)
                .Where(e => e.Status == EventStatus.FeastPlanGenerated
                         || e.Status == EventStatus.InProgress)
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.StartTime)
                .ToList();

            // Per event: its approved UC19 feast plan (if any) and
            // whether the ingredients have already been issued
            var ids = events.Select(e => e.Id).ToList();
            ViewBag.ApprovedPlans = _db.EventFeastPlans
                .Where(p => ids.Contains(p.EventId) && p.Status == FeastPlanStatus.Approved)
                .GroupBy(p => p.EventId)
                .ToDictionary(g => g.Key, g => g.Max(p => p.Id));

            var keys = ids.Select(KitchenIssueService.EventKey).ToList();
            var issued = _db.KitchenIngredientIssues.Where(i => keys.Contains(i.IssueKey)).Select(i => i.IssueKey).ToList();
            ViewBag.IssuedEventIds = ids.Where(id => issued.Contains(KitchenIssueService.EventKey(id))).ToList();

            return View(events);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private string RsvpLink(int eventId)
        {
            var scheme = Request != null ? Request.Url.Scheme : "https";
            return Url.Action("Event", "Rsvp", new { id = eventId }, scheme);
        }

        private bool CanEdit(CafeteriaEvent evt)
        {
            return evt.Status != EventStatus.Cancelled
                && evt.Status != EventStatus.Completed
                && evt.EventDate.Date >= SchoolClock.Today;
        }

        // Houses for parents and for students, from the checkboxes
        private void ReadAudience(CafeteriaEvent model)
        {
            var parentHouses = Request.Form.GetValues("InviteHouses");
            model.InviteParentResidenceIds = model.InviteParents && parentHouses != null && parentHouses.Length > 0
                ? string.Join(",", EventRsvpService.ParseIds(string.Join(",", parentHouses)))
                : null;

            var studentHouses = Request.Form.GetValues("InviteStudentHouses");
            model.InviteStudentResidenceIds = model.InviteStudents && studentHouses != null && studentHouses.Length > 0
                ? string.Join(",", EventRsvpService.ParseIds(string.Join(",", studentHouses)))
                : null;
        }

        // Ticked meals ("BuffetMeals") with their portions per guest
        // ("Portions_{id}", invariant decimal; defaults to 1)
        private Dictionary<int, decimal> ReadBuffetMeals()
        {
            var result = new Dictionary<int, decimal>();
            var ticked = Request.Form.GetValues("BuffetMeals");
            if (ticked == null) return result;

            foreach (var raw in ticked)
            {
                int id;
                if (!int.TryParse(raw, out id) || result.ContainsKey(id)) continue;

                decimal portions;
                var portionsRaw = (Request.Form["Portions_" + id] ?? "").Replace(',', '.');
                if (!decimal.TryParse(portionsRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out portions) || portions <= 0)
                {
                    portions = 1m;
                }

                result[id] = portions;
            }

            return result;
        }

        private void Validate(CafeteriaEvent model, CafeteriaEvent existing)
        {
            var now = SchoolClock.Now;

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("EndTime", "End time must be after start time.");
            }

            if (model.ExpectedHeadcount <= 0)
            {
                ModelState.AddModelError("ExpectedHeadcount", "Expected headcount must be greater than zero.");
            }

            if (model.EventDate.Date < now.Date)
            {
                ModelState.AddModelError("EventDate", "The event date cannot be in the past.");
            }

            if (!model.InviteParents && !model.InviteStaff && !model.InviteStudents)
            {
                ModelState.AddModelError("", "Choose who is invited (parents, staff and/or students).");
            }

            if (model.AllowGuests && (model.MaxGuestsPerInvitee < 1 || model.MaxGuestsPerInvitee > 10))
            {
                ModelState.AddModelError("MaxGuestsPerInvitee", "Guests per parent / staff member must be between 1 and 10.");
            }

            var eventStart = model.EventDate.Date + model.StartTime;

            if (model.RsvpOpensAt.HasValue && model.RsvpOpensAt.Value >= eventStart)
            {
                ModelState.AddModelError("RsvpOpensAt", "RSVPs must open before the event starts.");
            }

            if (model.RsvpDeadline.HasValue)
            {
                var deadline = model.RsvpDeadline.Value.Date;

                if (deadline > model.EventDate.Date)
                {
                    ModelState.AddModelError("RsvpDeadline", "The RSVP deadline cannot be after the event.");
                }
                else if (deadline < now.Date)
                {
                    ModelState.AddModelError("RsvpDeadline", "The RSVP deadline cannot be in the past.");
                }
                else if (model.RsvpOpensAt.HasValue && deadline < model.RsvpOpensAt.Value.Date)
                {
                    ModelState.AddModelError("RsvpDeadline", "The RSVP deadline must be on or after the day RSVPs open.");
                }
            }

            // Venue is optional; when given, it can't be double-booked
            if (model.VenueId.HasValue && ModelState.IsValid)
            {
                int selfId = existing != null ? existing.Id : 0;
                var date = model.EventDate.Date;

                var clashEvent = _db.CafeteriaEvents
                    .Include("Venue")
                    .FirstOrDefault(e =>
                        e.Id != selfId
                        && e.VenueId == model.VenueId
                        && e.EventDate == date
                        && e.Status != EventStatus.Cancelled
                        && e.StartTime < model.EndTime
                        && e.EndTime > model.StartTime);

                if (clashEvent != null)
                {
                    ModelState.AddModelError("",
                        string.Format("Venue conflict — {0} is already booked for \"{1}\" from {2:hh\\:mm} to {3:hh\\:mm}.",
                            clashEvent.Venue.Name,
                            clashEvent.EventName,
                            clashEvent.StartTime,
                            clashEvent.EndTime));
                }
            }
        }

        // What happens next with RSVPs, plus any dietary warnings
        private string AfterSaveMessage(CafeteriaEvent evt)
        {
            // A due opening time opens RSVPs straight away
            int notified;
            var status = _rsvp.GetStatus(evt);
            if (status.State == RsvpState.Open && (evt.Status == EventStatus.Confirmed || evt.Status == EventStatus.Draft))
            {
                _rsvp.OpenRsvp(evt, null, RsvpLink, out notified);
                status = _rsvp.GetStatus(evt);
            }

            string rsvpText;
            switch (status.State)
            {
                case RsvpState.ScheduledToOpen: rsvpText = "RSVPs will open automatically on " + evt.RsvpOpensAt.Value.ToString("ddd dd MMM 'at' HH:mm") + "."; break;
                case RsvpState.Open: rsvpText = "RSVPs are open."; break;
                case RsvpState.NotOpen: rsvpText = "RSVPs are not open yet — open them from this page when ready."; break;
                default: rsvpText = ""; break;
            }

            var coverage = _buffet.Analyse(evt, _buffet.SelectedMealIds(evt));
            if (!coverage.AllGroupsCovered)
            {
                rsvpText += " Check the dietary warnings below.";
            }

            return rsvpText.Trim();
        }

        private void PopulateForm(CafeteriaEvent model, Dictionary<int, decimal> selectedMeals)
        {
            ViewBag.Venues = _db.EventVenues
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .ToList();

            ViewBag.Templates = _db.EventMenuTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ToList();

            ViewBag.TemplateItems = _buffet.TemplateItems();

            ViewBag.Houses = _db.Residences
                .OrderBy(r => r.Name)
                .ToList();

            ViewBag.EventTypes = Enum.GetValues(typeof(EventType))
                .Cast<EventType>()
                .Select(e => new SelectListItem
                {
                    Value = ((int)e).ToString(),
                    Text = System.Text.RegularExpressions.Regex.Replace(e.ToString(), "([a-z])([A-Z])", "$1 $2")
                })
                .ToList();

            ViewBag.Meals = _buffet.AvailableMeals();
            ViewBag.SelectedMeals = selectedMeals;
            ViewBag.ParentHouseIds = EventRsvpService.ParseIds(model.InviteParentResidenceIds);
            ViewBag.StudentHouseIds = EventRsvpService.ParseIds(model.InviteStudentResidenceIds);
            ViewBag.IsEdit = model.Id > 0;
            ViewBag.RsvpStarted = model.Id > 0 && model.Status != EventStatus.Confirmed && model.Status != EventStatus.Draft;
        }

        private int ResolveUserId()
        {
            if (Session["UserId"] == null) return 0;
            int id;
            int.TryParse(Session["UserId"].ToString(), out id);
            return id;
        }
    }
}
