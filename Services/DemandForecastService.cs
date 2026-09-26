using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Michaelhouse.Models;

namespace Michaelhouse.Services
{
    public class DemandForecastResult
    {
        public int EligibleStudents { get; set; }
        public int ExpectedAttendance { get; set; }
        public int EventGuests { get; set; }
        public int SportsParticipants { get; set; }
        public int KnownExclusions { get; set; }
        public int BufferPercent { get; set; }
        public int BufferMeals { get; set; }
        public int ForecastTotal { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
    }

    public class DemandForecastService
    {
        private readonly DBContextClass _db;
        private readonly CafeteriaSettingsService _settings;

        public DemandForecastService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _settings = new CafeteriaSettingsService(db);
        }

        public DemandForecastService() : this(new DBContextClass()) { }

        // ─────────────────────────────────────────────────────────────
        // Forecast for a single meal
        // ─────────────────────────────────────────────────────────────

        public DemandForecastResult ForecastForMeal(int menuMealId)
        {
            var result = new DemandForecastResult();
            var meal = _db.MenuMeals
                .Include("MenuDay")
                .FirstOrDefault(m => m.Id == menuMealId);

            if (meal == null)
            {
                result.Notes.Add("Menu meal not found.");
                return result;
            }

            // 1. Eligible students
            result.EligibleStudents = _db.Students.Count(s => s.IsActive);

            // 2. Known exclusions
            result.KnownExclusions = 0; // absence integration in Phase 11

            // 3. Expected attendance
            result.ExpectedAttendance = result.EligibleStudents - result.KnownExclusions;

            // 4. Sports participants whose activity touches this meal
            if (meal.MenuDay != null)
            {
                var day = meal.MenuDay.Date.Date;
                var slotStart = meal.MealSlot != null && meal.MealSlot.DefaultStartTime.HasValue
                    ? meal.MealSlot.DefaultStartTime.Value
                    : TimeSpan.Zero;

                var slotWindowStart = day.Add(slotStart);
                var slotWindowEnd = slotWindowStart.AddHours(3);

                var activityTeamIds = _db.SportsActivities
                    .Where(a => a.StartDateTime >= slotWindowStart &&
                                a.StartDateTime <= slotWindowEnd &&
                                !a.IsCancelled)
                    .Select(a => a.TeamId)
                    .Distinct()
                    .ToList();

                if (activityTeamIds.Count > 0)
                {
                    result.SportsParticipants = _db.TeamMemberships
                        .Where(m => activityTeamIds.Contains(m.TeamId) && m.EndDate == null)
                        .Select(m => m.StudentId)
                        .Distinct()
                        .Count();

                    result.Notes.Add(result.SportsParticipants +
                        " student(s) have sports activities near this meal.");
                }
            }

            // 5. Event guests
            if (meal.MenuDay != null)
            {
                var day = meal.MenuDay.Date.Date;
                var events = _db.Events
                    .Where(e => DbFunctions.TruncateTime(e.Date) == day && e.Status != "Cancelled")
                    .ToList();
                foreach (var e in events)
                    result.EventGuests += e.ExpectedGuests;
            }

            // 6. Buffer
            result.BufferPercent = _settings.GetInt("DemandBufferPercent", 5);

            // 7. Totals
            int baseCount = result.ExpectedAttendance + result.SportsParticipants + result.EventGuests;
            result.BufferMeals = (int)Math.Ceiling(baseCount * (result.BufferPercent / 100m));
            result.ForecastTotal = baseCount + result.BufferMeals;

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // Forecast for a whole day (all meals)
        // ─────────────────────────────────────────────────────────────

        public Dictionary<string, DemandForecastResult> ForecastForDay(DateTime date)
        {
            var output = new Dictionary<string, DemandForecastResult>();

            var day = _db.MenuDays
                .Include("Meals.MealSlot")
                .FirstOrDefault(d => DbFunctions.TruncateTime(d.Date) == date.Date);

            if (day == null) return output;

            foreach (var meal in day.Meals)
            {
                var slotName = meal.MealSlot != null ? meal.MealSlot.Name : ("Meal " + meal.Id);
                output[slotName] = ForecastForMeal(meal.Id);
            }

            return output;
        }

        // ─────────────────────────────────────────────────────────────
        // Forecast vs actual (used by reports)
        // ─────────────────────────────────────────────────────────────

        public class ForecastActualComparison
        {
            public string MealSlot { get; set; }
            public int Forecast { get; set; }
            public int Selected { get; set; }
            public int Produced { get; set; }
            public int Collected { get; set; }
            public int Leftover { get { return Math.Max(0, Produced - Collected); } }
        }

        public List<ForecastActualComparison> CompareForecastVsActual(DateTime date)
        {
            var output = new List<ForecastActualComparison>();

            var day = _db.MenuDays
                .Include("Meals.MealSlot")
                .FirstOrDefault(d => DbFunctions.TruncateTime(d.Date) == date.Date);

            if (day == null) return output;

            foreach (var meal in day.Meals)
            {
                var slotName = meal.MealSlot != null ? meal.MealSlot.Name : ("Meal " + meal.Id);

                var forecast = ForecastForMeal(meal.Id).ForecastTotal;
                var selected = _db.MealSelections.Count(s => s.MenuMealId == meal.Id);
                var produced = _db.ProductionRecords
                    .Where(p => p.MenuMealId == meal.Id && DbFunctions.TruncateTime(p.ProductionDate) == date.Date)
                    .Select(p => (int?)p.ProducedQuantity)
                    .FirstOrDefault() ?? 0;
                var collected = _db.MealServices.Count(s => s.MenuMealId == meal.Id &&
                                                          DbFunctions.TruncateTime(s.ServedAt) == date.Date);

                output.Add(new ForecastActualComparison
                {
                    MealSlot = slotName,
                    Forecast = forecast,
                    Selected = selected,
                    Produced = produced,
                    Collected = collected
                });
            }

            return output;
        }
    }
}