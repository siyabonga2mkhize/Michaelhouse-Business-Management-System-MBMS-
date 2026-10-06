using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Students' sports — one place for:
    //
    //   1. SYNC: the sports a parent selects on the student's profile
    //      (StudentProfile.Sports) become the student's
    //      StudentSportStatus rows, which the Coach sees as the squad.
    //      The Coach never keeps a second player list. Availability
    //      the Coach set (injury, leave) is kept for sports that stay.
    //
    //   2. CALENDAR: which students a Coach's fixture (SportEvent)
    //      affects, and what each student's day calls for —
    //      used by DemandService, MenuSchedulingService,
    //      MenuCoverageService, MealPlanService and the Coach pages,
    //      so they all agree.
    // ============================================================
    public class StudentSportService
    {
        private readonly DBContextClass _db;

        public StudentSportService(DBContextClass db)
        {
            _db = db;
        }

        // ============================================================
        // AVAILABILITY — the single rule
        // A return date that has arrived means the student is
        // available again, even if the Coach hasn't cleared the flag.
        // ============================================================

        public static bool IsAvailableOn(StudentSportStatus status, DateTime date)
        {
            if (status.UnavailableUntil.HasValue && status.UnavailableUntil.Value.Date <= date.Date)
            {
                return true;
            }

            return status.IsActive;
        }

        // Scheduled (not cancelled or postponed)
        public static bool IsScheduled(SportEvent e)
        {
            return !e.IsCancelled && (string.IsNullOrEmpty(e.Status) || e.Status == "Scheduled");
        }

        public static bool IsMatch(SportEvent e)
        {
            return string.Equals(e.EventType, "Match", StringComparison.OrdinalIgnoreCase);
        }

        public static string Label(SportEvent e)
        {
            if (!IsMatch(e)) return e.Sport + " training";
            return string.IsNullOrWhiteSpace(e.Opponent) ? e.Sport + " match" : e.Sport + " vs " + e.Opponent;
        }

        // ============================================================
        // SYNC FROM PROFILE
        // ============================================================

        // Returns true if anything changed. Saves.
        public bool SyncFromProfile(int studentId)
        {
            var profile = _db.StudentProfiles.Find(studentId);
            var rows = _db.StudentSportStatuses.Where(s => s.StudentId == studentId).ToList();

            bool changed = Sync(studentId, profile != null ? profile.Sports : null, rows);
            if (changed) _db.SaveChanges();
            return changed;
        }

        // Every student — used by the database seed so existing
        // students get their squad rows. Returns the number of
        // students whose rows changed. Saves.
        public int SyncAll()
        {
            var sportsByStudent = _db.StudentProfiles
                .Select(p => new { p.StudentId, p.Sports })
                .ToList()
                .ToDictionary(p => p.StudentId, p => p.Sports);

            var rowsByStudent = _db.StudentSportStatuses
                .ToList()
                .GroupBy(s => s.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            int changed = 0;

            foreach (var studentId in sportsByStudent.Keys.Union(rowsByStudent.Keys).ToList())
            {
                string sports;
                sportsByStudent.TryGetValue(studentId, out sports);

                List<StudentSportStatus> rows;
                if (!rowsByStudent.TryGetValue(studentId, out rows)) rows = new List<StudentSportStatus>();

                if (Sync(studentId, sports, rows)) changed++;
            }

            if (changed > 0) _db.SaveChanges();
            return changed;
        }

        private bool Sync(int studentId, string profileSports, List<StudentSportStatus> rows)
        {
            var wanted = SportCatalogue.Parse(profileSports);
            bool changed = false;

            // Sports no longer on the profile
            foreach (var row in rows.Where(r => !wanted.Contains(r.Sport, StringComparer.OrdinalIgnoreCase)).ToList())
            {
                _db.StudentSportStatuses.Remove(row);
                changed = true;
            }

            foreach (var sport in wanted)
            {
                var archetype = SportCatalogue.ArchetypeOf(sport);
                var row = rows.FirstOrDefault(r => string.Equals(r.Sport, sport, StringComparison.OrdinalIgnoreCase));

                if (row == null)
                {
                    _db.StudentSportStatuses.Add(new StudentSportStatus
                    {
                        StudentId = studentId,
                        Sport = sport,
                        Archetype = archetype,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    changed = true;
                }
                else if (row.Sport != sport || (archetype != SportArchetype.None && row.Archetype != archetype))
                {
                    // Catalogue spelling / archetype; availability is kept
                    row.Sport = sport;
                    if (archetype != SportArchetype.None) row.Archetype = archetype;
                    row.UpdatedAt = DateTime.UtcNow;
                    changed = true;
                }
            }

            return changed;
        }

        // ============================================================
        // CALENDAR
        // studentIds: limit to these students (null = every active
        // student). Fixtures are loaded to end + 1 day, so the last
        // day can see a match the day after.
        // ============================================================

        public SportsCalendar LoadCalendar(DateTime start, DateTime end, ICollection<int> studentIds = null)
        {
            start = start.Date;
            end = end.Date;
            var afterEnd = end.AddDays(2);

            var activeIds = _db.Students.Where(s => s.IsActive).Select(s => s.StudentId).ToList();
            var idSet = new HashSet<int>(studentIds != null ? activeIds.Intersect(studentIds) : activeIds);

            var statuses = _db.StudentSportStatuses
                .ToList()
                .Where(s => idSet.Contains(s.StudentId))
                .ToList();

            var events = _db.SportEvents
                .Where(e => e.ScheduledDate >= start && e.ScheduledDate < afterEnd)
                .ToList()
                .Where(IsScheduled)
                .ToList();

            var priorities = _db.SportPriorities
                .Where(p => p.PriorityLevel == 2 && p.WeekStartDate <= end && p.WeekEndDate >= start)
                .ToList();

            return new SportsCalendar(statuses, events, priorities, new StudentHouseService(_db).HouseByStudent());
        }
    }

    // What a student's day calls for, in priority order
    public enum SportNeedKind
    {
        None = 0,
        MatchDay = 1,
        DayBeforeMatch = 2,
        Training = 3,
        PriorityWeek = 4
    }

    public class SportNeed
    {
        public static readonly SportNeed None = new SportNeed();

        public SportNeedKind Kind { get; set; }

        // Null when the day has an activity but no particular need
        // (e.g. cricket training)
        public NutritionCategory? Category { get; set; }

        public string Sport { get; set; }

        // e.g. "Rugby vs Hilton", "Rugby training"
        public string Description { get; set; }

        // Every sport the student is available for is marked
        // unavailable today (injury, leave) — no sports push
        public bool IsRecovering { get; set; }

        // At least one of the student's sports is available today
        public bool HasAvailableSport { get; set; }

        public SportArchetype Archetype { get; set; }

        // For the student: "Recommended for your Rugby match"
        public string RecommendationLabel
        {
            get
            {
                switch (Kind)
                {
                    case SportNeedKind.MatchDay: return "Recommended for your " + Sport + " match";
                    case SportNeedKind.DayBeforeMatch: return "Recommended before your " + Sport + " match";
                    case SportNeedKind.Training: return "Recommended for your " + Sport + " training";
                    case SportNeedKind.PriorityWeek: return "Recommended for your " + Sport + " priority week";
                    default: return null;
                }
            }
        }

        // For the Cafeteria Manager: "Rugby vs Hilton (match day)"
        public string ManagerLabel
        {
            get
            {
                switch (Kind)
                {
                    case SportNeedKind.MatchDay: return Description + " (match day)";
                    case SportNeedKind.DayBeforeMatch: return Description + " (day before the match)";
                    case SportNeedKind.Training: return Description;
                    case SportNeedKind.PriorityWeek: return Description;
                    default: return null;
                }
            }
        }
    }

    // ============================================================
    // Fixtures, squads and availability for a date range — built by
    // StudentSportService.LoadCalendar.
    //
    // Rules (unchanged from the original UC12 logic):
    //   Match day          → the sport's match-day category
    //                        (Power → HighProtein, Endurance → HighCarb,
    //                         Skill → Light, Speed → Hydration)
    //   Day before a match → HighCarb
    //   Training day       → Power → HighProtein, Endurance → HighCarb
    //   Priority sport week→ same as training
    // A student with several activities on one day gets the first of
    // match > day before a match > training > priority week.
    // ============================================================
    public class SportsCalendar
    {
        private readonly Dictionary<int, List<StudentSportStatus>> _statusesByStudent;
        private readonly List<SportEvent> _events;
        private readonly List<SportPriority> _priorities;
        private readonly Dictionary<int, int> _houseByStudent;

        public SportsCalendar(
            List<StudentSportStatus> statuses,
            List<SportEvent> events,
            List<SportPriority> priorities,
            Dictionary<int, int> houseByStudent)
        {
            _statusesByStudent = statuses
                .GroupBy(s => s.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());
            _events = events.OrderBy(e => e.ScheduledDate).ThenBy(e => e.StartTime).ToList();
            _priorities = priorities;
            _houseByStudent = houseByStudent ?? new Dictionary<int, int>();
        }

        public IEnumerable<int> StudentsWithSports
        {
            get { return _statusesByStudent.Keys; }
        }

        public List<StudentSportStatus> StatusesOf(int studentId)
        {
            List<StudentSportStatus> list;
            return _statusesByStudent.TryGetValue(studentId, out list) ? list : new List<StudentSportStatus>();
        }

        public List<SportEvent> EventsOn(DateTime date)
        {
            return _events.Where(e => e.ScheduledDate.Date == date.Date).ToList();
        }

        // Does this fixture involve the student? Their sport, available
        // that day, and in the fixture's house if it names one.
        public bool Involves(SportEvent e, int studentId)
        {
            if (e.ResidenceId.HasValue)
            {
                int house;
                if (!_houseByStudent.TryGetValue(studentId, out house) || house != e.ResidenceId.Value) return false;
            }

            return StatusesOf(studentId).Any(s =>
                string.Equals(s.Sport, e.Sport, StringComparison.OrdinalIgnoreCase)
                && StudentSportService.IsAvailableOn(s, e.ScheduledDate));
        }

        // The students a fixture affects
        public List<int> PlayersFor(SportEvent e)
        {
            return _statusesByStudent.Keys.Where(id => Involves(e, id)).ToList();
        }

        public SportNeed NeedOn(int studentId, DateTime date)
        {
            var statuses = StatusesOf(studentId);
            if (statuses.Count == 0) return SportNeed.None;

            date = date.Date;
            var available = statuses.Where(s => StudentSportService.IsAvailableOn(s, date)).ToList();

            if (available.Count == 0)
            {
                return new SportNeed { IsRecovering = true };
            }

            var match = EventsOn(date).FirstOrDefault(e => StudentSportService.IsMatch(e) && Involves(e, studentId));
            if (match != null)
            {
                var archetype = ArchetypeOf(match.Sport, statuses);
                return Need(SportNeedKind.MatchDay, MenuSchedulingService.MatchDayCategory(archetype), match.Sport, StudentSportService.Label(match), archetype);
            }

            var tomorrow = EventsOn(date.AddDays(1)).FirstOrDefault(e => StudentSportService.IsMatch(e) && Involves(e, studentId));
            if (tomorrow != null)
            {
                return Need(SportNeedKind.DayBeforeMatch, NutritionCategory.HighCarb, tomorrow.Sport, StudentSportService.Label(tomorrow), ArchetypeOf(tomorrow.Sport, statuses));
            }

            var training = EventsOn(date).FirstOrDefault(e => !StudentSportService.IsMatch(e) && Involves(e, studentId));
            if (training != null)
            {
                var archetype = ArchetypeOf(training.Sport, statuses);
                return Need(SportNeedKind.Training, MenuSchedulingService.TrainingCategory(archetype), training.Sport, StudentSportService.Label(training), archetype);
            }

            var priority = available.FirstOrDefault(s => _priorities.Any(p =>
                string.Equals(p.Sport, s.Sport, StringComparison.OrdinalIgnoreCase)
                && p.WeekStartDate.Date <= date && p.WeekEndDate.Date >= date));
            if (priority != null)
            {
                var archetype = ArchetypeOf(priority.Sport, statuses);
                return Need(SportNeedKind.PriorityWeek, MenuSchedulingService.TrainingCategory(archetype), priority.Sport, priority.Sport + " priority week", archetype);
            }

            return new SportNeed { HasAvailableSport = true };
        }

        private static SportNeed Need(SportNeedKind kind, NutritionCategory? category, string sport, string description, SportArchetype archetype)
        {
            return new SportNeed
            {
                Kind = kind,
                Category = category,
                Sport = sport,
                Description = description,
                Archetype = archetype,
                HasAvailableSport = true
            };
        }

        // The catalogue's archetype; the student's row if the sport
        // isn't in the catalogue
        private static SportArchetype ArchetypeOf(string sport, List<StudentSportStatus> statuses)
        {
            var archetype = SportCatalogue.ArchetypeOf(sport);
            if (archetype != SportArchetype.None) return archetype;

            var row = statuses.FirstOrDefault(s => string.Equals(s.Sport, sport, StringComparison.OrdinalIgnoreCase));
            return row != null ? row.Archetype : SportArchetype.None;
        }
    }
}
