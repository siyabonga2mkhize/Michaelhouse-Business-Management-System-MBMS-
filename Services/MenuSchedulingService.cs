using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    public class MenuSchedulingService
    {
        private readonly DBContextClass _db;

        private const decimal SportsMultiplier = 1.30m;

        private static readonly MealSlot[] GeneratedMealSlots =
        {
            MealSlot.Breakfast,
            MealSlot.Lunch,
            MealSlot.Dinner
        };

        private const int MealCooldownDays = 2;

        public MenuSchedulingService(DBContextClass db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            _db = db;
        }

        // ============================================================
        // GENERATE MENU
        //
        // A menu is a set of OPTIONS per meal slot — the choices
        // students pick from later in their meal plans. Nobody is
        // assigned a meal here.
        //
        // For every (day, slot) the generator picks up to
        // OptionsPerSlot dishes that together:
        //   1. include a main option that suits the day's sports needs
        //      (match day, day before a match, training, priority sport)
        //   2. give every dietary group of students at least one
        //      suitable choice (vegetarian, vegan, halal, allergies,
        //      lactose / gluten / coeliac) — see MenuCoverageService
        //   3. stay varied (no repeats in a day, 2-day lunch/dinner
        //      cooldown, rotation across the catalogue)
        //
        // Stock is NOT used here; the Chef / stock / production steps
        // happen after students have chosen. Portions per option are
        // an estimate split from the slot's demand.
        // ============================================================

        public const int DefaultOptionsPerSlot = 3;
        public const int MaxOptionsPerSlot = 6;

        public MealMenu GenerateMenu(
            ScheduleMenuInputViewModel input,
            string rejectionReason = null,
            int? regeneratedFromMenuId = null)
        {
            ValidateInput(input);

            return GenerateMenuInternal(
                input,
                rejectionReason,
                regeneratedFromMenuId,
                null);
        }

        private MealMenu GenerateMenuInternal(
            ScheduleMenuInputViewModel input,
            string rejectionReason,
            int? regeneratedFromMenuId,
            List<RejectedSelection> rejectedSelections)
        {
            var activeMenuItems = _db.MenuItems
                .Where(x => x.IsActive)
                .Include(x => x.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .OrderBy(x => x.Name)
                .ToList();

            if (!activeMenuItems.Any())
            {
                throw new InvalidOperationException(
                    "No active MenuItem records exist. Add menu items before generating a menu.");
            }

            foreach (MealSlot mealSlot in GeneratedMealSlots)
            {
                if (!activeMenuItems.Any(x => IsCompatibleWithMealSlot(x, mealSlot)))
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "No active menu items are configured for the {0} meal slot.",
                            mealSlot));
                }
            }

            int optionsPerSlot = input.OptionsPerSlot > 0
                ? Math.Min(input.OptionsPerSlot, MaxOptionsPerSlot)
                : DefaultOptionsPerSlot;

            // ─────────────────────────────────────────────────────────
            // UC12: per-day, per-meal portion demand (split across the
            // options later as an estimate).
            // ─────────────────────────────────────────────────────────
            var demandService = new DemandService(_db);
            var allDemand = demandService.ComputeRange(
                input.StartDate, input.EndDate, input.StaffMeals);

            var demandLookup = new Dictionary<string, MealDemand>();
            foreach (var d in allDemand)
            {
                var key = d.Date.ToString("yyyy-MM-dd") + "|" + d.MealSlot;
                if (!demandLookup.ContainsKey(key))
                {
                    demandLookup.Add(key, d);
                }
            }

            // ─────────────────────────────────────────────────────────
            // Dietary groups of students, and which groups each dish
            // suits. Computed once — profiles don't change by date.
            // ─────────────────────────────────────────────────────────
            var coverage = new MenuCoverageService(_db);
            var groups = coverage.LoadPopulation();
            int totalStudents = groups.Sum(g => g.StudentCount);

            var suitability = activeMenuItems.ToDictionary(
                x => x.Id,
                x => new HashSet<string>(groups.Where(g => coverage.Suits(g, x)).Select(g => g.Key)));

            // ─────────────────────────────────────────────────────────
            // Sports context for every day in the range
            // ─────────────────────────────────────────────────────────
            var sportsDays = BuildSportsDays(input, totalStudents);

            var menu = new MealMenu
            {
                StartDate = input.StartDate.Date,
                EndDate = input.EndDate.Date,
                StaffMeals = input.StaffMeals,
                SpecialEventNotes = input.SpecialEventNotes,
                MenuStatus = MenuStatus.PendingReview,
                RejectionReason = rejectionReason,
                RegeneratedFromMenuId = regeneratedFromMenuId,
                CreatedDate = DateTime.UtcNow
            };

            _db.MealMenus.Add(menu);
            _db.SaveChanges();

            // ── Variety: what was offered recently ──
            var itemUsage = new Dictionary<int, List<MealUsage>>();

            DateTime historicalStart =
                input.StartDate.Date.AddDays(-MealCooldownDays);

            var historicalUsages = _db.MenuScheduleItems
                .Where(x => x.MealMenu.MenuStatus == MenuStatus.Accepted)
                .Where(x => x.Date >= historicalStart &&
                            x.Date < input.StartDate.Date)
                .Select(x => new { x.MenuItemId, x.Date, x.MealSlot })
                .ToList();

            foreach (var usage in historicalUsages)
            {
                RecordItemUsage(itemUsage, usage.MenuItemId, usage.Date, usage.MealSlot);
            }

            int generationIndex = 0;

            for (DateTime date = input.StartDate.Date;
                 date <= input.EndDate.Date;
                 date = date.AddDays(1))
            {
                SportsDay sportsDay;
                if (!sportsDays.TryGetValue(date, out sportsDay)) sportsDay = new SportsDay();

                var usedToday = new HashSet<int>();

                foreach (MealSlot mealSlot in GeneratedMealSlots)
                {
                    var chosen = SelectOptions(
                        activeMenuItems,
                        optionsPerSlot,
                        generationIndex,
                        mealSlot,
                        date,
                        sportsDay,
                        groups,
                        suitability,
                        itemUsage,
                        usedToday,
                        rejectedSelections);

                    generationIndex++;

                    int slotPortions = GetPortionsForSlot(
                        demandLookup, date, mealSlot, input.StaffMeals);

                    var portions = SplitPortions(chosen, groups, suitability, slotPortions);

                    for (int i = 0; i < chosen.Count; i++)
                    {
                        var option = chosen[i];

                        _db.MenuScheduleItems.Add(new MenuScheduleItem
                        {
                            MealMenuId = menu.Id,
                            MenuItemId = option.Item.Id,
                            Date = date,
                            MealSlot = mealSlot,
                            CalculatedPortions = portions[i],
                            ItemTagStatus = ItemTagStatus.Confirmed,
                            TagReason = BuildOptionReason(option, sportsDay, groups, suitability, totalStudents),
                            SubstitutionMenuItemId = null
                        });

                        usedToday.Add(option.Item.Id);
                        RecordItemUsage(itemUsage, option.Item.Id, date, mealSlot);
                    }
                }
            }

            _db.SaveChanges();

            return GetMenu(menu.Id);
        }

        private int GetPortionsForSlot(
            Dictionary<string, MealDemand> demandLookup,
            DateTime date,
            MealSlot slot,
            int staffMeals)
        {
            var key = date.ToString("yyyy-MM-dd") + "|" + slot.ToString();

            MealDemand demand;
            if (demandLookup.TryGetValue(key, out demand))
            {
                return demand.TotalPortions;
            }

            return Math.Max(1, staffMeals);
        }

        // ============================================================
        // OPTION SELECTION FOR ONE SLOT
        // ============================================================

        private class ChosenOption
        {
            public MenuItem Item { get; set; }

            // Why it was chosen — shown on the Review page
            public string Role { get; set; }

            public bool IsSportsOption { get; set; }
        }

        private List<ChosenOption> SelectOptions(
            List<MenuItem> allItems,
            int optionsPerSlot,
            int generationIndex,
            MealSlot slot,
            DateTime date,
            SportsDay sportsDay,
            List<DietaryGroup> groups,
            Dictionary<int, HashSet<string>> suitability,
            Dictionary<int, List<MealUsage>> itemUsage,
            HashSet<int> usedToday,
            List<RejectedSelection> rejectedSelections)
        {
            var compatible = allItems.Where(x => IsCompatibleWithMealSlot(x, slot)).ToList();

            // A dish is offered once per day (relaxed only if the
            // catalogue has nothing else).
            var pool = compatible
                .Where(x => !usedToday.Contains(x.Id))
                .ToList();

            if (pool.Count == 0)
            {
                pool = compatible;
            }

            // Rotation: start at a different point in the catalogue for
            // every slot so the week stays varied.
            int n = compatible.Count;
            int start = (generationIndex * optionsPerSlot) % n;
            Func<MenuItem, int> rotation = x => (compatible.IndexOf(x) - start + n) % n;

            // Avoid, but don't forbid: dishes the manager rejected for
            // this slot (regeneration) and dishes in the 2-day cooldown.
            // Candidates come from the lowest tier available.
            Func<MenuItem, int> tier = x =>
                (WasSelectionRejected(rejectedSelections, x.Id, date, slot) ? 2 : 0)
                + (IsWithinCooldown(itemUsage, x.Id, date, slot) ? 1 : 0);

            Func<IEnumerable<MenuItem>, List<MenuItem>> bestTier = items =>
            {
                var list = items.ToList();
                if (list.Count == 0) return list;
                int min = list.Min(tier);
                return list.Where(x => tier(x) == min).ToList();
            };

            NutritionCategory? preferred = sportsDay.PreferredCategory;

            // A large share of students playing → two sports options
            int sportsOptionsWanted = !preferred.HasValue ? 0
                : (sportsDay.PlayerShare >= LargeSportsGroupShare && optionsPerSlot >= 3 ? 2 : 1);

            var chosen = new List<ChosenOption>();
            var covered = new HashSet<string>();

            for (int k = 0; k < optionsPerSlot; k++)
            {
                var remaining = pool.Where(x => !chosen.Any(c => c.Item.Id == x.Id)).ToList();
                if (remaining.Count == 0) break;

                var fresh = bestTier(remaining);

                int sportsChosen = chosen.Count(c => c.IsSportsOption);
                bool wantSports = preferred.HasValue && sportsChosen < sportsOptionsWanted;

                var chosenClassifications = new HashSet<string>(chosen.Select(c => c.Item.DietaryClassification ?? ""), StringComparer.OrdinalIgnoreCase);
                var chosenCategories = new HashSet<NutritionCategory>(chosen.Select(c => c.Item.NutritionCategory));

                Func<MenuItem, int> sportsFit = x => wantSports && x.NutritionCategory == preferred.Value ? 1 : 0;
                Func<MenuItem, int> diversity = x =>
                    (chosenClassifications.Contains(x.DietaryClassification ?? "") ? 0 : 1)
                    + (chosenCategories.Contains(x.NutritionCategory) ? 0 : 1);
                Func<MenuItem, int> newlyCovered = x => groups
                    .Where(g => !covered.Contains(g.Key) && suitability[x.Id].Contains(g.Key))
                    .Sum(g => g.StudentCount);

                MenuItem pick;
                string role;

                bool anyUncovered = groups.Any(g => !covered.Contains(g.Key) && g.StudentCount > 0);

                if (k == 0)
                {
                    // Main option: sports need first, then rotation
                    pick = fresh
                        .OrderByDescending(sportsFit)
                        .ThenBy(rotation)
                        .First();

                    role = sportsFit(pick) == 1 ? SportsRole(sportsDay) : "Main option";
                }
                else if (anyUncovered && remaining.Any(x => newlyCovered(x) > 0))
                {
                    // Dietary coverage: the dish that gives the most
                    // still-uncovered students a suitable choice.
                    var coverCandidates = bestTier(remaining.Where(x => newlyCovered(x) > 0));

                    pick = coverCandidates
                        .OrderByDescending(newlyCovered)
                        .ThenByDescending(sportsFit)
                        .ThenByDescending(diversity)
                        .ThenBy(rotation)
                        .First();

                    var newGroups = groups
                        .Where(g => !covered.Contains(g.Key) && !g.IsUnrestricted && suitability[pick.Id].Contains(g.Key))
                        .OrderByDescending(g => g.StudentCount)
                        .Select(g => g.Label)
                        .Take(3)
                        .ToList();

                    role = newGroups.Count > 0
                        ? "Added so these students have a choice: " + string.Join("; ", newGroups)
                        : "Additional choice";
                }
                else
                {
                    // Everyone is covered — fill with sports / variety
                    pick = fresh
                        .OrderByDescending(sportsFit)
                        .ThenByDescending(diversity)
                        .ThenBy(rotation)
                        .First();

                    role = sportsFit(pick) == 1 ? SportsRole(sportsDay) : "Additional choice";
                }

                bool isSports = sportsFit(pick) == 1;

                chosen.Add(new ChosenOption { Item = pick, Role = role, IsSportsOption = isSports });

                foreach (var key in suitability[pick.Id]) covered.Add(key);
            }

            return chosen;
        }

        // Estimated portions per option: each dietary group's students
        // are shared across the options that suit them; the slot total
        // (students + staff + sports uplift) is split in that proportion.
        // Actual numbers come from student meal plans.
        private static List<int> SplitPortions(
            List<ChosenOption> chosen,
            List<DietaryGroup> groups,
            Dictionary<int, HashSet<string>> suitability,
            int slotPortions)
        {
            var weights = new double[chosen.Count];

            foreach (var group in groups)
            {
                var suitable = Enumerable.Range(0, chosen.Count)
                    .Where(i => suitability[chosen[i].Item.Id].Contains(group.Key))
                    .ToList();

                if (suitable.Count == 0) continue;

                double share = (double)group.StudentCount / suitable.Count;
                foreach (var i in suitable) weights[i] += share;
            }

            double total = weights.Sum();
            if (total <= 0)
            {
                for (int i = 0; i < weights.Length; i++) weights[i] = 1;
                total = weights.Length;
            }

            // Largest-remainder rounding so the options add up to the slot total
            var exact = weights.Select(w => slotPortions * w / total).ToArray();
            var result = exact.Select(e => (int)Math.Floor(e)).ToList();
            int leftover = slotPortions - result.Sum();

            foreach (var i in Enumerable.Range(0, exact.Length)
                         .OrderByDescending(i => exact[i] - Math.Floor(exact[i]))
                         .Take(Math.Max(0, leftover)))
            {
                result[i]++;
            }

            // MenuScheduleItem requires at least one portion
            return result.Select(r => Math.Max(1, r)).ToList();
        }

        private static string BuildOptionReason(
            ChosenOption option,
            SportsDay sportsDay,
            List<DietaryGroup> groups,
            Dictionary<int, HashSet<string>> suitability,
            int totalStudents)
        {
            // MealPlanService looks for "Match-day" / "Pre-match" at the
            // start of the reason to rank options for athletes.
            var prefix = sportsDay.ReasonPrefix ?? "";

            var suited = groups
                .Where(g => suitability[option.Item.Id].Contains(g.Key))
                .Sum(g => g.StudentCount);

            return string.Format(
                "{0}{1}. Suitable for {2} of {3} students.",
                prefix, option.Role, suited, totalStudents);
        }

        // ============================================================
        // SPORTS CONTEXT
        //
        // Uses SportEvent fixtures (plus match dates entered on the
        // Schedule form), StudentSportStatus and SportPriority.
        //
        //   Match day          → category from the playing sports'
        //                        archetype: Power → HighProtein,
        //                        Endurance → HighCarb, Skill → Light,
        //                        Speed → Hydration
        //   Day before a match → HighCarb (carb loading)
        //   Training day       → Power → HighProtein, Endurance → HighCarb
        //   Priority sport week→ its archetype's category
        // ============================================================

        // Share of students playing that justifies a second sports option
        private const double LargeSportsGroupShare = 0.40;

        private class SportsDay
        {
            public NutritionCategory? PreferredCategory { get; set; }

            // e.g. "Match-day recovery (Rugby vs Hilton). "
            public string ReasonPrefix { get; set; }

            // e.g. "Rugby vs Hilton"
            public string Description { get; set; }

            // Share of students involved (0..1)
            public double PlayerShare { get; set; }
        }

        private static string SportsRole(SportsDay day)
        {
            return string.Format("Sports nutrition option ({0}{1})",
                day.PreferredCategory,
                string.IsNullOrEmpty(day.Description) ? "" : " — " + day.Description);
        }

        private Dictionary<DateTime, SportsDay> BuildSportsDays(ScheduleMenuInputViewModel input, int totalStudents)
        {
            var start = input.StartDate.Date;
            var end = input.EndDate.Date;
            var afterEnd = end.AddDays(2);   // day-before needs tomorrow's fixtures

            var fixtures = _db.SportEvents
                .Where(e => !e.IsCancelled && e.ScheduledDate >= start && e.ScheduledDate < afterEnd)
                .ToList()
                .Where(e => string.IsNullOrEmpty(e.Status) || e.Status == "Scheduled")
                .ToList();

            var statuses = _db.StudentSportStatuses.ToList();

            var archetypeBySport = statuses
                .GroupBy(s => s.Sport, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Archetype, StringComparer.OrdinalIgnoreCase);

            // Priority sports this week (existing UC12 behaviour)
            var prioritySports = _db.SportPriorities
                .Where(p => p.PriorityLevel == 2)
                .Where(p => p.WeekStartDate <= end && p.WeekEndDate >= start)
                .Select(p => p.Sport)
                .ToList();

            // Match dates entered / confirmed on the Schedule form
            var formMatches = (input.BoardingHouses ?? new List<BoardingHouseScheduleOption>())
                .Where(h => h != null && h.IsInSeason && h.MatchDate.HasValue && !string.IsNullOrWhiteSpace(h.ActiveSport))
                .Select(h => new { Date = h.MatchDate.Value.Date, Sport = h.ActiveSport.Trim() })
                .ToList();

            Func<DateTime, List<string>> matchLabelsOn = d => fixtures
                .Where(e => e.EventType == "Match" && e.ScheduledDate.Date == d)
                .Select(e => string.IsNullOrEmpty(e.Opponent) ? e.Sport : e.Sport + " vs " + e.Opponent)
                .Concat(formMatches.Where(m => m.Date == d).Select(m => m.Sport))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Func<DateTime, List<string>> matchSportsOn = d => fixtures
                .Where(e => e.EventType == "Match" && e.ScheduledDate.Date == d)
                .Select(e => e.Sport)
                .Concat(formMatches.Where(m => m.Date == d).Select(m => m.Sport))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Func<DateTime, List<string>> trainingSportsOn = d => fixtures
                .Where(e => e.EventType == "Training" && e.ScheduledDate.Date == d)
                .Select(e => e.Sport)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Active players per sport on a date (injury-aware)
            Func<DateTime, IEnumerable<string>, List<StudentSportStatus>> playersOn = (d, sports) =>
            {
                var set = new HashSet<string>(sports, StringComparer.OrdinalIgnoreCase);
                return statuses
                    .Where(s => set.Contains(s.Sport) && IsSportStatusActiveOn(s, d))
                    .ToList();
            };

            Func<List<StudentSportStatus>, SportArchetype> dominantArchetype = players =>
            {
                var top = players
                    .GroupBy(p => p.Archetype)
                    .OrderByDescending(g => g.Select(p => p.StudentId).Distinct().Count())
                    .FirstOrDefault();

                return top != null ? top.Key : SportArchetype.None;
            };

            Func<List<StudentSportStatus>, double> shareOf = players =>
                totalStudents > 0 ? (double)players.Select(p => p.StudentId).Distinct().Count() / totalStudents : 0;

            var result = new Dictionary<DateTime, SportsDay>();

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var day = new SportsDay();

                var todaySports = matchSportsOn(date);
                var tomorrowSports = matchSportsOn(date.AddDays(1));
                var trainingSports = trainingSportsOn(date);

                if (todaySports.Count > 0)
                {
                    var players = playersOn(date, todaySports);
                    day.PreferredCategory = MatchDayCategory(dominantArchetype(players));
                    day.Description = string.Join(", ", matchLabelsOn(date));
                    day.ReasonPrefix = "Match-day recovery (" + day.Description + "). ";
                    day.PlayerShare = shareOf(players);
                }
                else if (tomorrowSports.Count > 0)
                {
                    var players = playersOn(date.AddDays(1), tomorrowSports);
                    day.PreferredCategory = NutritionCategory.HighCarb;
                    day.Description = string.Join(", ", matchLabelsOn(date.AddDays(1)));
                    day.ReasonPrefix = "Pre-match carb-loading (" + day.Description + "). ";
                    day.PlayerShare = shareOf(players);
                }
                else if (trainingSports.Count > 0)
                {
                    var players = playersOn(date, trainingSports);
                    var category = TrainingCategory(dominantArchetype(players));

                    if (category.HasValue)
                    {
                        day.PreferredCategory = category;
                        day.Description = "Training: " + string.Join(", ", trainingSports);
                        day.PlayerShare = shareOf(players);
                    }
                }

                if (!day.PreferredCategory.HasValue && prioritySports.Count > 0)
                {
                    var players = playersOn(date, prioritySports);
                    var category = TrainingCategory(dominantArchetype(players));

                    if (category.HasValue)
                    {
                        day.PreferredCategory = category;
                        day.Description = "Priority: " + string.Join(", ", prioritySports.Distinct());
                        day.PlayerShare = shareOf(players);
                    }
                }

                result[date] = day;
            }

            return result;
        }

        // Also used by MealPlanService for a student's own fixtures.
        public static NutritionCategory MatchDayCategory(SportArchetype archetype)
        {
            switch (archetype)
            {
                case SportArchetype.Endurance: return NutritionCategory.HighCarb;
                case SportArchetype.Skill: return NutritionCategory.Light;
                case SportArchetype.Speed: return NutritionCategory.Hydration;
                default: return NutritionCategory.HighProtein;   // Power / unknown
            }
        }

        public static NutritionCategory? TrainingCategory(SportArchetype archetype)
        {
            switch (archetype)
            {
                case SportArchetype.Power: return NutritionCategory.HighProtein;
                case SportArchetype.Endurance: return NutritionCategory.HighCarb;
                default: return null;
            }
        }

        // Same rule as DemandService: a return date in the past means
        // the student is available again.
        private static bool IsSportStatusActiveOn(StudentSportStatus status, DateTime date)
        {
            if (status.UnavailableUntil.HasValue && status.UnavailableUntil.Value.Date <= date.Date)
            {
                return true;
            }

            return status.IsActive;
        }


        // ============================================================
        // MENU RETRIEVAL / ACCEPT / REJECT
        // ============================================================

        public MealMenu GetMenu(int id)
        {
            return _db.MealMenus
                .Include(x => x.ScheduleItems.Select(y => y.MenuItem))
                .Include(x => x.ScheduleItems.Select(y => y.SubstitutionMenuItem))
                .FirstOrDefault(x => x.Id == id);
        }

        public MealMenu AcceptMenu(int id)
        {
            MealMenu menu = GetMenu(id);

            if (menu == null)
            {
                throw new InvalidOperationException("Menu was not found.");
            }

            if (menu.MenuStatus == MenuStatus.Rejected)
            {
                throw new InvalidOperationException(
                    "A rejected menu cannot be accepted. Generate its replacement first.");
            }

            menu.MenuStatus = MenuStatus.Accepted;
            menu.LastModifiedDate = DateTime.UtcNow;

            _db.SaveChanges();

            return menu;
        }

        public MealMenu RejectMenu(int id, string reason)
        {
            MealMenu menu = GetMenu(id);

            if (menu == null)
            {
                throw new InvalidOperationException("Menu was not found.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "A rejection reason is required.", nameof(reason));
            }

            menu.MenuStatus = MenuStatus.Rejected;
            menu.RejectionReason = reason.Trim();
            menu.LastModifiedDate = DateTime.UtcNow;

            _db.SaveChanges();

            return menu;
        }

        // ============================================================
        // REGENERATE REJECTED MENU
        // ============================================================

        public MealMenu RegenerateRejectedMenu(int id, string reason)
        {
            MealMenu rejectedMenu = GetMenu(id);

            if (rejectedMenu == null)
            {
                throw new InvalidOperationException("Menu was not found.");
            }

            if (rejectedMenu.MenuStatus != MenuStatus.Rejected)
            {
                throw new InvalidOperationException(
                    "Only rejected menus can be regenerated.");
            }

            var input = new ScheduleMenuInputViewModel
            {
                StartDate = rejectedMenu.StartDate,
                EndDate = rejectedMenu.EndDate,
                StaffMeals = rejectedMenu.StaffMeals,
                SpecialEventNotes = rejectedMenu.SpecialEventNotes,
                BoardingHouses = new List<BoardingHouseScheduleOption>(),

                // Keep the same number of options per slot
                OptionsPerSlot = rejectedMenu.ScheduleItems.Any()
                    ? rejectedMenu.ScheduleItems.GroupBy(x => new { x.Date, x.MealSlot }).Max(g => g.Count())
                    : DefaultOptionsPerSlot
            };

            var rejectedSelections = rejectedMenu.ScheduleItems
                .Select(x => new RejectedSelection
                {
                    MenuItemId = x.MenuItemId,
                    Date = x.Date.Date,
                    MealSlot = x.MealSlot
                })
                .ToList();

            return GenerateMenuInternal(
                input,
                reason,
                rejectedMenu.Id,
                rejectedSelections);
        }

        // ============================================================
        // MODIFY MENU ITEM
        // ============================================================

        public MenuScheduleItem ModifyItem(
            int scheduleItemId,
            int newMenuItemId,
            int newPortions)
        {
            if (newPortions <= 0)
            {
                throw new ArgumentException(
                    "Portions must be greater than zero.", nameof(newPortions));
            }

            MenuScheduleItem scheduleItem = _db.MenuScheduleItems
                .Include(x => x.MealMenu)
                .Include(x => x.MenuItem)
                .FirstOrDefault(x => x.Id == scheduleItemId);

            if (scheduleItem == null)
            {
                throw new InvalidOperationException("The schedule item was not found.");
            }

            if (scheduleItem.MealMenu == null)
            {
                throw new InvalidOperationException(
                    "The schedule item is not associated with a menu.");
            }

            if (scheduleItem.MealMenu.MenuStatus == MenuStatus.Accepted)
            {
                throw new InvalidOperationException("Accepted menus cannot be modified.");
            }

            MenuItem replacement = _db.MenuItems
                .Include(x => x.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .FirstOrDefault(x => x.Id == newMenuItemId && x.IsActive);

            if (replacement == null)
            {
                throw new InvalidOperationException(
                    "The selected menu item does not exist or is inactive.");
            }

            if (!IsCompatibleWithMealSlot(replacement, scheduleItem.MealSlot))
            {
                throw new InvalidOperationException(
                    string.Format(
                        "{0} is not configured as a {1} menu item.",
                        replacement.Name,
                        scheduleItem.MealSlot));
            }

            bool alreadyUsedSameDay = _db.MenuScheduleItems.Any(x =>
                x.MealMenuId == scheduleItem.MealMenuId &&
                x.Id != scheduleItem.Id &&
                x.Date == scheduleItem.Date &&
                x.MenuItemId == replacement.Id);

            if (alreadyUsedSameDay)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "{0} is already scheduled on {1:dd MMM yyyy}.",
                        replacement.Name, scheduleItem.Date));
            }

            // With several options per slot a hard cooldown can leave the
            // manager nothing to choose, so a repeat is allowed and noted
            // on the option (the generator treats cooldown the same way).
            bool repeatsRecentDish = false;

            if (IsCooldownMealSlot(scheduleItem.MealSlot))
            {
                DateTime cooldownStart =
                    scheduleItem.Date.Date.AddDays(-MealCooldownDays);

                bool usedDuringCooldown = _db.MenuScheduleItems.Any(x =>
                    x.Id != scheduleItem.Id &&
                    x.MenuItemId == replacement.Id &&
                    x.Date >= cooldownStart &&
                    x.Date < scheduleItem.Date.Date &&
                    (x.MealSlot == MealSlot.Lunch || x.MealSlot == MealSlot.Dinner) &&
                    (x.MealMenuId == scheduleItem.MealMenuId ||
                     x.MealMenu.MenuStatus == MenuStatus.Accepted));

                repeatsRecentDish = usedDuringCooldown;
            }

            // Stock is not part of menu scheduling — the option is
            // confirmed as a choice. Keep the slot's match-day context
            // (MealPlanService reads it) and record the manual change.
            scheduleItem.ItemTagStatus = ItemTagStatus.Confirmed;
            scheduleItem.TagReason = SportsContextPrefix(scheduleItem.TagReason)
                + "Chosen by the cafeteria manager."
                + (repeatsRecentDish
                    ? string.Format(" Note: also offered within the previous {0} days.", MealCooldownDays)
                    : "");

            scheduleItem.MenuItemId = replacement.Id;
            scheduleItem.CalculatedPortions = newPortions;
            scheduleItem.SubstitutionMenuItemId = null;

            scheduleItem.MealMenu.MenuStatus = MenuStatus.PendingReview;
            scheduleItem.MealMenu.LastModifiedDate = DateTime.UtcNow;

            _db.SaveChanges();

            return scheduleItem;
        }

        // "Match-day recovery (Rugby vs Hilton). " etc. — the leading
        // sentence the generator writes for sports days.
        private static string SportsContextPrefix(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "";

            if (!reason.StartsWith("Match-day") && !reason.StartsWith("Pre-match")) return "";

            int end = reason.IndexOf(". ");
            return end > 0 ? reason.Substring(0, end + 2) : "";
        }

        // ============================================================
        // CHEF SUBSTITUTION LOOKUP
        // ============================================================

        public List<SubstituteOption> GetValidSubstitutes(int scheduleItemId)
        {
            var item = _db.MenuScheduleItems
                .Include(x => x.MealMenu)
                .Include(x => x.MenuItem)
                .FirstOrDefault(x => x.Id == scheduleItemId);

            if (item == null)
            {
                throw new InvalidOperationException("Schedule item was not found.");
            }

            var candidates = _db.MenuItems
                .Where(x => x.IsActive)
                .Include(x => x.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .ToList();

            var ingredientStock = _db.Ingredients
                .Where(i => i.IsActive)
                .ToDictionary(
                    i => i.Id,
                    i => new IngredientState
                    {
                        Ingredient = i,
                        FarmAvailable = i.FarmAvailableQuantity,
                        ExternalAvailable = i.ExternalAvailableQuantity
                    });

            var otherItems = _db.MenuScheduleItems
                .Include(x => x.MenuItem.Recipe.RecipeIngredients)
                .Where(x => x.MealMenuId == item.MealMenuId && x.Id != item.Id)
                .ToList();

            foreach (var other in otherItems)
            {
                DrawIngredientStock(
                    other.MenuItem,
                    other.CalculatedPortions,
                    ingredientStock);
            }

            var cooldownStart = item.Date.Date.AddDays(-MealCooldownDays);

            var historicalUsages = _db.MenuScheduleItems
                .Where(x => x.MealMenu.MenuStatus == MenuStatus.Accepted)
                .Where(x => x.Date >= cooldownStart && x.Date < item.Date.Date)
                .Select(x => new { x.MenuItemId, x.Date, x.MealSlot })
                .ToList();

            var currentMenuUsages = otherItems
                .Where(x => x.Date.Date >= cooldownStart && x.Date.Date < item.Date.Date)
                .Select(x => new { x.MenuItemId, x.Date, x.MealSlot })
                .ToList();

            var allUsages = historicalUsages
                .Concat(currentMenuUsages)
                .ToList();

            var results = new List<SubstituteOption>();

            foreach (var candidate in candidates)
            {
                if (!IsCompatibleWithMealSlot(candidate, item.MealSlot))
                {
                    continue;
                }

                if (candidate.Id == item.MenuItemId)
                {
                    continue;
                }

                bool sameDayUsed = otherItems.Any(x =>
                    x.Date.Date == item.Date.Date &&
                    x.MenuItemId == candidate.Id);

                if (sameDayUsed)
                {
                    continue;
                }

                var option = new SubstituteOption
                {
                    MenuItemId = candidate.Id,
                    Name = candidate.Name
                };

                bool cooldownOk = true;

                if (IsCooldownMealSlot(item.MealSlot))
                {
                    cooldownOk = !allUsages.Any(u =>
                        u.MenuItemId == candidate.Id &&
                        IsCooldownMealSlot(u.MealSlot));
                }

                option.IsCooldownOk = cooldownOk;

                var shortages = GetIngredientShortages(
                    candidate,
                    item.CalculatedPortions,
                    ingredientStock);

                // Stock is shown for information only: what's served is
                // decided by the menu, and stock is bought to match
                // (cafeteria inventory) — not the other way round.
                option.IsInStock = shortages.Count == 0;

                string stockNote = option.IsInStock
                    ? "in stock"
                    : shortages.OrderByDescending(s => s.Required - s.Available).First().Name + " currently short — order needed";

                if (!cooldownOk)
                {
                    option.IsValid = false;
                    option.Reason = string.Format("used within {0} days · {1}", MealCooldownDays, stockNote);
                }
                else
                {
                    option.IsValid = true;
                    option.Reason = "cooldown OK · " + stockNote;
                }

                results.Add(option);
            }

            return results
                .OrderByDescending(x => x.IsValid)
                .ThenBy(x => x.Name)
                .ToList();
        }

        // ============================================================
        // PORTION CALCULATION (legacy)
        // ============================================================

        public int CalculateRequiredPortions(ScheduleMenuInputViewModel input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            decimal total = input.StaffMeals;

            if (input.BoardingHouses != null)
            {
                foreach (BoardingHouseScheduleOption house in input.BoardingHouses)
                {
                    if (house == null) continue;

                    if (house.StudentCount < 0)
                    {
                        throw new InvalidOperationException(
                            "Student count cannot be negative.");
                    }

                    if (house.IsInSeason && !string.IsNullOrWhiteSpace(house.ActiveSport))
                    {
                        total += house.StudentCount * SportsMultiplier;
                    }
                    else
                    {
                        total += house.StudentCount;
                    }
                }
            }

            return Math.Max(1, (int)Math.Ceiling(total));
        }

        // ============================================================
        // UC15: BUILD KITCHEN PLAN
        //
        // Built from students' SUBMITTED meal plans for the menu's week
        // — not from the menu's options or its portion estimates:
        //   - portions per dish = students who chose it for that meal;
        //     the menu's staff meals are added to the meal's most
        //     chosen dish
        //   - dishes nobody chose are listed as not needed
        //   - ingredients = recipe quantity per portion × portions
        //     (Meal → Recipe → RecipeIngredient → Ingredient, in the
        //     ingredient's own unit), totalled per meal, day and week
        //   - availability = current Ingredient stock (farm + external),
        //     shown as Available / Insufficient / Not available, with
        //     warnings naming the affected meals
        //   - preparation info from the Recipe; start and ready times
        //     are worked back from each meal's serve time
        //
        // Availability is a check against current stock — nothing is
        // reserved. Stock goes down only when the Chef issues a day's
        // ingredients (KitchenIssueService); issued days are shown as
        // issued and left out of the remaining-week check.
        // ============================================================

        public ProductionPlanViewModel BuildProductionPlan(int mealMenuId)
        {
            var menu = _db.MealMenus
                .Include(m => m.ScheduleItems)
                .FirstOrDefault(m => m.Id == mealMenuId);

            if (menu == null)
            {
                throw new InvalidOperationException("Menu not found.");
            }

            var weekStart = menu.StartDate.Date;
            var weekEnd = menu.EndDate.Date;

            // ── Submitted plans for this menu's week ─────────────────
            // (meal plans are keyed by the published menu's start date)
            var plans = _db.MealPlans
                .Include(p => p.Items)
                .Where(p => p.WeekStartDate == weekStart
                            && (p.Status == MealPlanStatus.Submitted
                                || p.Status == MealPlanStatus.SubmittedToDietitian
                                || p.Status == MealPlanStatus.Approved))
                .ToList();

            int unsubmittedWithPicks = _db.MealPlans.Count(p =>
                p.WeekStartDate == weekStart
                && (p.Status == MealPlanStatus.Draft || p.Status == MealPlanStatus.SentBack)
                && p.Items.Any());

            // Selections per (date, slot, meal)
            var selectionCounts = plans
                .SelectMany(p => p.Items)
                .Where(i => i.Date >= weekStart && i.Date <= weekEnd)
                .GroupBy(i => SelectionKey(i.Date, i.MealSlot, i.MenuItemId))
                .ToDictionary(g => g.Key, g => g.Count());

            var mealIds = plans
                .SelectMany(p => p.Items)
                .Select(i => i.MenuItemId)
                .Concat(menu.ScheduleItems.Select(s => s.MenuItemId))
                .Distinct()
                .ToList();

            var meals = _db.MenuItems
                .Include(m => m.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .Where(m => mealIds.Contains(m.Id))
                .ToDictionary(m => m.Id);

            // Current stock. An inactive ingredient counts as not available.
            var ingredientStock = _db.Ingredients
                .ToList()
                .ToDictionary(
                    i => i.Id,
                    i => i.IsActive ? i.FarmAvailableQuantity + i.ExternalAvailableQuantity : 0m);

            var vm = new ProductionPlanViewModel
            {
                MenuId = menu.Id,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                IsProductionConfirmed = menu.IsProductionConfirmed,
                ConfirmedAt = menu.ProductionConfirmedAt,
                IsMenuPublished = menu.MenuStatus == MenuStatus.Accepted,
                SubmittedPlanCount = plans.Count,
                StudentCount = new StudentHouseService(_db).BoardingStudentIds().Count,
                UnsubmittedPlansWithPicks = unsubmittedWithPicks,
                StaffMeals = menu.StaffMeals
            };

            var now = SchoolClock.Now;
            var weekTotals = new Dictionary<int, IngredientLineViewModel>();
            var flaggedIngredients = new HashSet<int>();

            // Days whose ingredients have already been issued from stock
            var issues = _db.KitchenIngredientIssues
                .Where(i => i.MealMenuId == menu.Id && i.Date.HasValue)
                .ToList()
                .ToDictionary(i => i.Date.Value.Date);
            var issuerIds = issues.Values.Select(i => i.IssuedByUserId).Distinct().ToList();
            var issuers = _db.Users.Where(u => issuerIds.Contains(u.UserId)).ToDictionary(u => u.UserId, u => u.Name);

            for (var date = weekStart; date <= weekEnd; date = date.AddDays(1))
            {
                var dayVm = new ProductionDayViewModel
                {
                    Date = date,
                    SelectionCutoff = MealPlanService.SelectionCutoff(date),
                    SelectionsFinal = now >= MealPlanService.SelectionCutoff(date)
                };

                KitchenIngredientIssue dayIssue;
                if (issues.TryGetValue(date, out dayIssue))
                {
                    dayVm.IsIssued = true;
                    dayVm.IssuedAt = dayIssue.IssuedAt;
                    dayVm.IssuedBy = issuers.ContainsKey(dayIssue.IssuedByUserId) ? issuers[dayIssue.IssuedByUserId] : null;
                }

                var dayTotals = new Dictionary<int, IngredientLineViewModel>();

                foreach (var slot in GeneratedMealSlots)
                {
                    var serveTime = MealTimes.ServeTime(slot);
                    var slotLabel = date.ToString("ddd dd MMM") + " " + slot;

                    var slotVm = new ProductionSlotViewModel
                    {
                        MealSlot = slot.ToString(),
                        ServeTime = serveTime,
                        StudentsWithoutPick = plans.Count(p =>
                            !p.Items.Any(i => i.Date.Date == date && i.MealSlot == slot))
                    };

                    var slotTotals = new Dictionary<int, IngredientLineViewModel>();

                    // The menu's options in menu order, plus anything chosen
                    // that isn't (or is no longer) on it
                    var menuOptionIds = menu.ScheduleItems
                        .Where(s => s.Date.Date == date && s.MealSlot == slot)
                        .OrderBy(s => s.Id)
                        .Select(s => s.MenuItemId);

                    var chosenIds = selectionCounts.Keys
                        .Where(k => k.StartsWith(date.ToString("yyyy-MM-dd") + "|" + slot + "|"))
                        .Select(k => int.Parse(k.Substring(k.LastIndexOf('|') + 1)));

                    var slotMealIds = menuOptionIds.Concat(chosenIds).Distinct().ToList();

                    Func<int, int> studentsFor = id =>
                    {
                        int count;
                        selectionCounts.TryGetValue(SelectionKey(date, slot, id), out count);
                        return count;
                    };

                    // Staff eat the most chosen dish of this meal (ties: the
                    // one listed first on the menu). No choices → unassigned.
                    int? staffMealId = slotMealIds
                        .Where(id => studentsFor(id) > 0)
                        .OrderByDescending(studentsFor)
                        .Select(id => (int?)id)
                        .FirstOrDefault();

                    if (menu.StaffMeals > 0 && !staffMealId.HasValue && slotMealIds.Any())
                    {
                        vm.Warnings.Add(new KitchenWarning
                        {
                            When = slotLabel,
                            Severity = KitchenWarningSeverity.Info,
                            Message = string.Format(
                                "{0} staff meal(s) not assigned — no student has chosen a dish for this meal yet.",
                                menu.StaffMeals)
                        });
                    }

                    foreach (var mealId in slotMealIds)
                    {
                        MenuItem meal;
                        meals.TryGetValue(mealId, out meal);
                        var mealName = meal != null ? meal.Name : "Unknown meal #" + mealId;

                        int studentPortions = studentsFor(mealId);
                        int staffPortions = staffMealId == mealId ? menu.StaffMeals : 0;
                        int portions = studentPortions + staffPortions;

                        if (studentPortions == 0)
                        {
                            slotVm.NotSelectedOptions.Add(mealName);
                            continue;
                        }

                        var recipe = meal != null ? meal.Recipe : null;
                        int prepMin = recipe != null ? recipe.PrepTimeMinutes : 0;
                        int cookMin = recipe != null ? recipe.CookTimeMinutes : 0;
                        string station = recipe != null && !string.IsNullOrWhiteSpace(recipe.Station)
                            ? recipe.Station
                            : "Line";

                        var task = new ProductionTaskViewModel
                        {
                            MenuItemId = mealId,
                            DishName = mealName,
                            Station = station,
                            Portions = portions,
                            StudentPortions = studentPortions,
                            StaffPortions = staffPortions,
                            PrepMinutes = prepMin,
                            CookMinutes = cookMin,
                            StartTime = serveTime.Subtract(TimeSpan.FromMinutes(prepMin + cookMin)),
                            ReadyTime = serveTime.Subtract(TimeSpan.FromMinutes(5)),
                            Instructions = recipe != null ? recipe.PreparationNotes : null,
                            HasRecipe = recipe != null && recipe.RecipeIngredients.Any(ri => ri.Ingredient != null)
                        };

                        if (!task.HasRecipe)
                        {
                            vm.Warnings.Add(new KitchenWarning
                            {
                                When = slotLabel,
                                Severity = KitchenWarningSeverity.Info,
                                Message = string.Format(
                                    "{0} has no recipe ingredients on file, so its ingredients can't be calculated or checked.",
                                    mealName),
                                AffectedMeals = { mealName }
                            });
                        }
                        else
                        {
                            foreach (var ri in recipe.RecipeIngredients.Where(x => x.Ingredient != null))
                            {
                                decimal required = ri.QuantityPerStandardPortion * portions;
                                decimal available;
                                ingredientStock.TryGetValue(ri.IngredientId, out available);

                                // Already taken out of stock for this day
                                if (dayVm.IsIssued) available = required;

                                var line = new IngredientLineViewModel
                                {
                                    IngredientId = ri.IngredientId,
                                    IngredientName = ri.Ingredient.Name,
                                    Unit = ri.Ingredient.Unit,
                                    QuantityPerPortion = ri.QuantityPerStandardPortion,
                                    TotalRequired = required,
                                    StockAvailable = available,
                                    Shortfall = required > available ? required - available : 0m,
                                    AffectedMeals = { mealName }
                                };

                                task.Ingredients.Add(line);
                                AddOrSum(slotTotals, line, mealName);
                                AddOrSum(dayTotals, line, slot + ": " + mealName);
                                if (!dayVm.IsIssued) AddOrSum(weekTotals, line, slotLabel + ": " + mealName);
                            }
                        }

                        slotVm.Tasks.Add(task);
                    }

                    // Earliest start first — the order the kitchen works in
                    slotVm.Tasks = slotVm.Tasks.OrderBy(t => t.StartTime).ThenBy(t => t.DishName).ToList();
                    slotVm.Portions = slotVm.Tasks.Sum(t => t.Portions);
                    slotVm.Ingredients = slotTotals.Values.OrderBy(x => x.IngredientName).ToList();

                    // ── Warnings for this meal ──
                    foreach (var line in slotVm.Ingredients.Where(l => l.Status != IngredientAvailability.Available))
                    {
                        flaggedIngredients.Add(line.IngredientId);
                        vm.Warnings.Add(IngredientWarning(slotLabel, line));
                    }

                    dayVm.Slots.Add(slotVm);
                }

                dayVm.DayTotalIngredients = dayTotals.Values
                    .OrderBy(x => x.IngredientName)
                    .ToList();

                vm.Days.Add(dayVm);
            }

            vm.WeekTotalIngredients = weekTotals.Values
                .OrderBy(x => x.IngredientName)
                .ToList();

            // ── Whole week: enough for each meal on its own, but not for
            //    all of them together ──
            foreach (var line in vm.WeekTotalIngredients.Where(l =>
                         l.Status != IngredientAvailability.Available && !flaggedIngredients.Contains(l.IngredientId)))
            {
                var warning = IngredientWarning("Whole week", line);
                warning.Message += " There is enough for each meal on its own, but not for all of this week's meals together.";
                vm.Warnings.Add(warning);
            }

            vm.TotalMeals = vm.Days.Sum(d => d.Slots.Sum(s => s.Portions));

            return vm;
        }

        private static string SelectionKey(DateTime date, MealSlot slot, int menuItemId)
        {
            return date.ToString("yyyy-MM-dd") + "|" + slot + "|" + menuItemId;
        }

        private static KitchenWarning IngredientWarning(string when, IngredientLineViewModel line)
        {
            var required = KitchenQuantity.Format(line.TotalRequired, line.Unit);
            var available = KitchenQuantity.Format(line.StockAvailable, line.Unit);

            return new KitchenWarning
            {
                When = when,
                Severity = line.Status == IngredientAvailability.NotAvailable
                    ? KitchenWarningSeverity.NotAvailable
                    : KitchenWarningSeverity.Insufficient,
                Message = line.Status == IngredientAvailability.NotAvailable
                    ? string.Format("{0} is not currently available. {1} required.", line.IngredientName, required)
                    : string.Format("Insufficient {0}. {1} required, {2} available.", line.IngredientName, required, available),
                AffectedMeals = line.AffectedMeals.ToList()
            };
        }

        // Adds a line into a totals bucket, keeping the list of meals
        // that need the ingredient.
        private void AddOrSum(
            Dictionary<int, IngredientLineViewModel> bucket,
            IngredientLineViewModel line,
            string mealLabel)
        {
            IngredientLineViewModel existing;

            if (!bucket.TryGetValue(line.IngredientId, out existing))
            {
                existing = new IngredientLineViewModel
                {
                    IngredientId = line.IngredientId,
                    IngredientName = line.IngredientName,
                    Unit = line.Unit,
                    QuantityPerPortion = line.QuantityPerPortion,
                    StockAvailable = line.StockAvailable
                };
                bucket[line.IngredientId] = existing;
            }

            existing.TotalRequired += line.TotalRequired;
            existing.Shortfall = existing.TotalRequired > existing.StockAvailable
                ? existing.TotalRequired - existing.StockAvailable
                : 0m;

            if (!existing.AffectedMeals.Contains(mealLabel))
            {
                existing.AffectedMeals.Add(mealLabel);
            }
        }

        // ============================================================
        // MEAL SLOT COMPATIBILITY
        // ============================================================

        private bool IsCompatibleWithMealSlot(MenuItem item, MealSlot slot)
        {
            if (item == null) return false;

            switch (slot)
            {
                case MealSlot.Breakfast: return item.IsBreakfastItem;
                case MealSlot.Lunch: return item.IsLunchItem;
                case MealSlot.Dinner: return item.IsDinnerItem;
                default: return false;
            }
        }

        private bool IsCooldownMealSlot(MealSlot slot)
        {
            return slot == MealSlot.Lunch || slot == MealSlot.Dinner;
        }

        private bool WasSelectionRejected(
            List<RejectedSelection> rejectedSelections,
            int menuItemId,
            DateTime date,
            MealSlot slot)
        {
            if (rejectedSelections == null || rejectedSelections.Count == 0) return false;

            return rejectedSelections.Any(x =>
                x.MenuItemId == menuItemId &&
                x.Date.Date == date.Date &&
                x.MealSlot == slot);
        }

        // ============================================================
        // USAGE / VARIETY
        // ============================================================

        private void RecordItemUsage(
            Dictionary<int, List<MealUsage>> itemUsage,
            int menuItemId,
            DateTime date,
            MealSlot mealSlot)
        {
            List<MealUsage> usages;
            if (!itemUsage.TryGetValue(menuItemId, out usages))
            {
                usages = new List<MealUsage>();
                itemUsage.Add(menuItemId, usages);
            }
            usages.Add(new MealUsage { Date = date.Date, MealSlot = mealSlot });
        }

        private bool IsWithinCooldown(
            Dictionary<int, List<MealUsage>> itemUsage,
            int menuItemId,
            DateTime date,
            MealSlot slot)
        {
            if (!IsCooldownMealSlot(slot)) return false;

            List<MealUsage> usages;
            if (!itemUsage.TryGetValue(menuItemId, out usages)) return false;

            DateTime cooldownStart = date.Date.AddDays(-MealCooldownDays);

            return usages.Any(x =>
                x.Date.Date >= cooldownStart &&
                x.Date.Date < date.Date &&
                IsCooldownMealSlot(x.MealSlot));
        }

        // ============================================================
        // INGREDIENT STOCK
        // ============================================================

        private List<IngredientShortage> GetIngredientShortages(
            MenuItem item,
            int portions,
            Dictionary<int, IngredientState> ingredientStock)
        {
            var shortages = new List<IngredientShortage>();

            if (item == null || item.Recipe == null || item.Recipe.RecipeIngredients == null)
            {
                return shortages;
            }

            foreach (var ri in item.Recipe.RecipeIngredients)
            {
                if (ri.Ingredient == null) continue;

                decimal required = ri.QuantityPerStandardPortion * portions;

                IngredientState state;
                if (!ingredientStock.TryGetValue(ri.IngredientId, out state))
                {
                    shortages.Add(new IngredientShortage
                    {
                        IngredientId = ri.IngredientId,
                        Name = ri.Ingredient.Name,
                        Unit = ri.Ingredient.Unit,
                        Required = required,
                        Available = 0m
                    });
                    continue;
                }

                decimal available = state.FarmAvailable + state.ExternalAvailable;

                if (required > available)
                {
                    shortages.Add(new IngredientShortage
                    {
                        IngredientId = ri.IngredientId,
                        Name = ri.Ingredient.Name,
                        Unit = ri.Ingredient.Unit,
                        Required = required,
                        Available = available
                    });
                }
            }

            return shortages;
        }

        private void DrawIngredientStock(
            MenuItem item,
            int portions,
            Dictionary<int, IngredientState> ingredientStock)
        {
            if (item == null || item.Recipe == null || item.Recipe.RecipeIngredients == null)
            {
                return;
            }

            foreach (var ri in item.Recipe.RecipeIngredients)
            {
                if (ri.Ingredient == null) continue;

                decimal required = ri.QuantityPerStandardPortion * portions;

                IngredientState state;
                if (!ingredientStock.TryGetValue(ri.IngredientId, out state)) continue;

                decimal fromFarm = Math.Min(state.FarmAvailable, required);
                state.FarmAvailable -= fromFarm;

                decimal remaining = required - fromFarm;
                if (remaining > 0)
                {
                    state.ExternalAvailable =
                        Math.Max(0m, state.ExternalAvailable - remaining);
                }
            }
        }

        // ============================================================
        // VALIDATION
        // ============================================================

        private void ValidateInput(ScheduleMenuInputViewModel input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.StartDate.Date > input.EndDate.Date)
                throw new ArgumentException("Start date cannot be after end date.");
            if (input.StaffMeals < 0)
                throw new ArgumentException("Staff meals cannot be negative.");
            if ((input.EndDate.Date - input.StartDate.Date).TotalDays > 31)
                throw new ArgumentException(
                    "A single generated menu may span a maximum of 31 days.");
        }

        // ============================================================
        // INTERNAL CLASSES
        // ============================================================

        private class IngredientShortage
        {
            public int IngredientId { get; set; }
            public string Name { get; set; }
            public string Unit { get; set; }
            public decimal Required { get; set; }
            public decimal Available { get; set; }
        }

        private class IngredientState
        {
            public Ingredient Ingredient { get; set; }
            public decimal FarmAvailable { get; set; }
            public decimal ExternalAvailable { get; set; }
        }

        private class MealUsage
        {
            public DateTime Date { get; set; }
            public MealSlot MealSlot { get; set; }
        }

        private class RejectedSelection
        {
            public int MenuItemId { get; set; }
            public DateTime Date { get; set; }
            public MealSlot MealSlot { get; set; }
        }
    }
}