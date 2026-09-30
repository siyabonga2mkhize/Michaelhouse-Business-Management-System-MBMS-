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
    // Rule-based recommender. No external AI:
    //   1. Loads the currently Accepted UC12 menu (latest published)
    //   2. For each (day, slot) finds the scheduled item (the chef's choice)
    //   3. Loads the wider MenuItem library as alternatives
    //   4. HARD FILTERS by student allergies (walks recipe → ingredients)
    //   5. RANKS remaining options using the student's active sport,
    //      sport archetype (UC12), and match-day context
    //   6. Returns top 3 options per slot
    //
    // UC12 integration:
    //   - Sports come from StudentSportStatus (structured, injury-aware)
    //     instead of the free-text StudentProfile.Sports field.
    //   - Archetype drives preference: Power→protein, Endurance→carb.
    //   - If the student is marked unavailable for a date, we do NOT
    //     push athletic nutrition on that date (they're recovering).
    // ============================================================

    public class MealPlanService
    {
        private readonly DBContextClass _db;

        // How many options to show the student per meal slot
        private const int OptionsPerSlot = 3;

        public MealPlanService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // ============================================================
        // STUDENT — GET OR CREATE DRAFT
        // ============================================================

        public MealPlanBuildViewModel GetOrCreateDraft(int studentId)
        {
            // 1. Load student + profile
            var student = _db.Students
                .Include(s => s.StudentProfile)
                .FirstOrDefault(s => s.StudentId == studentId);

            if (student == null)
            {
                throw new InvalidOperationException("Student not found.");
            }

            // 2. Find the most recently PUBLISHED menu.
            //
            //    BUG FIX: previously this ordered by StartDate. When two
            //    accepted menus overlap (e.g. 22–28 Sept and 23–29 Sept),
            //    StartDate ordering picks the one starting LAST, which is
            //    not necessarily the one the coordinator just published.
            //
            //    LastModifiedDate is set when the menu is Accepted — that's
            //    the correct "publication" timestamp.
            var acceptedMenu = _db.MealMenus
                .Where(m => m.MenuStatus == MenuStatus.Accepted)
                .OrderByDescending(m => m.LastModifiedDate)
                .ThenByDescending(m => m.CreatedDate)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();

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

            // 4. Build the view model
            return BuildViewModel(plan, student, acceptedMenu);
        }

        // ============================================================
        // STUDENT — SAVE PICKS
        // ============================================================

        public void SavePicks(int mealPlanId, Dictionary<string, int> picks)
        {
            var plan = _db.MealPlans
                .Include(p => p.Items)
                .FirstOrDefault(p => p.Id == mealPlanId);

            if (plan == null)
            {
                throw new InvalidOperationException("Meal plan not found.");
            }

            if (plan.Status != MealPlanStatus.Draft && plan.Status != MealPlanStatus.SentBack)
            {
                throw new InvalidOperationException(
                    "This plan has been submitted and can no longer be edited.");
            }

            foreach (var entry in picks)
            {
                // Key format: "yyyy-MM-dd|Breakfast"
                var parts = entry.Key.Split('|');
                if (parts.Length != 2) continue;

                DateTime date;
                if (!DateTime.TryParse(parts[0], out date)) continue;

                MealSlot slot;
                if (!Enum.TryParse(parts[1], out slot)) continue;

                var existing = plan.Items
                    .FirstOrDefault(i => i.Date.Date == date.Date && i.MealSlot == slot);

                if (existing == null)
                {
                    plan.Items.Add(new MealPlanItem
                    {
                        MealPlanId = plan.Id,
                        Date = date.Date,
                        MealSlot = slot,
                        MenuItemId = entry.Value,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.MenuItemId = entry.Value;
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
        // STUDENT — SUBMIT TO DIETITIAN
        // ============================================================

        public void Submit(int mealPlanId)
        {
            var plan = _db.MealPlans
                .Include(p => p.Items)
                .FirstOrDefault(p => p.Id == mealPlanId);

            if (plan == null)
            {
                throw new InvalidOperationException("Meal plan not found.");
            }

            var slotsRequired = (int)((plan.WeekEndDate - plan.WeekStartDate).TotalDays + 1) * 3;

            if (plan.Items.Count < slotsRequired)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Please choose a meal for every slot. You have {0} of {1} picks.",
                        plan.Items.Count, slotsRequired));
            }

            plan.Status = MealPlanStatus.SubmittedToDietitian;
            plan.SubmittedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
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
                IsEditable = plan.Status == MealPlanStatus.Draft || plan.Status == MealPlanStatus.SentBack,
                DietitianComment = plan.DietitianComment,
                SubmittedAt = plan.SubmittedAt,
                ReviewedAt = plan.ReviewedAt,
                SourceMenuId = acceptedMenu.Id,
                Allergies = profile != null ? profile.Allergies : "",
                MedicalConditions = profile != null ? profile.MedicalConditions : "",
                Sports = sportsDisplay
            };

            // Parse the student's allergens into a set (case-insensitive)
            var studentAllergens = ParseAllergens(vm.Allergies);

            // Load all candidate menu items with recipe + ingredients
            var candidates = _db.MenuItems
                .Where(m => m.IsActive)
                .Include(m => m.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .ToList();

            // Load this menu's schedule items (the chef's picks)
            var scheduleItems = _db.MenuScheduleItems
                .Where(s => s.MealMenuId == acceptedMenu.Id)
                .ToList();

            // For each date in the week
            for (var date = plan.WeekStartDate.Date;
                 date <= plan.WeekEndDate.Date;
                 date = date.AddDays(1))
            {
                var dayVm = new MealPlanDayViewModel { Date = date };

                // ── UC12: build sport context for this specific day ──
                var sportCtx = BuildSportContextForDate(sportStatuses, date);

                foreach (var slot in new[] { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Dinner })
                {
                    var slotVm = BuildSlot(
                        date,
                        slot,
                        scheduleItems,
                        candidates,
                        plan,
                        studentAllergens,
                        sportCtx);

                    dayVm.Slots.Add(slotVm);
                }

                vm.Days.Add(dayVm);
            }

            return vm;
        }

        // ============================================================
        // UC12: SPORT CONTEXT
        // ------------------------------------------------------------
        // Evaluates a student's sport statuses FOR A GIVEN DATE and
        // converts them into ranked nutrition preferences.
        //
        //   Power     → prefer HighProtein
        //   Endurance → prefer HighCarb
        //   Skill / Speed → no strong preference
        //
        // If every sport is marked unavailable on that date, the
        // student is treated as "recovering" — no athletic push.
        // ============================================================

        private class StudentSportContext
        {
            public bool HasActiveSport { get; set; }
            public bool PrefersProtein { get; set; }
            public bool PrefersCarb { get; set; }
            public bool IsRecovering { get; set; }
        }

        private StudentSportContext BuildSportContextForDate(
            List<StudentSportStatus> statuses,
            DateTime date)
        {
            var ctx = new StudentSportContext();

            if (statuses == null || statuses.Count == 0)
            {
                return ctx;
            }

            var active = statuses.Where(s => IsStatusActiveOn(s, date)).ToList();

            if (active.Count == 0)
            {
                ctx.IsRecovering = true;
                return ctx;
            }

            ctx.HasActiveSport = true;

            foreach (var s in active)
            {
                switch (s.Archetype)
                {
                    case SportArchetype.Power:
                        ctx.PrefersProtein = true;
                        break;

                    case SportArchetype.Endurance:
                        ctx.PrefersCarb = true;
                        break;

                        // Skill / Speed: no strong category preference.
                }
            }

            return ctx;
        }

        private bool IsStatusActiveOn(StudentSportStatus status, DateTime date)
        {
            // Auto-recover: return date has passed → treat as active.
            if (status.UnavailableUntil.HasValue &&
                status.UnavailableUntil.Value.Date <= date.Date)
            {
                return true;
            }

            return status.IsActive;
        }

        // ============================================================
        // BUILD ONE SLOT
        // ============================================================

        private MealPlanSlotViewModel BuildSlot(
            DateTime date,
            MealSlot slot,
            List<MenuScheduleItem> scheduleItems,
            List<MenuItem> candidates,
            MealPlan plan,
            HashSet<string> studentAllergens,
            StudentSportContext sportCtx)
        {
            var vm = new MealPlanSlotViewModel
            {
                MealSlot = slot
            };

            // 1. What's the UC12 default for this (date, slot)?
            var scheduled = scheduleItems
                .FirstOrDefault(s => s.Date.Date == date.Date && s.MealSlot == slot);

            // 2. Match-day context (from UC12's tagged reason).
            //    We only surface this if the student is not recovering —
            //    an injured athlete shouldn't see "match-day recovery".
            var reason = scheduled != null ? (scheduled.TagReason ?? "") : "";
            bool rawMatchDay = reason.Contains("Match-day");
            bool rawDayBefore = reason.Contains("Pre-match");

            bool studentActive = sportCtx != null
                                 && sportCtx.HasActiveSport
                                 && !sportCtx.IsRecovering;

            vm.IsMatchDay = rawMatchDay && studentActive;
            vm.IsDayBeforeMatch = rawDayBefore && studentActive;

            if (vm.IsMatchDay || vm.IsDayBeforeMatch)
            {
                var dotIdx = reason.IndexOf(". ");
                vm.MatchDescription = dotIdx > 0 ? reason.Substring(0, dotIdx) : reason;
            }

            if (scheduled != null && scheduled.MenuItem != null)
            {
                vm.DefaultItemName = scheduled.MenuItem.Name;
            }

            // 3. What did the student already pick?
            var pick = plan.Items
                .FirstOrDefault(i => i.Date.Date == date.Date && i.MealSlot == slot);

            if (pick != null)
            {
                vm.CurrentPickMenuItemId = pick.MenuItemId;
                vm.CurrentPickMealPlanItemId = pick.Id;
            }

            // 4. Build the ranked list of options
            var eligible = candidates
                .Where(c => IsEligibleForSlot(c, slot))
                .Where(c => IsSafeForStudent(c, studentAllergens))
                .ToList();

            var scored = eligible
                .Select(c => new
                {
                    Item = c,
                    Score = ScoreCandidate(c, vm.IsMatchDay, vm.IsDayBeforeMatch, sportCtx)
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.Name)
                .ToList();

            var options = new List<MenuItem>();

            if (scheduled != null
                && scheduled.MenuItem != null
                && IsSafeForStudent(scheduled.MenuItem, studentAllergens))
            {
                options.Add(scheduled.MenuItem);
            }

            if (pick != null)
            {
                var pickItem = candidates
                    .FirstOrDefault(c => c.Id == pick.MenuItemId);

                if (pickItem != null
                    && IsSafeForStudent(pickItem, studentAllergens)
                    && !options.Any(o => o.Id == pickItem.Id))
                {
                    options.Add(pickItem);
                }
            }

            foreach (var entry in scored)
            {
                if (options.Count >= OptionsPerSlot) break;
                if (options.Any(o => o.Id == entry.Item.Id)) continue;
                options.Add(entry.Item);
            }

            // 5. Map to option view models
            vm.Options = options.Select(o => new MealPlanOptionViewModel
            {
                MenuItemId = o.Id,
                Name = o.Name,
                DietaryClassification = o.DietaryClassification,
                CaloriesPerPortion = o.CaloriesPerPortion,
                ProteinGramsPerPortion = o.ProteinGramsPerPortion,
                NutritionCategory = o.NutritionCategory,
                IsDefault = scheduled != null && scheduled.MenuItemId == o.Id,
                Tags = BuildOptionTags(o, vm.IsMatchDay, vm.IsDayBeforeMatch, sportCtx)
            }).ToList();

            return vm;
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
        // HARD FILTER — ALLERGENS
        // ============================================================

        private bool IsSafeForStudent(MenuItem item, HashSet<string> studentAllergens)
        {
            if (studentAllergens.Count == 0)
            {
                return true;
            }

            var itemAllergens = GetAllergensForMenuItem(item);

            return !itemAllergens.Any(a => studentAllergens.Contains(a));
        }

        private HashSet<string> GetAllergensForMenuItem(MenuItem item)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (item == null || item.Recipe == null || item.Recipe.RecipeIngredients == null)
            {
                return result;
            }

            foreach (var ri in item.Recipe.RecipeIngredients)
            {
                if (ri.Ingredient == null) continue;
                if (string.IsNullOrWhiteSpace(ri.Ingredient.Allergens)) continue;

                foreach (var a in ri.Ingredient.Allergens.Split(','))
                {
                    var trimmed = a.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) result.Add(trimmed);
                }
            }

            return result;
        }

        private HashSet<string> ParseAllergens(string allergies)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(allergies))
            {
                return result;
            }

            foreach (var a in allergies.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = a.Trim();
                if (!string.IsNullOrEmpty(trimmed)) result.Add(trimmed);
            }

            return result;
        }

        // ============================================================
        // SOFT RANK — match-day + sport archetype
        // ============================================================

        private int ScoreCandidate(
            MenuItem item,
            bool isMatchDay,
            bool isDayBeforeMatch,
            StudentSportContext sportCtx)
        {
            int score = 0;

            // Match-day recovery: heavy protein tilt
            if (isMatchDay && item.NutritionCategory == NutritionCategory.HighProtein)
            {
                score += 10;
            }

            // Day before: carb loading tilt
            if (isDayBeforeMatch && item.NutritionCategory == NutritionCategory.HighCarb)
            {
                score += 10;
            }

            // UC12: archetype-driven baseline preference.
            // Applied for every active day, not just match days.
            if (sportCtx != null && sportCtx.HasActiveSport)
            {
                if (sportCtx.PrefersProtein &&
                    item.NutritionCategory == NutritionCategory.HighProtein)
                {
                    score += 3;
                }

                if (sportCtx.PrefersCarb &&
                    item.NutritionCategory == NutritionCategory.HighCarb)
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

        private List<string> BuildOptionTags(
            MenuItem item,
            bool isMatchDay,
            bool isDayBeforeMatch,
            StudentSportContext sportCtx)
        {
            var tags = new List<string>();

            if (isMatchDay && item.NutritionCategory == NutritionCategory.HighProtein)
            {
                tags.Add("Match-day recovery");
            }

            if (isDayBeforeMatch && item.NutritionCategory == NutritionCategory.HighCarb)
            {
                tags.Add("Pre-match loading");
            }

            if (item.NutritionCategory == NutritionCategory.HighProtein)
            {
                tags.Add("High protein");
            }
            else if (item.NutritionCategory == NutritionCategory.HighCarb)
            {
                tags.Add("High carb");
            }

            if (item.IsFarmGrownProduce)
            {
                tags.Add("Farm-grown");
            }

            return tags;
        }
    }
}