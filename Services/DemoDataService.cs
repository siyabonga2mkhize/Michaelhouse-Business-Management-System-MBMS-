using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Demo data for presentations (Admin only — DemoDataController)
    //
    // Gives one student a SUBMITTED meal plan with breakfast, lunch
    // and dinner chosen for today and the next few days, so face meal
    // collection can be shown at any time of day.
    //
    // It does not change how real meal plans work:
    //   • it never creates or changes a menu — it uses the menu the
    //     Meal Coordinator published, picked the same way as
    //     MealPlanService / MealCollectionService do
    //   • every pick is a published option for that day and meal,
    //     active, right for the meal, and safe for the student's
    //     dietary profile (DietaryProfileService.IsSafe)
    //   • the only rule it skips is the selection deadline, so that
    //     today's meals can be set up
    // Running it again resets the same days (and, if asked, removes
    // the student's collections for them so they can collect again).
    // ============================================================
    public class DemoDataService
    {
        private static readonly MealSlot[] Slots = { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Dinner };

        private readonly DBContextClass _db;

        public DemoDataService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public DemoMealPlanResult CreateSubmittedMealPlan(int studentId, int days, bool clearCollections)
        {
            var result = new DemoMealPlanResult();

            var student = _db.Students.FirstOrDefault(s => s.StudentId == studentId);
            if (student == null)
            {
                result.Problems.Add("Student not found.");
                return result;
            }
            result.StudentName = (student.FirstName + " " + student.LastName).Trim();

            days = Math.Max(1, Math.Min(days, 7));
            var today = SchoolClock.Today;

            var profile = new DietaryProfileService(_db).GetProfileForStudent(studentId);
            var dietary = new DietaryProfileService(_db);
            var candidates = _db.MenuItems
                .Where(m => m.IsActive)
                .Include(m => m.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .ToList()
                .ToDictionary(m => m.Id);

            var touchedPlans = new List<MealPlan>();

            for (var date = today; date < today.AddDays(days); date = date.AddDays(1))
            {
                var menu = PublishedMenuFor(date);
                if (menu == null)
                {
                    result.Problems.Add(date.ToString("ddd dd MMM") + ": no published menu covers this day. " +
                                        "Ask the Meal Coordinator to publish a menu that includes it.");
                    continue;
                }

                // Same plan the student's own page uses: one per menu week
                // (one already being set up in this run comes first — it
                // isn't saved yet, so the database doesn't know it)
                var weekStart = menu.StartDate.Date;
                var plan = touchedPlans.FirstOrDefault(p => p.WeekStartDate == weekStart)
                           ?? _db.MealPlans
                               .Include(p => p.Items)
                               .FirstOrDefault(p => p.StudentId == studentId && p.WeekStartDate == weekStart);

                if (plan == null)
                {
                    plan = new MealPlan
                    {
                        StudentId = studentId,
                        WeekStartDate = menu.StartDate.Date,
                        WeekEndDate = menu.EndDate.Date,
                        Status = MealPlanStatus.Draft,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.MealPlans.Add(plan);
                }
                if (!touchedPlans.Contains(plan)) touchedPlans.Add(plan);

                var options = _db.MenuScheduleItems
                    .Where(s => s.MealMenuId == menu.Id && s.Date == date)
                    .OrderBy(s => s.Id)
                    .ToList();

                foreach (var slot in Slots)
                {
                    // First published option for this meal that the student may eat
                    MenuItem chosenItem = null;
                    MenuScheduleItem chosenOption = null;

                    foreach (var option in options.Where(o => o.MealSlot == slot))
                    {
                        MenuItem item;
                        if (candidates.TryGetValue(option.MenuItemId, out item)
                            && IsEligibleForSlot(item, slot)
                            && dietary.IsSafe(profile, item))
                        {
                            chosenItem = item;
                            chosenOption = option;
                            break;
                        }
                    }

                    if (chosenItem == null)
                    {
                        result.Problems.Add(string.Format("{0:ddd dd MMM} {1}: no option on the menu suits {2}'s dietary profile.",
                            date, slot, result.StudentName));
                        continue;
                    }

                    var existing = plan.Items.FirstOrDefault(i => i.Date == date && i.MealSlot == slot);
                    if (existing == null)
                    {
                        plan.Items.Add(new MealPlanItem
                        {
                            Date = date,
                            MealSlot = slot,
                            MenuItemId = chosenItem.Id,
                            MenuScheduleItemId = chosenOption.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existing.MenuItemId = chosenItem.Id;
                        existing.MenuScheduleItemId = chosenOption.Id;
                    }

                    result.Picks.Add(string.Format("{0:ddd dd MMM} · {1}: {2}", date, slot, chosenItem.Name));
                }

                if (clearCollections)
                {
                    var day = date;
                    var collected = _db.MealCollections.Where(c => c.StudentId == studentId && c.Date == day).ToList();
                    result.CollectionsRemoved += collected.Count;
                    _db.MealCollections.RemoveRange(collected);
                }
            }

            // Submitted, exactly as if the student had pressed Submit
            foreach (var plan in touchedPlans)
            {
                plan.Status = MealPlanStatus.Submitted;
                plan.SubmittedAt = DateTime.UtcNow;
                plan.UpdatedAt = DateTime.UtcNow;
            }

            _db.SaveChanges();
            return result;
        }

        // The published menu whose week includes this date. For a week
        // start, the most recently published menu wins — the same
        // ordering MealPlanService and MealCollectionService use.
        public MealMenu PublishedMenuFor(DateTime date)
        {
            var accepted = _db.MealMenus
                .Where(m => m.MenuStatus == MenuStatus.Accepted)
                .ToList();

            return accepted
                .GroupBy(m => m.StartDate.Date)
                .Select(g => g
                    .OrderByDescending(m => m.LastModifiedDate)
                    .ThenByDescending(m => m.CreatedDate)
                    .ThenByDescending(m => m.Id)
                    .First())
                .Where(m => m.StartDate.Date <= date && m.EndDate.Date >= date)
                .OrderByDescending(m => m.LastModifiedDate)
                .ThenByDescending(m => m.CreatedDate)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();
        }

        // Same rule as MealPlanService.IsEligibleForSlot
        private static bool IsEligibleForSlot(MenuItem item, MealSlot slot)
        {
            switch (slot)
            {
                case MealSlot.Breakfast: return item.IsBreakfastItem;
                case MealSlot.Lunch: return item.IsLunchItem;
                case MealSlot.Dinner: return item.IsDinnerItem;
                default: return false;
            }
        }
    }

    public class DemoMealPlanResult
    {
        public DemoMealPlanResult()
        {
            Picks = new List<string>();
            Problems = new List<string>();
        }

        public string StudentName { get; set; }
        public List<string> Picks { get; set; }
        public List<string> Problems { get; set; }
        public int CollectionsRemoved { get; set; }
    }
}
