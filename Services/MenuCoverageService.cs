using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Menu dietary coverage
    //
    // A generated menu offers several options per meal slot. This
    // service checks whether, together, the options give every
    // dietary group of students at least one suitable choice — and
    // every group of athletes with a sports need that day (match,
    // day before a match, training) an option in the needed
    // nutrition category that is also safe for their diet.
    //
    // Students are grouped by the requirements that actually filter
    // meals (DietaryProfileService.IsSafe): vegetarian / vegan / halal,
    // allergies, and lactose / gluten / coeliac restrictions. Things
    // that never exclude a meal (diabetes, "Other" preference) don't
    // create a separate group.
    //
    // Used by MenuSchedulingService while choosing options and by the
    // Review page, so warnings stay correct after manual changes.
    // ============================================================

    public class DietaryGroup
    {
        public DietaryGroup()
        {
            Requirements = new List<string>();
        }

        // "" for students with no restrictions that affect meals
        public string Key { get; set; }

        // e.g. "Vegan", "Peanuts allergy", "Coeliac disease"
        public List<string> Requirements { get; set; }

        public int StudentCount { get; set; }

        // Representative profile — every student in the group is
        // filtered identically.
        public StudentDietaryProfile Profile { get; set; }

        public bool IsUnrestricted
        {
            get { return Requirements.Count == 0; }
        }

        public string Label
        {
            get { return IsUnrestricted ? "No dietary restrictions" : string.Join(", ", Requirements); }
        }
    }

    // ============================================================
    // Students who share a sports need AND a dietary group on one
    // day, e.g. 3 vegan rugby players on a match day. A meal covers
    // them when it is in the need's category (e.g. HighProtein) and
    // safe for their diet. Sports never override dietary safety.
    // ============================================================
    public class SportsGroup
    {
        public SportsGroup()
        {
            Activities = new List<string>();
        }

        public DateTime Date { get; set; }
        public NutritionCategory Category { get; set; }

        // The dietary group these students belong to
        public DietaryGroup Dietary { get; set; }

        public int StudentCount { get; set; }

        // e.g. "Rugby vs Hilton (match day)", "Athletics training"
        public List<string> Activities { get; set; }

        public string Key
        {
            get { return Category + "#" + Dietary.Key; }
        }

        // e.g. "3 players for Rugby vs Hilton (match day) — Vegan"
        public string Describe()
        {
            return string.Format("{0} player{1} for {2}{3}",
                StudentCount, StudentCount == 1 ? "" : "s",
                string.Join("; ", Activities),
                Dietary.IsUnrestricted ? "" : " (" + Dietary.Label + ")");
        }
    }

    public class OptionCoverage
    {
        public OptionCoverage()
        {
            SuitableFor = new List<string>();
        }

        public int ScheduleItemId { get; set; }
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public int StudentsSuited { get; set; }

        // Restricted groups this option is suitable for, e.g. "Vegan"
        public List<string> SuitableFor { get; set; }
    }

    public class SlotCoverage
    {
        public SlotCoverage()
        {
            Options = new List<OptionCoverage>();
            Warnings = new List<string>();
        }

        public DateTime Date { get; set; }
        public MealSlot MealSlot { get; set; }
        public int TotalStudents { get; set; }
        public int StudentsWithOption { get; set; }
        public List<OptionCoverage> Options { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class MenuCoverageReport
    {
        public MenuCoverageReport()
        {
            Slots = new List<SlotCoverage>();
            Groups = new List<DietaryGroup>();
        }

        public int TotalStudents { get; set; }
        public List<DietaryGroup> Groups { get; set; }
        public List<SlotCoverage> Slots { get; set; }

        public int SlotsWithWarnings
        {
            get { return Slots.Count(s => s.Warnings.Count > 0); }
        }

        public SlotCoverage For(DateTime date, MealSlot slot)
        {
            return Slots.FirstOrDefault(s => s.Date.Date == date.Date && s.MealSlot == slot);
        }
    }

    public class MenuCoverageService
    {
        private readonly DBContextClass _db;
        private readonly DietaryProfileService _dietary;

        public MenuCoverageService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _dietary = new DietaryProfileService(db);
        }

        // ============================================================
        // POPULATION
        // Same students DemandService counts: active students living
        // in a house (residence allocation — StudentHouseService).
        // ============================================================

        public List<DietaryGroup> LoadPopulation()
        {
            return LoadPopulation(new StudentHouseService(_db).BoardingStudentIds());
        }

        // A given set of students, e.g. those invited to or attending
        // an event (EventBuffetService)
        public List<DietaryGroup> LoadPopulation(ICollection<int> studentIds)
        {
            if (studentIds == null || studentIds.Count == 0)
            {
                return new List<DietaryGroup>();
            }

            var ids = studentIds.Distinct().ToList();
            var students = _db.Students
                .Include(s => s.StudentProfile)
                .Where(s => ids.Contains(s.StudentId))
                .ToList();

            return GroupStudents(students);
        }

        private static List<DietaryGroup> GroupStudents(IEnumerable<Student> students)
        {
            return GroupProfiles(students.Select(s => DietaryProfileService.FromStudentProfile(s.StudentProfile)));
        }

        // Groups any set of dietary profiles — e.g. event attendees
        // (students, parents, staff and guests). Each profile counts once.
        public static List<DietaryGroup> GroupProfiles(IEnumerable<StudentDietaryProfile> profiles)
        {
            var groups = new Dictionary<string, DietaryGroup>();

            foreach (var profile in profiles)
            {
                var requirements = FilteringRequirements(profile);
                var key = string.Join("|", requirements).ToLowerInvariant();

                DietaryGroup group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new DietaryGroup
                    {
                        Key = key,
                        Requirements = requirements,
                        Profile = profile
                    };
                    groups.Add(key, group);
                }

                group.StudentCount++;
            }

            return groups.Values
                .OrderByDescending(g => g.StudentCount)
                .ThenBy(g => g.Label)
                .ToList();
        }

        // The requirements from a profile that can exclude a meal
        public static List<string> FilteringRequirements(StudentDietaryProfile profile)
        {
            var result = new List<string>();

            if (profile.Preference == DietaryProfileService.PreferenceVegetarian
                || profile.Preference == DietaryProfileService.PreferenceVegan
                || profile.Preference == DietaryProfileService.PreferenceHalal)
            {
                result.Add(profile.Preference);
            }

            foreach (var option in DietaryProfileService.AllergyOptions)
            {
                if (profile.Allergies.Contains(option.Code)) result.Add(option.Code + " allergy");
            }

            foreach (var other in profile.OtherAllergies.OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
            {
                if (other.Length >= 3) result.Add(other + " allergy");
            }

            foreach (var code in new[]
            {
                DietaryProfileService.MedicalLactoseIntolerance,
                DietaryProfileService.MedicalGlutenIntolerance,
                DietaryProfileService.MedicalCoeliac
            })
            {
                if (profile.MedicalRestrictions.Contains(code))
                    result.Add(DietaryProfileService.LabelFor(DietaryProfileService.MedicalRestrictionOptions, code));
            }

            return result;
        }

        public bool Suits(DietaryGroup group, MenuItem item)
        {
            return _dietary.IsSafe(group.Profile, item);
        }

        // ============================================================
        // SPORTS GROUPS
        // For every day: the students whose day calls for a nutrition
        // category (match day, day before a match, training, priority
        // week — SportsCalendar.NeedOn), grouped by that category and
        // their dietary group. Same students as LoadPopulation unless
        // given.
        // ============================================================

        public Dictionary<DateTime, List<SportsGroup>> LoadSportsGroups(
            DateTime start, DateTime end, ICollection<int> studentIds = null)
        {
            var result = new Dictionary<DateTime, List<SportsGroup>>();
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1)) result[d] = new List<SportsGroup>();

            var ids = studentIds ?? new StudentHouseService(_db).BoardingStudentIds();
            if (ids.Count == 0) return result;

            var calendar = new StudentSportService(_db).LoadCalendar(start, end, ids);
            var athleteIds = calendar.StudentsWithSports.ToList();
            if (athleteIds.Count == 0) return result;

            // Each athlete's dietary group (same grouping as LoadPopulation)
            var dietaryByStudent = _db.Students
                .Include(s => s.StudentProfile)
                .Where(s => athleteIds.Contains(s.StudentId))
                .ToList()
                .ToDictionary(s => s.StudentId, s => DietaryProfileService.FromStudentProfile(s.StudentProfile));

            foreach (var date in result.Keys.ToList())
            {
                var groups = new Dictionary<string, SportsGroup>();

                foreach (var studentId in athleteIds)
                {
                    var need = calendar.NeedOn(studentId, date);
                    if (!need.Category.HasValue) continue;

                    StudentDietaryProfile profile;
                    if (!dietaryByStudent.TryGetValue(studentId, out profile)) continue;

                    var requirements = FilteringRequirements(profile);
                    var dietaryKey = string.Join("|", requirements).ToLowerInvariant();
                    var key = need.Category.Value + "#" + dietaryKey;

                    SportsGroup group;
                    if (!groups.TryGetValue(key, out group))
                    {
                        group = new SportsGroup
                        {
                            Date = date,
                            Category = need.Category.Value,
                            Dietary = new DietaryGroup { Key = dietaryKey, Requirements = requirements, Profile = profile }
                        };
                        groups.Add(key, group);
                    }

                    group.StudentCount++;
                    group.Dietary.StudentCount++;
                    if (!group.Activities.Contains(need.ManagerLabel)) group.Activities.Add(need.ManagerLabel);
                }

                result[date] = groups.Values.OrderByDescending(g => g.StudentCount).ToList();
            }

            return result;
        }

        // In the group's category and safe for their diet
        public bool SuitsSports(SportsGroup group, MenuItem item)
        {
            return item.NutritionCategory == group.Category && _dietary.IsSafe(group.Dietary.Profile, item);
        }

        public static string CategoryWords(NutritionCategory category)
        {
            switch (category)
            {
                case NutritionCategory.HighProtein: return "high-protein";
                case NutritionCategory.HighCarb: return "high-carb";
                case NutritionCategory.Light: return "light";
                case NutritionCategory.Hydration: return "hydration-focused";
                default: return "standard";
            }
        }

        // ============================================================
        // EVALUATE ONE SLOT
        // ============================================================

        // sportsGroups: that day's sports groups (LoadSportsGroups);
        // each must have an option in its category that suits its diet
        public SlotCoverage Evaluate(
            DateTime date,
            MealSlot slot,
            IList<MenuScheduleItem> scheduled,
            List<DietaryGroup> groups,
            List<SportsGroup> sportsGroups = null)
        {
            var result = new SlotCoverage
            {
                Date = date.Date,
                MealSlot = slot,
                TotalStudents = groups.Sum(g => g.StudentCount)
            };

            var covered = new HashSet<string>();

            foreach (var s in scheduled)
            {
                var item = s.MenuItem;
                if (item == null) continue;

                var option = new OptionCoverage
                {
                    ScheduleItemId = s.Id,
                    MenuItemId = item.Id,
                    Name = item.Name
                };

                foreach (var group in groups)
                {
                    if (!Suits(group, item)) continue;

                    option.StudentsSuited += group.StudentCount;
                    covered.Add(group.Key);
                }

                // Which of the three preference groups can eat it
                foreach (var pref in new[]
                {
                    DietaryProfileService.PreferenceVegetarian,
                    DietaryProfileService.PreferenceVegan,
                    DietaryProfileService.PreferenceHalal
                })
                {
                    var probe = new StudentDietaryProfile { Preference = pref };
                    if (_dietary.IsSafe(probe, item)) option.SuitableFor.Add(pref);
                }

                result.Options.Add(option);
            }

            result.StudentsWithOption = groups.Where(g => covered.Contains(g.Key)).Sum(g => g.StudentCount);
            result.Warnings = BuildWarnings(date, slot, scheduled.Count, groups.Where(g => !covered.Contains(g.Key)).ToList());

            // Sports needs — only meaningful once there are options
            if (scheduled.Count > 0 && sportsGroups != null)
            {
                var slotLabel = date.ToString("dddd dd MMM") + " " + slot;
                var items = scheduled.Where(s => s.MenuItem != null).Select(s => s.MenuItem).ToList();

                foreach (var group in sportsGroups.Where(g => g.StudentCount > 0 && !items.Any(i => SuitsSports(g, i))))
                {
                    result.Warnings.Add(string.Format(
                        "No {0} option for {1}: {2}.",
                        CategoryWords(group.Category), slotLabel, group.Describe()));
                }
            }

            return result;
        }

        private static List<string> BuildWarnings(DateTime date, MealSlot slot, int optionCount, List<DietaryGroup> uncovered)
        {
            var warnings = new List<string>();
            var slotLabel = date.ToString("dddd dd MMM") + " " + slot;

            if (optionCount == 0)
            {
                warnings.Add(string.Format("No options are scheduled for {0}.", slotLabel));
                return warnings;
            }

            if (uncovered.Count == 0 || uncovered.Sum(g => g.StudentCount) == 0)
            {
                return warnings;
            }

            warnings.Add(string.Format(
                "The current {0} options do not accommodate all identified dietary groups.", slotLabel));

            foreach (var group in uncovered.OrderByDescending(g => g.StudentCount))
            {
                bool onlyPreference = group.Requirements.Count == 1
                    && (group.Requirements[0] == DietaryProfileService.PreferenceVegetarian
                        || group.Requirements[0] == DietaryProfileService.PreferenceVegan
                        || group.Requirements[0] == DietaryProfileService.PreferenceHalal);

                if (onlyPreference)
                {
                    warnings.Add(string.Format(
                        "No suitable {0} option is available for {1} ({2} student{3}).",
                        group.Requirements[0].ToLowerInvariant(), slotLabel,
                        group.StudentCount, group.StudentCount == 1 ? "" : "s"));
                }
                else if (group.Requirements.Count == 1)
                {
                    warnings.Add(string.Format(
                        "Students with {0} ({1}) may have no suitable option for {2}.",
                        group.Requirements[0], group.StudentCount, slotLabel));
                }
                else
                {
                    warnings.Add(string.Format(
                        "{0} student{1} ({2}) may have no suitable option for {3}.",
                        group.StudentCount, group.StudentCount == 1 ? "" : "s", group.Label, slotLabel));
                }
            }

            return warnings;
        }

        // ============================================================
        // EVALUATE A WHOLE MENU (Review page)
        // ============================================================

        public MenuCoverageReport AnalyseMenu(int mealMenuId)
        {
            var menu = _db.MealMenus.FirstOrDefault(m => m.Id == mealMenuId);
            if (menu == null) return null;

            var items = _db.MenuScheduleItems
                .Include(s => s.MenuItem.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .Where(s => s.MealMenuId == mealMenuId)
                .ToList();

            var groups = LoadPopulation();

            // Fixtures as they are now, so a match added or cancelled
            // after generation shows up here
            var sportsGroups = LoadSportsGroups(menu.StartDate, menu.EndDate);

            var report = new MenuCoverageReport
            {
                Groups = groups,
                TotalStudents = groups.Sum(g => g.StudentCount)
            };

            for (var date = menu.StartDate.Date; date <= menu.EndDate.Date; date = date.AddDays(1))
            {
                foreach (var slot in new[] { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Dinner })
                {
                    var scheduled = items
                        .Where(s => s.Date.Date == date && s.MealSlot == slot)
                        .OrderBy(s => s.Id)
                        .ToList();

                    report.Slots.Add(Evaluate(date, slot, scheduled, groups, sportsGroups[date]));
                }
            }

            return report;
        }
    }
}
