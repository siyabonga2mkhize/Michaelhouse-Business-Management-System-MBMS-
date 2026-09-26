using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Services
{
    public class MenuValidationResult
    {
        public bool IsReadyToPublish { get; set; }
        public List<MenuValidationIssue> CriticalErrors { get; set; } = new List<MenuValidationIssue>();
        public List<MenuValidationIssue> Warnings { get; set; } = new List<MenuValidationIssue>();
        public List<MenuValidationIssue> Information { get; set; } = new List<MenuValidationIssue>();

        public int CriticalCount { get { return CriticalErrors.Count; } }
        public int WarningCount { get { return Warnings.Count; } }
        public int InfoCount { get { return Information.Count; } }
    }

    public class MenuValidationService
    {
        private readonly DBContextClass _db;
        private readonly DietarySafetyService _safety;

        public MenuValidationService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _safety = new DietarySafetyService(db);
        }

        public MenuValidationService() : this(new DBContextClass()) { }

        // ─────────────────────────────────────────────────────────────
        // MAIN ENTRY POINT
        // ─────────────────────────────────────────────────────────────

        public MenuValidationResult ValidateWeeklyMenu(int weeklyMenuId)
        {
            var result = new MenuValidationResult();

            var menu = _db.WeeklyMenus
                .Include("Days.Meals.Components.MenuItem")
                .Include("Days.Meals.Alternatives.MenuItem")
                .FirstOrDefault(w => w.WeeklyMenuID == weeklyMenuId);

            if (menu == null)
            {
                result.CriticalErrors.Add(MakeIssue(ValidationSeverity.Critical,
                    ValidationCategory.Completeness,
                    "Weekly menu not found."));
                result.IsReadyToPublish = false;
                return result;
            }

            CheckCompleteness(menu, result);
            CheckDietarySafety(menu, result);
            CheckInventory(menu, result);
            CheckSportsRequirements(menu, result);
            CheckKitchenCapacity(menu, result);

            SaveIssues(weeklyMenuId, result);

            result.IsReadyToPublish = result.CriticalErrors.Count == 0;
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // CHECKER A — MENU COMPLETENESS (Section 41.A)
        // ─────────────────────────────────────────────────────────────

        private void CheckCompleteness(WeeklyMenu menu, MenuValidationResult result)
        {
            var requiredSlots = new[] { "Breakfast", "Lunch", "Dinner" };

            for (int i = 0; i < 7; i++)
            {
                var dayDate = menu.StartDate.Date.AddDays(i);
                var dayOfWeek = dayDate.DayOfWeek;

                var day = menu.Days.FirstOrDefault(d => d.Date.Date == dayDate);
                if (day == null)
                {
                    result.CriticalErrors.Add(MakeIssue(
                        ValidationSeverity.Critical,
                        ValidationCategory.Completeness,
                        dayOfWeek + " (" + dayDate.ToString("dd MMM") + ") has no menu entry."));
                    continue;
                }

                var slotsToCheck = dayOfWeek == DayOfWeek.Sunday
                    ? new[] { "Sunday Brunch", "Lunch", "Dinner" }
                    : requiredSlots;

                foreach (var slotName in slotsToCheck)
                {
                    var slot = _db.MealSlots.FirstOrDefault(m => m.Name == slotName);
                    if (slot == null) continue;

                    var meal = day.Meals.FirstOrDefault(m => m.MealSlotId == slot.Id);
                    if (meal == null)
                    {
                        result.CriticalErrors.Add(MakeIssue(
                            ValidationSeverity.Critical,
                            ValidationCategory.Completeness,
                            dayOfWeek + " " + slotName + " is missing."));
                        continue;
                    }

                    if (meal.Components == null || meal.Components.Count == 0)
                    {
                        result.CriticalErrors.Add(MakeIssue(
                            ValidationSeverity.Critical,
                            ValidationCategory.Completeness,
                            dayOfWeek + " " + slotName + " has no menu components."));
                    }

                    if ((meal.Alternatives == null || meal.Alternatives.Count == 0) &&
                        slot.Name != "Afternoon Tea" && slot.Name != "Morning Snack")
                    {
                        result.Warnings.Add(MakeIssue(
                            ValidationSeverity.Warning,
                            ValidationCategory.Completeness,
                            dayOfWeek + " " + slotName + " has no alternative option."));
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // CHECKER B — DIETARY SAFETY (Section 41.B)
        // ─────────────────────────────────────────────────────────────

        private void CheckDietarySafety(WeeklyMenu menu, MenuValidationResult result)
        {
            var allMenuItems = menu.Days
                .SelectMany(d => d.Meals)
                .SelectMany(m => m.Components)
                .Where(c => c.MenuItem != null)
                .Select(c => c.MenuItem)
                .GroupBy(mi => mi.Id)
                .Select(g => g.First())
                .ToList();

            foreach (var item in allMenuItems)
            {
                int blockedCount = _safety.CountStudentsBlockedFromMenuItem(item.Id);

                if (blockedCount > 0)
                {
                    bool hasAlternative = menu.Days
                        .SelectMany(d => d.Meals)
                        .Where(m => m.Components.Any(c => c.MenuItemId == item.Id))
                        .Any(m => m.Alternatives != null && m.Alternatives.Count > 0);

                    if (!hasAlternative)
                    {
                        result.CriticalErrors.Add(MakeIssue(
                            ValidationSeverity.Critical,
                            ValidationCategory.DietarySafety,
                            "Menu item '" + item.Name + "' conflicts with " + blockedCount +
                            " student(s), and has no safe alternative.",
                            "MenuItem", item.Id));
                    }
                    else
                    {
                        result.Warnings.Add(MakeIssue(
                            ValidationSeverity.Warning,
                            ValidationCategory.DietarySafety,
                            "Menu item '" + item.Name + "' conflicts with " + blockedCount +
                            " student(s) — alternative available.",
                            "MenuItem", item.Id));
                    }
                }

                if (item.HasIncompleteAllergenInfo)
                {
                    result.Warnings.Add(MakeIssue(
                        ValidationSeverity.Warning,
                        ValidationCategory.DietarySafety,
                        "Menu item '" + item.Name + "' has incomplete allergen information.",
                        "MenuItem", item.Id));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // CHECKER C — INVENTORY (Section 41.C)
        // ─────────────────────────────────────────────────────────────

        private void CheckInventory(WeeklyMenu menu, MenuValidationResult result)
        {
            var mealIds = menu.Days.SelectMany(d => d.Meals).Select(m => m.Id).ToList();
            if (mealIds.Count == 0) return;

            var componentItems = _db.MenuMealComponents
                .Include("MenuItem.Recipe.Ingredients.InventoryItem")
                .Where(c => mealIds.Contains(c.MenuMealId) && c.MenuItem != null)
                .ToList();

            var required = new Dictionary<int, decimal>();
            var names = new Dictionary<int, string>();
            var units = new Dictionary<int, string>();

            foreach (var c in componentItems)
            {
                var recipe = c.MenuItem.Recipe;
                if (recipe == null) continue;

                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.IsOptional) continue;
                    if (!required.ContainsKey(ing.InventoryItemId)) required[ing.InventoryItemId] = 0m;
                    required[ing.InventoryItemId] += ing.Quantity;

                    if (ing.InventoryItem != null)
                    {
                        names[ing.InventoryItemId] = ing.InventoryItem.Name;
                        units[ing.InventoryItemId] = ing.InventoryItem.Unit;
                    }
                }
            }

            foreach (var kv in required)
            {
                var inv = _db.InventoryItems.FirstOrDefault(i => i.Id == kv.Key);
                if (inv == null) continue;

                var requiredQty = kv.Value * 100;
                var availableQty = inv.CurrentStock;

                if (availableQty < requiredQty)
                {
                    var shortfall = requiredQty - availableQty;
                    result.CriticalErrors.Add(MakeIssue(
                        ValidationSeverity.Critical,
                        ValidationCategory.Inventory,
                        "Shortage: '" + (names.ContainsKey(kv.Key) ? names[kv.Key] : "item #" + kv.Key) +
                        "' required " + requiredQty.ToString("0.##") + " " + (units.ContainsKey(kv.Key) ? units[kv.Key] : "") +
                        ", available " + availableQty.ToString("0.##") + " (short by " + shortfall.ToString("0.##") + ").",
                        "InventoryItem", kv.Key));
                }
                else if (availableQty < requiredQty * 1.15m)
                {
                    result.Warnings.Add(MakeIssue(
                        ValidationSeverity.Warning,
                        ValidationCategory.Inventory,
                        "Low margin: '" + (names.ContainsKey(kv.Key) ? names[kv.Key] : "item #" + kv.Key) +
                        "' available " + availableQty.ToString("0.##") + ", required " + requiredQty.ToString("0.##") + ".",
                        "InventoryItem", kv.Key));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // CHECKER D — SPORTS REQUIREMENTS (Section 41.D)
        // ─────────────────────────────────────────────────────────────

        private void CheckSportsRequirements(WeeklyMenu menu, MenuValidationResult result)
        {
            var weekStart = menu.StartDate.Date;
            var pending = _db.CoachRequirements
                .Where(r => r.WeekStartDate == weekStart &&
                            r.Status == CoachRequirementStatus.Submitted)
                .ToList();

            foreach (var req in pending)
            {
                var team = _db.Teams.FirstOrDefault(t => t.TeamID == req.TeamId);
                var teamName = team != null ? team.Name : "Unknown team";

                result.Warnings.Add(MakeIssue(
                    ValidationSeverity.Warning,
                    ValidationCategory.Sports,
                    "Coach requirement from '" + teamName + "' not yet responded to: " + req.RequirementText,
                    "CoachRequirement", req.Id));
            }

            var lateActivities = _db.SportsActivities
                .Where(a => a.StartDateTime >= menu.StartDate &&
                            a.StartDateTime <= menu.EndDate &&
                            a.MealRequirement &&
                            a.IsLateRequirement &&
                            !a.IsCancelled)
                .ToList();

            foreach (var activity in lateActivities)
            {
                result.Warnings.Add(MakeIssue(
                    ValidationSeverity.Warning,
                    ValidationCategory.Sports,
                    "Late sports meal requirement: " + activity.Title + " on " +
                    activity.StartDateTime.ToString("ddd dd MMM HH:mm"),
                    "SportsActivity", activity.Id));
            }
        }

        // ─────────────────────────────────────────────────────────────
        // CHECKER E — KITCHEN CAPACITY (Section 41.F)
        // ─────────────────────────────────────────────────────────────

        private void CheckKitchenCapacity(WeeklyMenu menu, MenuValidationResult result)
        {
            result.Information.Add(MakeIssue(
                ValidationSeverity.Info,
                ValidationCategory.Kitchen,
                "Kitchen production capacity check will activate in Phase 8."));
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private MenuValidationIssue MakeIssue(
            ValidationSeverity severity,
            ValidationCategory category,
            string message,
            string relatedEntityType = null,
            int? relatedEntityId = null)
        {
            return new MenuValidationIssue
            {
                Severity = severity,
                Category = category,
                Message = message,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                IsResolved = false,
                CreatedAt = DateTime.Now
            };
        }

        private void SaveIssues(int weeklyMenuId, MenuValidationResult result)
        {
            var old = _db.MenuValidationIssues.Where(i => i.WeeklyMenuId == weeklyMenuId).ToList();
            _db.MenuValidationIssues.RemoveRange(old);

            foreach (var issue in result.CriticalErrors.Concat(result.Warnings).Concat(result.Information))
            {
                issue.WeeklyMenuId = weeklyMenuId;
                _db.MenuValidationIssues.Add(issue);
            }
            _db.SaveChanges();
        }

        public List<MenuValidationIssue> GetOpenIssues(int weeklyMenuId)
        {
            return _db.MenuValidationIssues
                .Where(i => i.WeeklyMenuId == weeklyMenuId && !i.IsResolved)
                .OrderByDescending(i => i.Severity)
                .ThenBy(i => i.Category)
                .ToList();
        }
    }
}