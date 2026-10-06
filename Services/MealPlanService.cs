using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC13 — Personalized Meal Plan Service
    //
    // The Cafeteria Manager publishes (accepts) a weekly menu with
    // several options per breakfast / lunch / dinner. The student's
    // meal plan is one choice per slot FROM THOSE OPTIONS:
    //
    //   1. Loads the most recently published menu
    //   2. For each (day, slot) takes the menu's options — nothing
    //      outside the published menu is ever offered
    //   3. Checks each against the student's dietary profile —
    //      allergies, preference, medical restrictions
    //      (DietaryProfileService). Every option is shown; ones that
    //      conflict are greyed out with the reason and can't be chosen
    //      (the server refuses them too).
    //   4. RANKS what's left for the student's own sport: their
    //      fixtures (match day, day before a match, training),
    //      priority sports and archetype (same rules as the
    //      scheduler, MenuSchedulingService.MatchDayCategory etc.)
    //   5. Enforces, on the server, every save and submit:
    //      own plan only, published option for that date + slot,
    //      suitable for the current profile, and the selection
    //      deadline — a meal can't be chosen or changed from 00:00
    //      the day before it is served (school time, SchoolClock).
    //
    // Submitting sends the plan straight to the kitchen (status
    // Submitted) — there is no Dietitian review of student plans; the
    // Dietitian has already approved the meals. The saved picks
    // (MealPlanItem → MenuScheduleItem) of submitted plans are the
    // demand the kitchen plan uses (MenuSchedulingService.BuildProductionPlan).
    // ============================================================

    public class MealPlanService
    {
        private readonly DBContextClass _db;
        private readonly DietaryProfileService _dietary;
        private readonly Func<DateTime> _now;

        private static readonly MealSlot[] Slots = { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Dinner };

        public MealPlanService(DBContextClass db)
            : this(db, null)
        {
        }

        // now: school time; defaults to SchoolClock.Now (tests can fix it)
        public MealPlanService(DBContextClass db, Func<DateTime> now)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _dietary = new DietaryProfileService(_db);
            _now = now ?? (() => SchoolClock.Now);
        }

        // ============================================================
        // SELECTION DEADLINE
        // A meal's selection closes at 00:00 on the day before it is
        // served, e.g. Wednesday's meals lock at the start of Tuesday.
        // ============================================================

        public static DateTime SelectionCutoff(DateTime mealDate)
        {
            return mealDate.Date.AddDays(-1);
        }

        private bool IsLocked(DateTime mealDate)
        {
            return _now() >= SelectionCutoff(mealDate);
        }

        // ============================================================
        // STUDENT — GET OR CREATE DRAFT
        // ============================================================

        public MealPlanBuildViewModel GetOrCreateDraft(int studentId)
        {
            return GetOrCreateDraft(studentId, null);
        }

        // weekStart: the start date of an accepted menu to open (from the
        // week tabs). Null, or a week that isn't published, opens the
        // default week — see MenuForStudent.
        public MealPlanBuildViewModel GetOrCreateDraft(int studentId, DateTime? weekStart)
        {
            // 1. Load student + profile
            var student = _db.Students
                .Include(s => s.StudentProfile)
                .FirstOrDefault(s => s.StudentId == studentId);

            if (student == null)
            {
                throw new InvalidOperationException("Student not found.");
            }

            // 2. Which published menu: the week asked for, else the one
            //    running today, else the next one (see MenuForStudent)
            var acceptedMenu = MenuForStudent(weekStart);

            if (acceptedMenu == null)
            {
                throw new InvalidOperationException(
                    "No published menu is available yet. Ask the Meal Coordinator to publish one.");
            }

            // 3. Find or create a Draft meal plan for this student for this week
            var plan = _db.MealPlans
                .Include(p => p.Items)
                .FirstOrDefault(p =>
                    p.StudentId == studentId &&
                    p.WeekStartDate == acceptedMenu.StartDate.Date);

            if (plan == null)
            {
                plan = new MealPlan
                {
                    StudentId = studentId,
                    WeekStartDate = acceptedMenu.StartDate.Date,
                    WeekEndDate = acceptedMenu.EndDate.Date,
                    Status = MealPlanStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };

                _db.MealPlans.Add(plan);
                _db.SaveChanges();
            }

            // 4. Build the view model, with the other weeks to switch to
            var vm = BuildViewModel(plan, student, acceptedMenu);
            vm.Weeks = AvailableWeeks(studentId, acceptedMenu);
            return vm;
        }

        // ============================================================
        // WHICH MENU / WEEK
        //
        // A menu can be accepted ahead of time, so "the most recently
        // published menu" is not necessarily this week's. Menus are
        // chosen by their dates instead:
        //   - the week asked for (an accepted menu starting that day);
        //   - else the accepted menu running today;
        //   - else the next one to start;
        //   - else the last one that ran.
        // Where two accepted menus start on the same day, the most
        // recently published wins (LastModifiedDate is set on Accept).
        // ============================================================

        private IQueryable<MealMenu> AcceptedMenus()
        {
            return _db.MealMenus.Where(m => m.MenuStatus == MenuStatus.Accepted);
        }

        private static IOrderedEnumerable<MealMenu> NewestFirst(IEnumerable<MealMenu> menus)
        {
            return menus
                .OrderByDescending(m => m.LastModifiedDate)
                .ThenByDescending(m => m.CreatedDate)
                .ThenByDescending(m => m.Id);
        }

        private MealMenu MenuForStudent(DateTime? weekStart)
        {
            var today = _now().Date;
            var menus = AcceptedMenus().ToList();

            MealMenu menu = null;

            if (weekStart.HasValue)
            {
                var start = weekStart.Value.Date;
                menu = NewestFirst(menus.Where(m => m.StartDate.Date == start)).FirstOrDefault();
            }

            if (menu == null)
            {
                menu = NewestFirst(menus.Where(m => m.StartDate.Date <= today && m.EndDate.Date >= today)).FirstOrDefault();
            }

            if (menu == null)
            {
                menu = menus
                    .Where(m => m.StartDate.Date > today)
                    .OrderBy(m => m.StartDate)
                    .ThenByDescending(m => m.LastModifiedDate)
                    .ThenByDescending(m => m.Id)
                    .FirstOrDefault();
            }

            if (menu == null)
            {
                menu = menus
                    .OrderByDescending(m => m.EndDate)
                    .ThenByDescending(m => m.LastModifiedDate)
                    .ThenByDescending(m => m.Id)
                    .FirstOrDefault();
            }

            return menu;
        }

        // The weeks the student can open: accepted menus that haven't
        // ended yet (one per start date), plus the week being shown.
        public List<MealPlanWeekOption> AvailableWeeks(int studentId, MealMenu current)
        {
            var today = _now().Date;

            var menus = AcceptedMenus().Where(m => m.EndDate >= today).ToList();
            if (current != null && !menus.Any(m => m.Id == current.Id)) menus.Add(current);

            var weeks = menus
                .GroupBy(m => m.StartDate.Date)
                .Select(g => NewestFirst(g).First())
                .OrderBy(m => m.StartDate)
                .ToList();

            var starts = weeks.Select(m => m.StartDate.Date).ToList();
            var statuses = _db.MealPlans
                .Where(p => p.StudentId == studentId && starts.Contains(p.WeekStartDate))
                .Select(p => new { p.WeekStartDate, p.Status })
                .ToList();

            return weeks.Select(m => new MealPlanWeekOption
            {
                WeekStartDate = m.StartDate.Date,
                WeekEndDate = m.EndDate.Date,
                IsCurrentWeek = m.StartDate.Date <= today && m.EndDate.Date >= today,
                IsSelected = current != null && m.StartDate.Date == current.StartDate.Date,
                Status = statuses.Where(s => s.WeekStartDate == m.StartDate.Date).Select(s => (MealPlanStatus?)s.Status).FirstOrDefault()
            }).ToList();
        }

        // ============================================================
        // STUDENT — SAVE PICKS
        //
        // picks: "yyyy-MM-dd|Lunch" → MenuItemId, as posted by the page.
        // Nothing posted is trusted — every pick is checked here, and
        // nothing is saved unless every pick is valid.
        //
        // Allowed in any status while the meal's deadline hasn't passed.
        // A Submitted / Approved plan keeps its status: each changed pick
        // is re-validated against the student's current profile.
        // ============================================================

        public void SavePicks(int mealPlanId, int studentId, Dictionary<string, int> picks)
        {
            var plan = LoadOwnPlan(mealPlanId, studentId);

            var profile = _dietary.GetProfileForStudent(plan.StudentId);
            var candidates = LoadCandidates().ToDictionary(c => c.Id);
            var menuOptions = LoadMenuOptions(plan);
            var validPicks = new List<Tuple<DateTime, MealSlot, int, int>>();

            foreach (var entry in picks ?? new Dictionary<string, int>())
            {
                // Key format: "yyyy-MM-dd|Breakfast"
                var parts = entry.Key.Split('|');
                if (parts.Length != 2) continue;

                DateTime date;
                if (!DateTime.TryParse(parts[0], out date)) continue;

                MealSlot slot;
                if (!Enum.TryParse(parts[1], out slot) || !Slots.Contains(slot)) continue;

                if (date.Date < plan.WeekStartDate.Date || date.Date > plan.WeekEndDate.Date) continue;

                var existing = plan.Items
                    .FirstOrDefault(i => i.Date.Date == date.Date && i.MealSlot == slot);

                // Deadline: an unchanged pick is fine; anything else is refused
                if (IsLocked(date))
                {
                    if (existing != null && existing.MenuItemId == entry.Value) continue;

                    throw new InvalidOperationException(string.Format(
                        "{0} on {1:ddd dd MMM} can no longer be chosen or changed — selection closed at the start of {2:ddd dd MMM}.",
                        slot, date, SelectionCutoff(date)));
                }

                // Must be one of this week's published options for that slot
                var option = menuOptions.FirstOrDefault(s =>
                    s.Date.Date == date.Date && s.MealSlot == slot && s.MenuItemId == entry.Value);

                MenuItem item;
                if (option == null
                    || !candidates.TryGetValue(entry.Value, out item)
                    || !IsEligibleForSlot(item, slot))
                {
                    throw new InvalidOperationException(string.Format(
                        "The meal chosen for {0} on {1:ddd dd MMM} is not one of the menu options for that meal.",
                        slot, date));
                }

                // Must suit the student's CURRENT dietary profile
                var conflicts = _dietary.GetConflicts(profile, item);
                if (conflicts.Count > 0)
                {
                    throw new InvalidOperationException(string.Format(
                        "{0} can't be chosen for {1} on {2:ddd dd MMM}: {3}.",
                        item.Name, slot, date, conflicts[0].Message));
                }

                validPicks.Add(Tuple.Create(date.Date, slot, item.Id, option.Id));
            }

            foreach (var pick in validPicks)
            {
                var date = pick.Item1;
                var slot = pick.Item2;

                var existing = plan.Items
                    .FirstOrDefault(i => i.Date.Date == date.Date && i.MealSlot == slot);

                if (existing == null)
                {
                    plan.Items.Add(new MealPlanItem
                    {
                        MealPlanId = plan.Id,
                        Date = date.Date,
                        MealSlot = slot,
                        MenuItemId = pick.Item3,
                        MenuScheduleItemId = pick.Item4,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.MenuItemId = pick.Item3;
                    existing.MenuScheduleItemId = pick.Item4;
                }
            }

            plan.UpdatedAt = DateTime.UtcNow;
            if (plan.Status == MealPlanStatus.SentBack)
            {
                plan.Status = MealPlanStatus.Draft;
            }

            _db.SaveChanges();
        }

        // ============================================================
        // STUDENT — SUBMIT (straight to the kitchen)
        // ============================================================

        public void Submit(int mealPlanId, int studentId)
        {
            var plan = LoadOwnPlan(mealPlanId, studentId);

            if (plan.Status != MealPlanStatus.Draft && plan.Status != MealPlanStatus.SentBack)
            {
                throw new InvalidOperationException("This meal plan has already been submitted.");
            }

            var profile = _dietary.GetProfileForStudent(plan.StudentId);
            var candidatesById = LoadCandidates().ToDictionary(c => c.Id);
            var menuOptions = LoadMenuOptions(plan);

            // ── Re-check picks that can still change ──
            // The profile or menu may have changed since they were saved.
            // Locked picks can't be changed any more, so they aren't
            // blocked here (the collection terminal still warns staff).
            foreach (var item in plan.Items.OrderBy(i => i.Date).ThenBy(i => i.MealSlot))
            {
                if (IsLocked(item.Date)) continue;

                MenuItem menuItem;
                bool onMenu = menuOptions.Any(s =>
                    s.Date.Date == item.Date.Date && s.MealSlot == item.MealSlot && s.MenuItemId == item.MenuItemId);

                if (!onMenu || !candidatesById.TryGetValue(item.MenuItemId, out menuItem))
                {
                    throw new InvalidOperationException(string.Format(
                        "Your pick for {0} on {1:ddd dd MMM} is no longer one of the menu options. Please choose again.",
                        item.MealSlot, item.Date));
                }

                var conflicts = _dietary.GetConflicts(profile, menuItem);
                if (conflicts.Count > 0)
                {
                    throw new InvalidOperationException(string.Format(
                        "Your pick for {0} on {1:ddd dd MMM} ({2}) no longer fits your dietary profile: {3}. Please choose another meal.",
                        item.MealSlot, item.Date, menuItem.Name, conflicts[0].Message));
                }
            }

            // ── Completeness ──
            // Every slot that is still open and has a suitable option
            // must be chosen. Slots past their deadline, or where nothing
            // on the menu suits the student, don't block submission.
            var missing = new List<string>();
            bool anyOpen = false;

            for (var date = plan.WeekStartDate.Date; date <= plan.WeekEndDate.Date; date = date.AddDays(1))
            {
                if (IsLocked(date)) continue;

                foreach (var slot in Slots)
                {
                    anyOpen = true;

                    bool hasSuitableOption = SlotMenuItems(menuOptions, candidatesById, date, slot)
                        .Any(c => _dietary.IsSafe(profile, c));

                    bool picked = plan.Items.Any(i => i.Date.Date == date && i.MealSlot == slot);

                    if (hasSuitableOption && !picked)
                    {
                        missing.Add(date.ToString("ddd dd MMM") + " " + slot);
                    }
                }
            }

            if (!anyOpen && plan.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    "The selection deadline has passed for every meal in this plan.");
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(string.Format(
                    "Please choose a meal for every open slot. Still to choose: {0}{1}.",
                    string.Join(", ", missing.Take(5)),
                    missing.Count > 5 ? string.Format(" and {0} more", missing.Count - 5) : ""));
            }

            plan.Status = MealPlanStatus.Submitted;
            plan.SubmittedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
        }

        // Students may only work on their own plan
        private MealPlan LoadOwnPlan(int mealPlanId, int studentId)
        {
            var plan = _db.MealPlans
                .Include(p => p.Items)
                .FirstOrDefault(p => p.Id == mealPlanId);

            if (plan == null || plan.StudentId != studentId)
            {
                throw new InvalidOperationException("Meal plan not found.");
            }

            return plan;
        }

        // ============================================================
        // SUBMITTED PLANS — the selections the kitchen and the
        // collection terminal work from. Plans submitted before the
        // Dietitian review step was removed (SubmittedToDietitian /
        // Approved) count too.
        // ============================================================

        public static bool IsSubmitted(MealPlanStatus status)
        {
            return status == MealPlanStatus.Submitted
                   || status == MealPlanStatus.SubmittedToDietitian
                   || status == MealPlanStatus.Approved;
        }

        // The student's submitted plan whose week includes this date
        public MealPlan FindSubmittedPlanFor(int studentId, DateTime date)
        {
            var day = date.Date;

            return _db.MealPlans
                .Where(p => p.StudentId == studentId
                            && p.WeekStartDate <= day
                            && p.WeekEndDate >= day
                            && (p.Status == MealPlanStatus.Submitted
                                || p.Status == MealPlanStatus.SubmittedToDietitian
                                || p.Status == MealPlanStatus.Approved))
                .OrderByDescending(p => p.SubmittedAt)
                .ThenByDescending(p => p.Id)
                .FirstOrDefault();
        }

        // ============================================================
        // DIETITIAN — APPROVE
        // ============================================================

        public void Approve(int mealPlanId, int dietitianUserId, string comment)
        {
            var plan = _db.MealPlans.FirstOrDefault(p => p.Id == mealPlanId);
            if (plan == null) throw new InvalidOperationException("Meal plan not found.");

            plan.Status = MealPlanStatus.Approved;
            plan.DietitianUserId = dietitianUserId;
            plan.DietitianComment = comment;
            plan.ReviewedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
        }

        // ============================================================
        // DIETITIAN — SEND BACK
        // ============================================================

        public void SendBack(int mealPlanId, int dietitianUserId, string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                throw new ArgumentException("Please explain why the plan is being sent back.");
            }

            var plan = _db.MealPlans.FirstOrDefault(p => p.Id == mealPlanId);
            if (plan == null) throw new InvalidOperationException("Meal plan not found.");

            plan.Status = MealPlanStatus.SentBack;
            plan.DietitianUserId = dietitianUserId;
            plan.DietitianComment = comment;
            plan.ReviewedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
        }

        // ============================================================
        // DIETITIAN — LOAD QUEUE
        // ============================================================

        public List<MealPlan> GetSubmittedPlans()
        {
            return _db.MealPlans
                .Include(p => p.Student)
                .Include(p => p.Items.Select(i => i.MenuItem))
                .Where(p => p.Status == MealPlanStatus.SubmittedToDietitian)
                .OrderBy(p => p.SubmittedAt)
                .ToList();
        }

        public List<MealPlan> GetRecentlyReviewedPlans()
        {
            return _db.MealPlans
                .Include(p => p.Student)
                .Where(p => p.Status == MealPlanStatus.Approved || p.Status == MealPlanStatus.SentBack)
                .OrderByDescending(p => p.ReviewedAt)
                .Take(10)
                .ToList();
        }

        public MealPlan GetPlan(int mealPlanId)
        {
            return _db.MealPlans
                .Include(p => p.Student)
                .Include(p => p.Items.Select(i => i.MenuItem))
                .FirstOrDefault(p => p.Id == mealPlanId);
        }

        // ============================================================
        // VIEW MODEL BUILDER
        // ============================================================

        private MealPlanBuildViewModel BuildViewModel(
            MealPlan plan,
            Student student,
            MealMenu acceptedMenu)
        {
            var profile = student.StudentProfile;

            // ── UC12: load structured sport records ───────────────────
            var sportStatuses = _db.StudentSportStatuses
                .Where(s => s.StudentId == student.StudentId)
                .ToList();

            // Prefer the structured table for display. Fall back to
            // the old free-text field if the student has no rows yet.
            string sportsDisplay;
            if (sportStatuses.Count > 0)
            {
                sportsDisplay = string.Join(", ",
                    sportStatuses.Select(s => s.Sport).Distinct());
            }
            else
            {
                sportsDisplay = profile != null ? profile.Sports : "";
            }

            var vm = new MealPlanBuildViewModel
            {
                MealPlanId = plan.Id,
                StudentName = student.FirstName + " " + student.LastName,
                WeekStartDate = plan.WeekStartDate,
                WeekEndDate = plan.WeekEndDate,
                Status = plan.Status,
                CanSubmit = plan.Status == MealPlanStatus.Draft || plan.Status == MealPlanStatus.SentBack,
                DietitianComment = plan.DietitianComment,
                SubmittedAt = plan.SubmittedAt,
                ReviewedAt = plan.ReviewedAt,
                SourceMenuId = acceptedMenu.Id,
                MedicalConditions = profile != null ? profile.MedicalConditions : "",
                Sports = sportsDisplay,
                Now = _now()
            };

            // The student's dietary profile drives the hard filter
            var dietaryProfile = DietaryProfileService.FromStudentProfile(profile);

            vm.Allergies = dietaryProfile.AllergySummary;
            vm.DietaryPreference = dietaryProfile.PreferenceSummary;
            vm.MedicalDietaryRestrictions = dietaryProfile.MedicalRestrictionSummary;

            // Active menu items with recipe + ingredients (for the
            // dietary check), keyed by id
            var candidatesById = LoadCandidates().ToDictionary(c => c.Id);

            // The published menu's options for the week
            var scheduleItems = _db.MenuScheduleItems
                .Where(s => s.MealMenuId == acceptedMenu.Id)
                .ToList();

            // The student's own sports needs for every day: their
            // sports, availability and the Coaches' fixtures
            var calendar = new StudentSportService(_db).LoadCalendar(
                plan.WeekStartDate.Date, plan.WeekEndDate.Date, new[] { student.StudentId });

            for (var date = plan.WeekStartDate.Date;
                 date <= plan.WeekEndDate.Date;
                 date = date.AddDays(1))
            {
                var dayVm = new MealPlanDayViewModel { Date = date };
                var sportCtx = StudentSportContext.From(calendar.NeedOn(student.StudentId, date));

                foreach (var slot in Slots)
                {
                    var slotVm = BuildSlot(
                        date,
                        slot,
                        scheduleItems,
                        candidatesById,
                        plan,
                        dietaryProfile,
                        sportCtx);

                    dayVm.Slots.Add(slotVm);

                    if (!slotVm.IsLocked && slotVm.Options.Any(o => o.IsAvailable))
                    {
                        vm.OpenSlotCount++;
                        if (slotVm.CurrentPickMenuItemId.HasValue) vm.OpenSlotsChosen++;
                    }
                }

                vm.Days.Add(dayVm);
            }

            vm.IsEditable = vm.Days.Any(d => d.Slots.Any(s => !s.IsLocked));

            return vm;
        }

        // ============================================================
        // SPORT CONTEXT — for THIS student, per day
        // ------------------------------------------------------------
        // From SportsCalendar.NeedOn (StudentSportService): the
        // student's sports, whether they're available, and the
        // Coaches' fixtures — the same rules the menu generator uses:
        //
        //   Match day          → the sport's category (Power →
        //                        HighProtein, Endurance → HighCarb,
        //                        Skill → Light, Speed → Hydration)
        //   Day before a match → HighCarb
        //   Training day       → Power → HighProtein, Endurance → HighCarb
        //   Priority sport week→ same as training
        //
        // Only days with an activity count: playing rugby doesn't make
        // every day a high-protein day. If every sport is marked
        // unavailable, the student is "recovering" — no sports push.
        // Sports only RANK safe options; they never hide one.
        // ============================================================

        private class StudentSportContext
        {
            // A match, the day before one, training or a priority week
            public bool HasActiveSport { get; set; }
            public bool PrefersProtein { get; set; }
            public bool PrefersCarb { get; set; }
            public bool IsRecovering { get; set; }

            public bool IsMatchDay { get; set; }
            public bool IsDayBeforeMatch { get; set; }
            public bool IsTrainingDay { get; set; }
            public bool IsPriorityWeek { get; set; }

            // What the day calls for, if anything
            public NutritionCategory? PreferredCategory { get; set; }

            // e.g. "Rugby vs Hilton"
            public string Description { get; set; }

            // e.g. "Recommended for your Rugby match"
            public string RecommendationLabel { get; set; }

            public static StudentSportContext From(SportNeed need)
            {
                bool active = need.Kind != SportNeedKind.None;

                return new StudentSportContext
                {
                    HasActiveSport = active,
                    IsRecovering = need.IsRecovering,
                    PrefersProtein = active && need.Archetype == SportArchetype.Power,
                    PrefersCarb = active && need.Archetype == SportArchetype.Endurance,
                    IsMatchDay = need.Kind == SportNeedKind.MatchDay,
                    IsDayBeforeMatch = need.Kind == SportNeedKind.DayBeforeMatch,
                    IsTrainingDay = need.Kind == SportNeedKind.Training,
                    IsPriorityWeek = need.Kind == SportNeedKind.PriorityWeek,
                    PreferredCategory = need.Category,
                    Description = need.Description,
                    RecommendationLabel = need.RecommendationLabel
                };
            }
        }

        // ============================================================
        // BUILD ONE SLOT
        //
        // Every published menu option for this slot is listed. Options
        // that suit the student come first, ranked for their sport;
        // options that conflict with their dietary profile follow,
        // marked unavailable with the reason. Nothing outside the menu
        // is ever listed.
        // ============================================================

        private MealPlanSlotViewModel BuildSlot(
            DateTime date,
            MealSlot slot,
            List<MenuScheduleItem> scheduleItems,
            Dictionary<int, MenuItem> candidatesById,
            MealPlan plan,
            StudentDietaryProfile dietaryProfile,
            StudentSportContext sportCtx)
        {
            var vm = new MealPlanSlotViewModel
            {
                MealSlot = slot,
                SelectionCutoff = SelectionCutoff(date),
                IsLocked = IsLocked(date),
                IsMatchDay = sportCtx.IsMatchDay,
                IsDayBeforeMatch = sportCtx.IsDayBeforeMatch,
                IsTrainingDay = sportCtx.IsTrainingDay,
                MatchDescription = sportCtx.Description
            };

            // 1. The menu's options for this (date, slot)
            var menuItems = SlotMenuItems(scheduleItems, candidatesById, date, slot);

            // Kept for the mobile API: the first menu option
            if (menuItems.Count > 0)
            {
                vm.DefaultItemName = menuItems[0].Name;
            }

            // 2. Options this student may choose
            var suitable = menuItems
                .Where(c => _dietary.IsSafe(dietaryProfile, c))
                .ToList();

            // 3. What did the student already pick?
            var pick = plan.Items
                .FirstOrDefault(i => i.Date.Date == date.Date && i.MealSlot == slot);

            if (pick != null)
            {
                vm.CurrentPickMealPlanItemId = pick.Id;
                vm.CurrentPickName = pick.MenuItem != null ? pick.MenuItem.Name : null;

                if (suitable.Any(c => c.Id == pick.MenuItemId))
                {
                    vm.CurrentPickMenuItemId = pick.MenuItemId;
                }
                else
                {
                    // No longer on the menu, or no longer fits the
                    // (updated) dietary profile.
                    vm.CurrentPickNoLongerSuitable = true;
                }
            }

            vm.HasNoSuitableOption = suitable.Count == 0;

            // 4. Sports: say so if nothing suitable meets the day's need
            if (sportCtx.PreferredCategory.HasValue
                && !sportCtx.IsRecovering
                && suitable.Count > 0
                && !suitable.Any(c => c.NutritionCategory == sportCtx.PreferredCategory.Value))
            {
                vm.SportsNote = string.Format(
                    "No {0} option on this menu suits your dietary profile for your {1} — choose whichever suits you best.",
                    CategoryWords(sportCtx.PreferredCategory.Value),
                    sportCtx.Description);
            }

            // 5. Suitable options first, ranked for the student's sport
            vm.Options = suitable
                .Select(o => new
                {
                    Item = o,
                    Score = ScoreCandidate(o, sportCtx)
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.Name)
                .Select(x => ToOption(x.Item, sportCtx, null))
                .ToList();

            // 6. Then the rest of the menu, shown but not selectable
            foreach (var item in menuItems.Where(c => !suitable.Contains(c)).OrderBy(c => c.Name))
            {
                var reason = string.Join("; ", _dietary.GetConflicts(dietaryProfile, item).Select(c => c.Message));
                vm.Options.Add(ToOption(item, sportCtx, reason));
            }

            return vm;
        }

        // unavailableReason: null when the student may choose it
        private MealPlanOptionViewModel ToOption(MenuItem item, StudentSportContext sportCtx, string unavailableReason)
        {
            bool available = unavailableReason == null;

            return new MealPlanOptionViewModel
            {
                MenuItemId = item.Id,
                Name = item.Name,
                IsAvailable = available,
                UnavailableReason = unavailableReason,
                DietaryClassification = item.DietaryClassification,
                CaloriesPerPortion = item.CaloriesPerPortion,
                ProteinGramsPerPortion = item.ProteinGramsPerPortion,
                CarbohydrateGramsPerPortion = item.CarbohydrateGramsPerPortion,
                FatGramsPerPortion = item.FatGramsPerPortion,
                NutritionCategory = item.NutritionCategory,
                IsDefault = false,

                // Sports tags only make sense on meals they can choose
                Tags = available ? BuildOptionTags(item, sportCtx) : new List<string>(),

                // "Recommended for your Rugby match" — safe options in
                // the category the day calls for
                RecommendationLabel = available && FitsToday(item, sportCtx) ? sportCtx.RecommendationLabel : null
            };
        }

        private static string CategoryWords(NutritionCategory category)
        {
            return MenuCoverageService.CategoryWords(category);
        }

        // ============================================================
        // MENU OPTIONS
        // ============================================================

        // The accepted menu a plan was built from: same week start,
        // most recently published (same ordering as GetOrCreateDraft).
        private List<MenuScheduleItem> LoadMenuOptions(MealPlan plan)
        {
            var menu = _db.MealMenus
                .Where(m => m.MenuStatus == MenuStatus.Accepted && m.StartDate == plan.WeekStartDate)
                .OrderByDescending(m => m.LastModifiedDate)
                .ThenByDescending(m => m.CreatedDate)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();

            if (menu == null)
            {
                throw new InvalidOperationException(
                    "The published menu for this week could not be found. Ask the Meal Coordinator.");
            }

            return _db.MenuScheduleItems
                .Where(s => s.MealMenuId == menu.Id)
                .ToList();
        }

        // Active, slot-eligible menu items offered in one slot
        private List<MenuItem> SlotMenuItems(
            IEnumerable<MenuScheduleItem> menuOptions,
            Dictionary<int, MenuItem> candidatesById,
            DateTime date,
            MealSlot slot)
        {
            var result = new List<MenuItem>();

            foreach (var option in menuOptions
                         .Where(s => s.Date.Date == date.Date && s.MealSlot == slot)
                         .OrderBy(s => s.Id))
            {
                MenuItem item;
                if (candidatesById.TryGetValue(option.MenuItemId, out item)
                    && IsEligibleForSlot(item, slot)
                    && !result.Any(r => r.Id == item.Id))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        // ============================================================
        // HARD FILTER — ELIGIBILITY
        // ============================================================

        private bool IsEligibleForSlot(MenuItem item, MealSlot slot)
        {
            switch (slot)
            {
                case MealSlot.Breakfast: return item.IsBreakfastItem;
                case MealSlot.Lunch: return item.IsLunchItem;
                case MealSlot.Dinner: return item.IsDinnerItem;
                default: return false;
            }
        }

        // ============================================================
        // CANDIDATES — active menu items with recipe + ingredients
        // (needed by the dietary conflict check)
        // ============================================================

        private List<MenuItem> LoadCandidates()
        {
            return _db.MenuItems
                .Where(m => m.IsActive)
                .Include(m => m.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .ToList();
        }

        // ============================================================
        // SOFT RANK — the day's sports need + sport archetype
        // ============================================================

        private int ScoreCandidate(MenuItem item, StudentSportContext sportCtx)
        {
            int score = 0;

            if (sportCtx != null && sportCtx.HasActiveSport && !sportCtx.IsRecovering)
            {
                // What today calls for (match, day before, training, priority)
                if (sportCtx.PreferredCategory.HasValue
                    && item.NutritionCategory == sportCtx.PreferredCategory.Value)
                {
                    score += 10;
                }

                // UC12: archetype preference — only on days with an
                // activity (HasActiveSport), never "every day"
                if (sportCtx.PrefersProtein && item.NutritionCategory == NutritionCategory.HighProtein)
                {
                    score += 3;
                }

                if (sportCtx.PrefersCarb && item.NutritionCategory == NutritionCategory.HighCarb)
                {
                    score += 3;
                }
            }

            if (item.IsFarmGrownProduce) score += 1;

            return score;
        }

        // ============================================================
        // TAGS — human-readable labels shown under each option
        // ============================================================

        private static bool FitsToday(MenuItem item, StudentSportContext sportCtx)
        {
            return sportCtx != null
                   && !sportCtx.IsRecovering
                   && sportCtx.PreferredCategory.HasValue
                   && item.NutritionCategory == sportCtx.PreferredCategory.Value;
        }

        private List<string> BuildOptionTags(MenuItem item, StudentSportContext sportCtx)
        {
            var tags = new List<string>();

            bool fitsToday = FitsToday(item, sportCtx);

            if (fitsToday)
            {
                if (sportCtx.IsMatchDay) tags.Add("Match-day recovery");
                else if (sportCtx.IsDayBeforeMatch) tags.Add("Pre-match loading");
                else if (sportCtx.IsTrainingDay) tags.Add("Training fuel");
                else if (sportCtx.IsPriorityWeek) tags.Add("Priority sport");
            }

            switch (item.NutritionCategory)
            {
                case NutritionCategory.HighProtein: tags.Add("High protein"); break;
                case NutritionCategory.HighCarb: tags.Add("High carb"); break;
                case NutritionCategory.Light: tags.Add("Light"); break;
                case NutritionCategory.Hydration: tags.Add("Hydration"); break;
            }

            if (item.IsFarmGrownProduce)
            {
                tags.Add("Farm-grown");
            }

            return tags;
        }
    }
}
