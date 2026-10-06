using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // Coach — trainings and matches (SportEvent)
    //
    // The players a fixture affects are never entered here: they are
    // the students whose profile lists the sport (StudentSportService)
    // and who are available that day. Leave the house empty for a
    // school fixture (every player of the sport); choose a house for
    // an inter-house fixture.
    //
    // Fixtures feed the cafeteria: menu generation adds suitable
    // options for match days, the day before a match and training,
    // and students' meal plans recommend them.
    // ============================================================
    [Authorize(Roles = "Coach, Admin")]
    public class CoachController : Controller
    {
        private readonly DBContextClass _db;

        public CoachController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: Coach/Index
        // Lists all upcoming fixtures and recent training sessions
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            var today = SchoolClock.Today;

            var upcoming = _db.SportEvents
                .Include(x => x.Residence)
                .Where(x => !x.IsCancelled && x.ScheduledDate >= today)
                .OrderBy(x => x.ScheduledDate)
                .ThenBy(x => x.StartTime)
                .ToList();

            var past = _db.SportEvents
                .Include(x => x.Residence)
                .Where(x => !x.IsCancelled && x.ScheduledDate < today)
                .OrderByDescending(x => x.ScheduledDate)
                .Take(10)
                .ToList();

            ViewBag.Upcoming = upcoming;
            ViewBag.Past = past;
            ViewBag.PlayerCounts = PlayerCounts(upcoming);

            return View();
        }

        // ============================================================
        // GET: Coach/Create
        // ============================================================

        [HttpGet]
        public ActionResult Create()
        {
            var model = new SportEvent { ScheduledDate = SchoolClock.Today };

            PopulateDropdowns();
            ViewBag.EndTime = EndTimeOf(model);
            return View(model);
        }

        // ============================================================
        // POST: Coach/Create
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SportEvent model, string endTime)
        {
            Validate(model, endTime);

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                ViewBag.EndTime = endTime;
                return View(model);
            }

            model.Sport = SportCatalogue.Normalise(model.Sport);
            model.CreatedByUserId = (int)(Session["UserId"] ?? 0);
            model.CreatedAt = DateTime.UtcNow;
            model.IsCancelled = false;
            model.Status = "Scheduled";
            if (!IsMatch(model)) model.Opponent = null;

            _db.SportEvents.Add(model);
            _db.SaveChanges();

            TempData["Success"] = Saved(model, "scheduled");
            return RedirectToAction("Index");
        }

        // ============================================================
        // GET: Coach/Edit/5
        // ============================================================

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var ev = _db.SportEvents.Find(id);

            if (ev == null) return HttpNotFound();

            PopulateDropdowns();
            ViewBag.EndTime = EndTimeOf(ev);
            return View(ev);
        }

        // ============================================================
        // POST: Coach/Edit/5
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SportEvent model, string endTime)
        {
            Validate(model, endTime);

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                ViewBag.EndTime = endTime;
                return View(model);
            }

            var existing = _db.SportEvents.Find(model.Id);

            if (existing == null) return HttpNotFound();

            existing.Sport = SportCatalogue.Normalise(model.Sport);
            existing.EventType = model.EventType;
            existing.ScheduledDate = model.ScheduledDate;
            existing.StartTime = model.StartTime;
            existing.DurationMinutes = model.DurationMinutes;
            existing.Intensity = model.Intensity;
            existing.ResidenceId = model.ResidenceId;
            existing.Opponent = IsMatch(model) ? model.Opponent : null;
            existing.IsHome = model.IsHome;
            existing.Notes = model.Notes;

            _db.SaveChanges();

            TempData["Success"] = Saved(existing, "updated");
            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: Coach/Cancel/5
        // Soft-cancel — keeps the audit trail
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var ev = _db.SportEvents.Find(id);

            if (ev == null) return HttpNotFound();

            ev.IsCancelled = true;
            ev.Status = "Cancelled";
            _db.SaveChanges();

            TempData["Success"] = "Fixture cancelled. It no longer affects menus or meal plans.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static bool IsMatch(SportEvent e)
        {
            return e.EventType == "Match";
        }

        // Sport, type and house come from the lists; the end time
        // becomes DurationMinutes
        private void Validate(SportEvent model, string endTime)
        {
            if (SportCatalogue.Normalise(model.Sport) == null)
            {
                ModelState.AddModelError("Sport", "Please choose a sport.");
            }

            if (model.EventType != "Training" && model.EventType != "Match")
            {
                ModelState.AddModelError("EventType", "Please choose Training or Match.");
            }

            if (IsMatch(model) && string.IsNullOrWhiteSpace(model.Opponent))
            {
                ModelState.AddModelError("Opponent", "Please name the opponent for a match.");
            }

            if (model.ResidenceId.HasValue && !_db.Residences.Any(r => r.ResidenceId == model.ResidenceId.Value))
            {
                ModelState.AddModelError("ResidenceId", "Please choose a house from the list, or leave it empty for every player.");
            }

            TimeSpan end;
            if (string.IsNullOrWhiteSpace(endTime) || !TimeSpan.TryParse(endTime, out end))
            {
                ModelState.AddModelError("endTime", "Please enter an end time.");
            }
            else if (end <= model.StartTime)
            {
                ModelState.AddModelError("endTime", "The end time must be after the start time.");
            }
            else
            {
                model.DurationMinutes = (int)(end - model.StartTime).TotalMinutes;
                ModelState.Remove("DurationMinutes");
            }
        }

        private static string EndTimeOf(SportEvent e)
        {
            var end = e.StartTime.Add(TimeSpan.FromMinutes(e.DurationMinutes));
            if (end.TotalHours >= 24) end = new TimeSpan(23, 59, 0);
            return end.ToString(@"hh\:mm");
        }

        // e.g. "Match scheduled for Wed 15 Oct. 28 available Rugby
        // players get match-day meal recommendations."
        private string Saved(SportEvent e, string verb)
        {
            int players = PlayerCounts(new[] { e }).Values.FirstOrDefault();

            string effect;
            switch (e.EventType)
            {
                case "Match": effect = "match-day meal recommendations (and pre-match meals the day before)"; break;
                default: effect = "training-day meal recommendations"; break;
            }

            var house = e.ResidenceId.HasValue
                ? _db.Residences.Where(r => r.ResidenceId == e.ResidenceId.Value).Select(r => r.Name).FirstOrDefault()
                : null;

            return string.Format(
                "{0} {1} for {2:ddd dd MMM}. {3} available {4} player{5}{6} — they get {7}.",
                e.EventType, verb, e.ScheduledDate, players, e.Sport,
                players == 1 ? "" : "s",
                house != null ? " in " + house : "",
                effect);
        }

        // Fixture id → the players it affects
        private Dictionary<int, int> PlayerCounts(ICollection<SportEvent> events)
        {
            var result = new Dictionary<int, int>();
            if (events.Count == 0) return result;

            var calendar = new StudentSportService(_db).LoadCalendar(
                events.Min(e => e.ScheduledDate), events.Max(e => e.ScheduledDate));

            foreach (var e in events)
            {
                result[e.Id] = calendar.PlayersFor(e).Count;
            }

            return result;
        }

        private void PopulateDropdowns()
        {
            ViewBag.Residences = _db.Residences
                .Where(x => !x.IsArchived)
                .OrderBy(x => x.Name)
                .ToList();

            ViewBag.Sports = SportCatalogue.Names.ToArray();

            ViewBag.Intensities = new[]
            {
                "Light", "Moderate", "High"
            };

            ViewBag.EventTypes = new[]
            {
                "Training", "Match"
            };
        }
    }
}
