using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Coach, Admin")]
    public class CoachSquadController : Controller
    {
        private readonly DBContextClass _db;

        public CoachSquadController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: CoachSquad/DemandPreview
        // Temporary test endpoint — returns the computed demand for
        // the next 7 days as JSON. Remove once the generator uses it.
        // ============================================================

        [HttpGet]
        public ActionResult DemandPreview()
        {
            using (var db = new DBContextClass())
            {
                var svc = new Michaelhouse.Services.DemandService(db);
                var start = DateTime.Today;
                var end = DateTime.Today.AddDays(6);
                var demand = svc.ComputeRange(start, end, 12);

                var output = demand.Select(d => new
                {
                    Date = d.Date.ToString("yyyy-MM-dd"),
                    Meal = d.MealSlot,
                    House = d.HousePortions,
                    Staff = d.StaffPortions,
                    Uplift = d.SportsUplift,
                    Total = d.TotalPortions,
                    Events = d.EventsThisMeal,
                    Houses = d.Houses.Select(h => new
                    {
                        h.ResidenceName,
                        h.ActiveStudents,
                        h.Unavailable,
                        h.MatchPlayers
                    })
                }).ToList();

                return Json(output, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================================================
        // GET: CoachSquad
        // Squad status — mark injuries, availability
        // ============================================================

        // Players come from the sports on students' profiles
        // (StudentSportService keeps StudentSportStatus in step), so
        // the Coach never maintains a player list — only availability.
        [HttpGet]
        public ActionResult Index()
        {
            var today = SchoolClock.Today;
            var until = today.AddDays(UpcomingDays);

            var statuses = _db.StudentSportStatuses
                .Include("Student")
                .Where(x => x.Student.IsActive)
                .ToList();

            var houseByStudent = new StudentHouseService(_db).HouseByStudent();
            var houseNames = _db.Residences.ToDictionary(r => r.ResidenceId, r => r.Name);
            Func<int?, string> houseName = id => id.HasValue && houseNames.ContainsKey(id.Value) ? houseNames[id.Value] : null;

            var calendar = new StudentSportService(_db).LoadCalendar(today, until);
            var fixtures = _db.SportEvents
                .Where(e => e.ScheduledDate >= today && e.ScheduledDate <= until)
                .ToList()
                .Where(StudentSportService.IsScheduled)
                .OrderBy(e => e.ScheduledDate)
                .ThenBy(e => e.StartTime)
                .ToList();

            var sports = statuses.Select(s => s.Sport)
                .Concat(fixtures.Select(e => e.Sport))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            var model = sports.Select(sport => new SportSquadViewModel
            {
                Sport = sport,
                Archetype = SportCatalogue.ArchetypeOf(sport),
                Players = statuses
                    .Where(s => string.Equals(s.Sport, sport, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.Student.LastName)
                    .ThenBy(s => s.Student.FirstName)
                    .Select(s =>
                    {
                        int house;
                        return new SquadPlayerViewModel
                        {
                            StatusId = s.Id,
                            Name = s.Student.FirstName + " " + s.Student.LastName,
                            StudentNumber = s.Student.StudentNumber,
                            Grade = !string.IsNullOrWhiteSpace(s.Student.CurrentGrade)
                                ? "Grade " + s.Student.CurrentGrade
                                : (s.Student.GradeLevel > 0 ? "Grade " + s.Student.GradeLevel : null),
                            House = houseByStudent.TryGetValue(s.StudentId, out house) ? houseName(house) : null,
                            IsAvailableToday = StudentSportService.IsAvailableOn(s, today),
                            StatusReason = s.StatusReason,
                            UnavailableUntil = s.UnavailableUntil
                        };
                    })
                    .ToList(),
                Upcoming = fixtures
                    .Where(e => string.Equals(e.Sport, sport, StringComparison.OrdinalIgnoreCase))
                    .Select(e => new SquadFixtureViewModel
                    {
                        Id = e.Id,
                        Date = e.ScheduledDate,
                        StartTime = e.StartTime,
                        EndTime = e.StartTime.Add(TimeSpan.FromMinutes(e.DurationMinutes)),
                        EventType = e.EventType,
                        Label = StudentSportService.Label(e),
                        House = houseName(e.ResidenceId),
                        Players = calendar.PlayersFor(e).Count
                    })
                    .ToList()
            }).ToList();

            ViewBag.UpcomingDays = UpcomingDays;
            return View(model);
        }

        private const int UpcomingDays = 14;

        // ============================================================
        // POST: CoachSquad/MarkUnavailable
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkUnavailable(int id, string reason, DateTime? until)
        {
            var entry = _db.StudentSportStatuses.FirstOrDefault(x => x.Id == id);
            if (entry == null) return HttpNotFound();

            entry.IsActive = false;
            entry.StatusReason = string.IsNullOrWhiteSpace(reason) ? "Unavailable" : reason.Trim();
            entry.UnavailableUntil = until;
            entry.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            TempData["Success"] = "Marked unavailable.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: CoachSquad/MarkAvailable
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAvailable(int id)
        {
            var entry = _db.StudentSportStatuses.FirstOrDefault(x => x.Id == id);
            if (entry == null) return HttpNotFound();

            entry.IsActive = true;
            entry.StatusReason = null;
            entry.UnavailableUntil = null;
            entry.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            TempData["Success"] = "Marked available.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // GET: CoachSquad/Priorities
        // Weekly sport priority flags
        // ============================================================

        [HttpGet]
        public ActionResult Priorities()
        {
            var thisMonday = GetMonday(DateTime.Today);
            var thisSunday = thisMonday.AddDays(6);

            var existing = _db.SportPriorities
                .Where(p => p.WeekStartDate == thisMonday)
                .ToList();

            ViewBag.WeekStart = thisMonday;
            ViewBag.WeekEnd = thisSunday;
            ViewBag.Existing = existing;

            // Every sport that has any active student
            var allSports = _db.StudentSportStatuses
                .Select(s => s.Sport)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            ViewBag.Sports = allSports;
            return View();
        }

        // ============================================================
        // POST: CoachSquad/SetPriority
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetPriority(string sport, string reason, bool isHigh)
        {
            var monday = GetMonday(DateTime.Today);
            var sunday = monday.AddDays(6);

            var existing = _db.SportPriorities
                .FirstOrDefault(p => p.Sport == sport && p.WeekStartDate == monday);

            if (isHigh)
            {
                if (existing == null)
                {
                    _db.SportPriorities.Add(new SportPriority
                    {
                        Sport = sport,
                        WeekStartDate = monday,
                        WeekEndDate = sunday,
                        PriorityLevel = 2,
                        Reason = string.IsNullOrWhiteSpace(reason) ? "High priority" : reason.Trim(),
                        SetByUserId = ResolveUserId(),
                        SetAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.PriorityLevel = 2;
                    existing.Reason = string.IsNullOrWhiteSpace(reason) ? "High priority" : reason.Trim();
                    existing.SetAt = DateTime.UtcNow;
                }
            }
            else
            {
                if (existing != null) _db.SportPriorities.Remove(existing);
            }

            _db.SaveChanges();

            TempData["Success"] = isHigh
                ? sport + " marked high priority this week."
                : sport + " priority reset to normal.";

            return RedirectToAction("Priorities");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private DateTime GetMonday(DateTime date)
        {
            int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.Date.AddDays(-diff);
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