using Michaelhouse.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
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
            var upcoming = _db.SportEvents
                .Include(x => x.Residence)
                .Where(x => !x.IsCancelled && x.ScheduledDate >= DateTime.Today)
                .OrderBy(x => x.ScheduledDate)
                .ThenBy(x => x.StartTime)
                .ToList();

            var past = _db.SportEvents
                .Include(x => x.Residence)
                .Where(x => !x.IsCancelled && x.ScheduledDate < DateTime.Today)
                .OrderByDescending(x => x.ScheduledDate)
                .Take(10)
                .ToList();

            ViewBag.Upcoming = upcoming;
            ViewBag.Past = past;

            return View();
        }

        // ============================================================
        // GET: Coach/Create
        // ============================================================

        [HttpGet]
        public ActionResult Create()
        {
            PopulateDropdowns();
            return View(new SportEvent());
        }

        // ============================================================
        // POST: Coach/Create
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(SportEvent model)
        {
            // Coach supplies these — system never invents them
            if (string.IsNullOrWhiteSpace(model.Sport))
            {
                ModelState.AddModelError("Sport", "Please choose a sport.");
            }

            if (string.IsNullOrWhiteSpace(model.EventType))
            {
                ModelState.AddModelError("EventType", "Please choose Training or Match.");
            }

            if (model.EventType == "Match" && string.IsNullOrWhiteSpace(model.Opponent))
            {
                ModelState.AddModelError("Opponent", "Please name the opponent for a match.");
            }

            if (model.ResidenceId <= 0)
            {
                ModelState.AddModelError("ResidenceId", "Please choose a residence.");
            }

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return View(model);
            }

            model.CreatedByUserId = (int)(Session["UserId"] ?? 0);
            model.CreatedAt = DateTime.UtcNow;
            model.IsCancelled = false;

            _db.SportEvents.Add(model);
            _db.SaveChanges();

            TempData["Success"] =
                model.EventType + " scheduled for " +
                model.ScheduledDate.ToString("dd MMM yyyy") + ".";

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
            return View(ev);
        }

        // ============================================================
        // POST: Coach/Edit/5
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(SportEvent model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return View(model);
            }

            var existing = _db.SportEvents.Find(model.Id);

            if (existing == null) return HttpNotFound();

            existing.Sport = model.Sport;
            existing.EventType = model.EventType;
            existing.ScheduledDate = model.ScheduledDate;
            existing.StartTime = model.StartTime;
            existing.DurationMinutes = model.DurationMinutes;
            existing.Intensity = model.Intensity;
            existing.ResidenceId = model.ResidenceId;
            existing.Opponent = model.Opponent;
            existing.IsHome = model.IsHome;
            existing.Notes = model.Notes;

            _db.SaveChanges();

            TempData["Success"] = "Fixture updated.";
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
            _db.SaveChanges();

            TempData["Success"] = "Fixture cancelled.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void PopulateDropdowns()
        {
            ViewBag.Residences = _db.Residences
                .OrderBy(x => x.Name)
                .ToList();

            ViewBag.Sports = new[]
            {
                "Rugby", "Cricket", "Water Polo", "Hockey",
                "Basketball", "Athletics", "Swimming",
                "Tennis", "Squash", "Cross Country", "Other"
            };

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