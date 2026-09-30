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
        // ============================================================

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

            // ─────────────────────────────────────────────────────────
            // UC12: load SportPriority records for this menu's week.
            // A high-priority sport pushes its archetype's preferred
            // nutrition category across the whole week.
            // ─────────────────────────────────────────────────────────
            var priorityCategories = new HashSet<NutritionCategory>();

            var priorityRecords = _db.SportPriorities
                .Where(p => p.PriorityLevel == 2)
                .Where(p => p.WeekStartDate <= input.EndDate.Date
                         && p.WeekEndDate >= input.StartDate.Date)
                .ToList();

            foreach (var priority in priorityRecords)
            {
                var archetype = _db.StudentSportStatuses
                    .Where(s => s.Sport == priority.Sport)
                    .Select(s => (SportArchetype?)s.Archetype)
                    .FirstOrDefault();

                if (!archetype.HasValue) continue;

                switch (archetype.Value)
                {
                    case SportArchetype.Power:
                        priorityCategories.Add(NutritionCategory.HighProtein);
                        break;
                    case SportArchetype.Endurance:
                        priorityCategories.Add(NutritionCategory.HighCarb);
                        break;
                        // Skill and Speed sports don't push a category.
                }
            }

            // ─────────────────────────────────────────────────────────
            // UC12: compute per-day, per-meal portion demand once.
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

            var menuItemInventory = activeMenuItems.ToDictionary(
                x => x.Id,
                x => new InventoryState
                {
                    MenuItem = x,
                    FarmAvailable = x.FarmAvailablePortions,
                    ExternalAvailable = x.ExternalAvailablePortions
                });

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
                var matchContext = GetMatchDayContext(date, input);
                matchContext.PriorityCategories = priorityCategories;
                var preferredCategory = PreferredCategoryFor(matchContext);

                foreach (MealSlot mealSlot in GeneratedMealSlots)
                {
                    int portionsForThisSlot = GetPortionsForSlot(
                        demandLookup, date, mealSlot, input.StaffMeals);

                    MenuItem selectedItem =
                        SelectNextMenuItem(
                            activeMenuItems,
                            generationIndex,
                            mealSlot,
                            date,
                            portionsForThisSlot,
                            menuItemInventory,
                            ingredientStock,
                            itemUsage,
                            rejectedSelections,
                            preferredCategory);

                    generationIndex++;

                    InventoryState stock = menuItemInventory[selectedItem.Id];
                    StockAllocation allocation = AllocateStock(stock, portionsForThisSlot);

                    var ingredientShortages = GetIngredientShortages(
                        selectedItem, portionsForThisSlot, ingredientStock);

                    ItemTagStatus status;
                    string reason;

                    if (allocation.TotalAvailable >= portionsForThisSlot
                        && ingredientShortages.Count == 0)
                    {
                        status = ItemTagStatus.Confirmed;
                        reason = BuildConfirmedReason(selectedItem, allocation);

                        if (matchContext.IsMatchDay
                            && selectedItem.NutritionCategory == preferredCategory)
                        {
                            reason = string.Format(
                                "Match-day recovery ({0}). {1}",
                                matchContext.MatchDescription, reason);
                        }
                        else if (matchContext.IsDayBeforeMatch
                                 && selectedItem.NutritionCategory == preferredCategory)
                        {
                            reason = string.Format(
                                "Pre-match carb-loading ({0}). {1}",
                                matchContext.MatchDescription, reason);
                        }
                        else if (matchContext.PriorityCategories != null
                                 && matchContext.PriorityCategories.Count > 0
                                 && selectedItem.NutritionCategory == preferredCategory)
                        {
                            reason = string.Format(
                                "Priority sport nutrition ({0}). {1}",
                                preferredCategory, reason);
                        }

                        DrawIngredientStock(selectedItem, portionsForThisSlot, ingredientStock);
                    }
                    else if (allocation.TotalAvailable > 0
                             || ingredientShortages.Count > 0)
                    {
                        status = ItemTagStatus.NeedsSubstitution;

                        if (ingredientShortages.Count > 0)
                        {
                            reason = BuildIngredientShortageReason(
                                selectedItem, portionsForThisSlot, ingredientShortages);
                        }
                        else
                        {
                            reason = string.Format(
                                "{0} requires {1:N0} portions but only {2:N0} are available. " +
                                "Farm stock is prioritised before external supplier stock.",
                                selectedItem.Name,
                                portionsForThisSlot,
                                allocation.TotalAvailable);
                        }

                        DrawIngredientStock(selectedItem, portionsForThisSlot, ingredientStock);
                    }
                    else
                    {
                        status = ItemTagStatus.NeedsReview;
                        reason = string.Format(
                            "{0} has no available inventory for {1:N0} portions.",
                            selectedItem.Name,
                            portionsForThisSlot);
                    }

                    var scheduleItem = new MenuScheduleItem
                    {
                        MealMenuId = menu.Id,
                        MenuItemId = selectedItem.Id,
                        Date = date,
                        MealSlot = mealSlot,
                        CalculatedPortions = portionsForThisSlot,
                        ItemTagStatus = status,
                        TagReason = reason,
                        SubstitutionMenuItemId = null
                    };

                    _db.MenuScheduleItems.Add(scheduleItem);
                    RecordItemUsage(itemUsage, selectedItem.Id, date, mealSlot);
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
                BoardingHouses = new List<BoardingHouseScheduleOption>()
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

                if (usedDuringCooldown)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "{0} was already scheduled within the previous {1} days and should not be repeated yet.",
                            replacement.Name, MealCooldownDays));
                }
            }

            var menuItems = _db.MenuItems
                .Where(x => x.IsActive)
                .ToList();

            var menuItemInventory = menuItems.ToDictionary(
                x => x.Id,
                x => new InventoryState
                {
                    MenuItem = x,
                    FarmAvailable = x.FarmAvailablePortions,
                    ExternalAvailable = x.ExternalAvailablePortions
                });

            var otherScheduleItems = _db.MenuScheduleItems
                .Where(x => x.MealMenuId == scheduleItem.MealMenuId &&
                            x.Id != scheduleItem.Id)
                .OrderBy(x => x.Date).ThenBy(x => x.MealSlot).ThenBy(x => x.Id)
                .ToList();

            foreach (MenuScheduleItem otherItem in otherScheduleItems)
            {
                InventoryState otherInventory;
                if (menuItemInventory.TryGetValue(otherItem.MenuItemId, out otherInventory))
                {
                    AllocateStock(otherInventory, otherItem.CalculatedPortions);
                }
            }

            InventoryState replacementInventory;
            if (!menuItemInventory.TryGetValue(replacement.Id, out replacementInventory))
            {
                throw new InvalidOperationException(
                    "Inventory information for the selected menu item could not be found.");
            }

            int availableBeforeModification =
                GetTotalAvailableStock(replacementInventory);

            StockAllocation allocation =
                AllocateStock(replacementInventory, newPortions);

            int allocatedPortions = allocation.TotalAvailable;

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

            var otherItemsWithRecipes = _db.MenuScheduleItems
                .Include(x => x.MenuItem.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .Where(x => x.MealMenuId == scheduleItem.MealMenuId &&
                            x.Id != scheduleItem.Id)
                .ToList();

            foreach (var otherItem in otherItemsWithRecipes)
            {
                DrawIngredientStock(otherItem.MenuItem, otherItem.CalculatedPortions, ingredientStock);
            }

            var ingredientShortages = GetIngredientShortages(
                replacement, newPortions, ingredientStock);

            if (allocatedPortions >= newPortions && ingredientShortages.Count == 0)
            {
                scheduleItem.ItemTagStatus = ItemTagStatus.Confirmed;
                scheduleItem.TagReason = BuildConfirmedReason(replacement, allocation);
            }
            else if (ingredientShortages.Count > 0)
            {
                scheduleItem.ItemTagStatus = ItemTagStatus.NeedsSubstitution;
                scheduleItem.TagReason = BuildIngredientShortageReason(
                    replacement, newPortions, ingredientShortages);
            }
            else if (allocatedPortions > 0)
            {
                scheduleItem.ItemTagStatus = ItemTagStatus.NeedsSubstitution;
                scheduleItem.TagReason = string.Format(
                    "{0} requires {1:N0} portions but only {2:N0} " +
                    "are available after accounting for the other meals scheduled in this menu.",
                    replacement.Name, newPortions, availableBeforeModification);
            }
            else
            {
                scheduleItem.ItemTagStatus = ItemTagStatus.NeedsReview;
                scheduleItem.TagReason = string.Format(
                    "{0} has no available inventory after accounting " +
                    "for the other meals scheduled in this menu.",
                    replacement.Name);
            }

            scheduleItem.MenuItemId = replacement.Id;
            scheduleItem.CalculatedPortions = newPortions;
            scheduleItem.SubstitutionMenuItemId = null;

            scheduleItem.MealMenu.MenuStatus = MenuStatus.PendingReview;
            scheduleItem.MealMenu.LastModifiedDate = DateTime.UtcNow;

            _db.SaveChanges();

            return scheduleItem;
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

                option.IsInStock = shortages.Count == 0;

                if (!cooldownOk && !option.IsInStock)
                {
                    option.IsValid = false;
                    option.Reason = "used recently · ingredient short";
                }
                else if (!cooldownOk)
                {
                    option.IsValid = false;
                    option.Reason = string.Format(
                        "used within {0} days", MealCooldownDays);
                }
                else if (!option.IsInStock)
                {
                    option.IsValid = false;

                    var firstShortage = shortages
                        .OrderByDescending(s => s.Required - s.Available)
                        .First();

                    option.Reason = string.Format(
                        "{0} short", firstShortage.Name);
                }
                else
                {
                    option.IsValid = true;
                    option.Reason = "in stock · cooldown OK";
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
        // INGREDIENT REQUIREMENT (BOM) CALCULATION
        // ============================================================

        public List<IngredientRequirement> CalculateIngredientRequirements(
            MenuScheduleItem scheduleItem)
        {
            if (scheduleItem == null)
            {
                throw new ArgumentNullException(nameof(scheduleItem));
            }

            MenuItem menuItem = scheduleItem.MenuItem;

            if (menuItem == null || menuItem.Recipe == null)
            {
                menuItem = _db.MenuItems
                    .Include(x => x.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                    .FirstOrDefault(x => x.Id == scheduleItem.MenuItemId);
            }

            if (menuItem == null)
            {
                throw new InvalidOperationException(
                    "The scheduled item's menu item could not be found.");
            }

            var requirements = new List<IngredientRequirement>();

            if (menuItem.RecipeId == null ||
                menuItem.Recipe == null ||
                menuItem.Recipe.RecipeIngredients == null)
            {
                return requirements;
            }

            Recipe recipe = menuItem.Recipe;

            foreach (RecipeIngredient recipeIngredient in recipe.RecipeIngredients)
            {
                if (recipeIngredient.Ingredient == null) continue;

                decimal requiredQuantity =
                    recipeIngredient.QuantityPerStandardPortion *
                    scheduleItem.CalculatedPortions;

                requirements.Add(new IngredientRequirement
                {
                    IngredientId = recipeIngredient.Ingredient.Id,
                    IngredientName = recipeIngredient.Ingredient.Name,
                    Unit = recipeIngredient.Ingredient.Unit,
                    MenuItemId = menuItem.Id,
                    MenuItemName = menuItem.Name,
                    RecipeId = recipe.Id,
                    RecipeName = recipe.Name,
                    RequiredQuantity = requiredQuantity
                });
            }

            return requirements;
        }

        public List<IngredientRequirement> CalculateIngredientRequirements(int mealMenuId)
        {
            List<MenuScheduleItem> scheduleItems = _db.MenuScheduleItems
                .Include(x => x.MenuItem.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .Where(x => x.MealMenuId == mealMenuId)
                .ToList();

            if (!scheduleItems.Any())
            {
                throw new InvalidOperationException(
                    "The menu was not found or does not contain any scheduled items.");
            }

            var combinedRequirements = new List<IngredientRequirement>();

            foreach (MenuScheduleItem scheduleItem in scheduleItems)
            {
                combinedRequirements.AddRange(
                    CalculateIngredientRequirements(scheduleItem));
            }

            return combinedRequirements
                .GroupBy(x => new { x.IngredientId, x.IngredientName, x.Unit })
                .Select(g => new IngredientRequirement
                {
                    IngredientId = g.Key.IngredientId,
                    IngredientName = g.Key.IngredientName,
                    Unit = g.Key.Unit,
                    RequiredQuantity = g.Sum(x => x.RequiredQuantity)
                })
                .OrderBy(x => x.IngredientName)
                .ToList();
        }

        // ============================================================
        // UC15: BUILD KITCHEN PRODUCTION PLAN
        // UC12: includes per-house breakdown + events per slot.
        // ============================================================

        public ProductionPlanViewModel BuildProductionPlan(int mealMenuId)
        {
            var menu = _db.MealMenus
                .Include(m => m.ScheduleItems.Select(s => s.MenuItem.Recipe.RecipeIngredients.Select(ri => ri.Ingredient)))
                .FirstOrDefault(m => m.Id == mealMenuId);

            if (menu == null)
            {
                throw new InvalidOperationException("Menu not found.");
            }

            var ingredientStock = _db.Ingredients
                .Where(i => i.IsActive)
                .ToDictionary(
                    i => i.Id,
                    i => i.FarmAvailableQuantity + i.ExternalAvailableQuantity);

            // ─────────────────────────────────────────────────────────
            // UC12: load per-house demand for the whole week once.
            // ─────────────────────────────────────────────────────────
            var demandService = new DemandService(_db);
            var allDemand = demandService.ComputeRange(
                menu.StartDate, menu.EndDate, menu.StaffMeals);

            var demandLookup = new Dictionary<string, MealDemand>();
            foreach (var d in allDemand)
            {
                var k = d.Date.ToString("yyyy-MM-dd") + "|" + d.MealSlot;
                if (!demandLookup.ContainsKey(k))
                {
                    demandLookup.Add(k, d);
                }
            }

            var vm = new ProductionPlanViewModel
            {
                MenuId = menu.Id,
                WeekStart = menu.StartDate.Date,
                WeekEnd = menu.EndDate.Date,
                IsProductionConfirmed = menu.IsProductionConfirmed,
                ConfirmedAt = menu.ProductionConfirmedAt,
                TotalMeals = menu.ScheduleItems.Count
            };

            var serveTimes = new Dictionary<MealSlot, TimeSpan>
            {
                { MealSlot.Breakfast, new TimeSpan(7, 0, 0) },
                { MealSlot.Lunch, new TimeSpan(12, 30, 0) },
                { MealSlot.Dinner, new TimeSpan(18, 0, 0) }
            };

            var weekTotals = new Dictionary<int, IngredientLineViewModel>();

            for (var date = menu.StartDate.Date;
                 date <= menu.EndDate.Date;
                 date = date.AddDays(1))
            {
                var dayVm = new ProductionDayViewModel { Date = date };
                var dayTotals = new Dictionary<int, IngredientLineViewModel>();

                foreach (var slot in new[] { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Dinner })
                {
                    var scheduled = menu.ScheduleItems
                        .FirstOrDefault(s => s.Date.Date == date.Date && s.MealSlot == slot);

                    if (scheduled == null || scheduled.MenuItem == null)
                    {
                        continue;
                    }

                    var recipe = scheduled.MenuItem.Recipe;
                    int portions = scheduled.CalculatedPortions;

                    int prepMin = recipe != null ? recipe.PrepTimeMinutes : 0;
                    int cookMin = recipe != null ? recipe.CookTimeMinutes : 0;
                    string station = recipe != null && !string.IsNullOrWhiteSpace(recipe.Station)
                        ? recipe.Station
                        : "Line";

                    TimeSpan serveTime = serveTimes[slot];
                    TimeSpan startTime = serveTime.Subtract(TimeSpan.FromMinutes(prepMin + cookMin));
                    TimeSpan readyTime = serveTime.Subtract(TimeSpan.FromMinutes(5));

                    var task = new ProductionTaskViewModel
                    {
                        DishName = scheduled.MenuItem.Name,
                        Station = station,
                        Portions = portions,
                        PrepMinutes = prepMin,
                        CookMinutes = cookMin,
                        StartTime = startTime,
                        ReadyTime = readyTime
                    };

                    if (recipe != null && recipe.RecipeIngredients != null)
                    {
                        foreach (var ri in recipe.RecipeIngredients)
                        {
                            if (ri.Ingredient == null) continue;

                            decimal required = ri.QuantityPerStandardPortion * portions;
                            decimal available = ingredientStock.ContainsKey(ri.IngredientId)
                                ? ingredientStock[ri.IngredientId]
                                : 0m;

                            var line = new IngredientLineViewModel
                            {
                                IngredientId = ri.IngredientId,
                                IngredientName = ri.Ingredient.Name,
                                Unit = ri.Ingredient.Unit,
                                QuantityPerPortion = ri.QuantityPerStandardPortion,
                                TotalRequired = required,
                                StockAvailable = available,
                                Shortfall = required > available ? required - available : 0m
                            };

                            task.Ingredients.Add(line);
                            AddOrSum(dayTotals, line);
                            AddOrSum(weekTotals, line);
                        }
                    }

                    var slotVm = new ProductionSlotViewModel
                    {
                        MealSlot = slot.ToString(),
                        ServeTime = serveTime,
                        Portions = portions,
                        Tasks = new List<ProductionTaskViewModel> { task }
                    };

                    // ─────────────────────────────────────────────────
                    // UC12: attach per-house breakdown + events.
                    // ─────────────────────────────────────────────────
                    var demandKey = date.ToString("yyyy-MM-dd") + "|" + slot.ToString();
                    MealDemand demand;
                    if (demandLookup.TryGetValue(demandKey, out demand))
                    {
                        foreach (var h in demand.Houses)
                        {
                            slotVm.HouseBreakdown.Add(new HouseBreakdownLine
                            {
                                ResidenceName = h.ResidenceName,
                                ActiveStudents = h.ActiveStudents,
                                Unavailable = h.Unavailable,
                                MatchPlayers = h.MatchPlayers
                            });
                        }

                        foreach (var evt in demand.EventsThisMeal)
                        {
                            slotVm.EventsThisMeal.Add(evt);
                        }
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

            return vm;
        }

        private void AddOrSum(
            Dictionary<int, IngredientLineViewModel> bucket,
            IngredientLineViewModel line)
        {
            if (bucket.ContainsKey(line.IngredientId))
            {
                var existing = bucket[line.IngredientId];
                existing.TotalRequired += line.TotalRequired;
                existing.Shortfall = existing.TotalRequired > existing.StockAvailable
                    ? existing.TotalRequired - existing.StockAvailable
                    : 0m;
            }
            else
            {
                bucket[line.IngredientId] = new IngredientLineViewModel
                {
                    IngredientId = line.IngredientId,
                    IngredientName = line.IngredientName,
                    Unit = line.Unit,
                    QuantityPerPortion = line.QuantityPerPortion,
                    TotalRequired = line.TotalRequired,
                    StockAvailable = line.StockAvailable,
                    Shortfall = line.Shortfall
                };
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

        // ============================================================
        // MATCH-DAY CONTEXT
        // ============================================================

        private class MatchDayContext
        {
            public bool IsMatchDay { get; set; }
            public bool IsDayBeforeMatch { get; set; }
            public string MatchDescription { get; set; }

            public HashSet<NutritionCategory> PriorityCategories { get; set; }

            public MatchDayContext()
            {
                PriorityCategories = new HashSet<NutritionCategory>();
            }
        }

        private MatchDayContext GetMatchDayContext(
            DateTime date,
            ScheduleMenuInputViewModel input)
        {
            var ctx = new MatchDayContext();
            if (input == null || input.BoardingHouses == null) return ctx;

            foreach (var house in input.BoardingHouses)
            {
                if (house == null) continue;
                if (!house.IsInSeason) continue;
                if (!house.MatchDate.HasValue) continue;

                var matchDate = house.MatchDate.Value.Date;

                if (matchDate == date.Date)
                {
                    ctx.IsMatchDay = true;
                    if (string.IsNullOrEmpty(ctx.MatchDescription))
                        ctx.MatchDescription = house.ActiveSport;
                }
                else if (matchDate == date.Date.AddDays(1))
                {
                    ctx.IsDayBeforeMatch = true;
                    if (string.IsNullOrEmpty(ctx.MatchDescription))
                        ctx.MatchDescription = house.ActiveSport;
                }
            }

            return ctx;
        }

        private NutritionCategory PreferredCategoryFor(MatchDayContext ctx)
        {
            if (ctx == null) return NutritionCategory.Standard;

            if (ctx.IsMatchDay) return NutritionCategory.HighProtein;
            if (ctx.IsDayBeforeMatch) return NutritionCategory.HighCarb;

            if (ctx.PriorityCategories != null)
            {
                if (ctx.PriorityCategories.Contains(NutritionCategory.HighProtein))
                    return NutritionCategory.HighProtein;

                if (ctx.PriorityCategories.Contains(NutritionCategory.HighCarb))
                    return NutritionCategory.HighCarb;
            }

            return NutritionCategory.Standard;
        }

        // ============================================================
        // MENU ITEM SELECTION
        // ============================================================

        private MenuItem SelectNextMenuItem(
            IList<MenuItem> items,
            int index,
            MealSlot slot,
            DateTime date,
            int requiredPortions,
            Dictionary<int, InventoryState> menuItemInventory,
            Dictionary<int, IngredientState> ingredientStock,
            Dictionary<int, List<MealUsage>> itemUsage,
            List<RejectedSelection> rejectedSelections,
            NutritionCategory preferredCategory)
        {
            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("No menu items are available.");
            }

            List<MenuItem> compatibleItems = items
                .Where(x => IsCompatibleWithMealSlot(x, slot))
                .ToList();

            if (compatibleItems.Count == 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "No active menu items are configured for the {0} meal slot.",
                        slot));
            }

            int startIndex = index % compatibleItems.Count;

            if (preferredCategory != NutritionCategory.Standard)
            {
                for (int offset = 0; offset < compatibleItems.Count; offset++)
                {
                    int candidateIndex = (startIndex + offset) % compatibleItems.Count;
                    MenuItem candidate = compatibleItems[candidateIndex];

                    if (candidate.NutritionCategory != preferredCategory) continue;
                    if (WasSelectionRejected(rejectedSelections, candidate.Id, date, slot)) continue;
                    if (WasItemUsedOnDate(itemUsage, candidate.Id, date)) continue;
                    if (IsWithinCooldown(itemUsage, candidate.Id, date, slot)) continue;

                    InventoryState stock = menuItemInventory[candidate.Id];
                    if (GetTotalAvailableStock(stock) < requiredPortions) continue;

                    if (GetIngredientShortages(candidate, requiredPortions, ingredientStock).Count > 0)
                        continue;

                    return candidate;
                }
            }

            for (int offset = 0; offset < compatibleItems.Count; offset++)
            {
                int candidateIndex = (startIndex + offset) % compatibleItems.Count;
                MenuItem candidate = compatibleItems[candidateIndex];

                if (WasSelectionRejected(rejectedSelections, candidate.Id, date, slot)) continue;
                if (WasItemUsedOnDate(itemUsage, candidate.Id, date)) continue;
                if (IsWithinCooldown(itemUsage, candidate.Id, date, slot)) continue;

                InventoryState stock = menuItemInventory[candidate.Id];
                if (GetTotalAvailableStock(stock) < requiredPortions) continue;

                if (GetIngredientShortages(candidate, requiredPortions, ingredientStock).Count > 0)
                {
                    continue;
                }

                return candidate;
            }

            for (int offset = 0; offset < compatibleItems.Count; offset++)
            {
                int candidateIndex = (startIndex + offset) % compatibleItems.Count;
                MenuItem candidate = compatibleItems[candidateIndex];

                if (WasSelectionRejected(rejectedSelections, candidate.Id, date, slot)) continue;
                if (WasItemUsedOnDate(itemUsage, candidate.Id, date)) continue;
                if (IsWithinCooldown(itemUsage, candidate.Id, date, slot)) continue;

                InventoryState stock = menuItemInventory[candidate.Id];
                if (GetTotalAvailableStock(stock) < requiredPortions) continue;

                return candidate;
            }

            for (int offset = 0; offset < compatibleItems.Count; offset++)
            {
                int candidateIndex = (startIndex + offset) % compatibleItems.Count;
                MenuItem candidate = compatibleItems[candidateIndex];

                if (WasSelectionRejected(rejectedSelections, candidate.Id, date, slot)) continue;
                if (WasItemUsedOnDate(itemUsage, candidate.Id, date)) continue;
                if (IsWithinCooldown(itemUsage, candidate.Id, date, slot)) continue;

                InventoryState stock = menuItemInventory[candidate.Id];
                if (GetTotalAvailableStock(stock) > 0) return candidate;
            }

            for (int offset = 0; offset < compatibleItems.Count; offset++)
            {
                int candidateIndex = (startIndex + offset) % compatibleItems.Count;
                MenuItem candidate = compatibleItems[candidateIndex];

                if (WasSelectionRejected(rejectedSelections, candidate.Id, date, slot)) continue;
                if (WasItemUsedOnDate(itemUsage, candidate.Id, date)) continue;
                if (IsWithinCooldown(itemUsage, candidate.Id, date, slot)) continue;

                return candidate;
            }

            for (int offset = 0; offset < compatibleItems.Count; offset++)
            {
                int candidateIndex = (startIndex + offset) % compatibleItems.Count;
                MenuItem candidate = compatibleItems[candidateIndex];

                if (WasSelectionRejected(rejectedSelections, candidate.Id, date, slot)) continue;
                if (WasItemUsedOnDate(itemUsage, candidate.Id, date)) continue;

                return candidate;
            }

            return compatibleItems[startIndex];
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

        private bool WasItemUsedOnDate(
            Dictionary<int, List<MealUsage>> itemUsage,
            int menuItemId,
            DateTime date)
        {
            List<MealUsage> usages;
            if (!itemUsage.TryGetValue(menuItemId, out usages)) return false;
            return usages.Any(x => x.Date.Date == date.Date);
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

        private string BuildIngredientShortageReason(
            MenuItem item,
            int portions,
            List<IngredientShortage> shortages)
        {
            var top = shortages
                .OrderByDescending(s => s.Required - s.Available)
                .Take(3)
                .Select(s => string.Format(
                    "{0}: requires {1:N2} {2}, available {3:N2} {2}",
                    s.Name, s.Required, s.Unit, s.Available));

            string detail = string.Join("; ", top);

            if (shortages.Count > 3)
            {
                detail += string.Format(" (+{0} more)", shortages.Count - 3);
            }

            return string.Format(
                "{0}: ingredient shortfall for {1:N0} portions. {2}.",
                item.Name, portions, detail);
        }

        // ============================================================
        // MENU-ITEM INVENTORY
        // ============================================================

        private StockAllocation AllocateStock(
            InventoryState inventory,
            int requiredPortions)
        {
            int farmUsed = 0;
            int externalUsed = 0;

            if (inventory.MenuItem.IsFarmGrownProduce)
            {
                farmUsed = Math.Min(inventory.FarmAvailable, requiredPortions);
                inventory.FarmAvailable -= farmUsed;

                int remaining = requiredPortions - farmUsed;
                externalUsed = Math.Min(inventory.ExternalAvailable, remaining);
                inventory.ExternalAvailable -= externalUsed;
            }
            else
            {
                externalUsed = Math.Min(inventory.ExternalAvailable, requiredPortions);
                inventory.ExternalAvailable -= externalUsed;
            }

            return new StockAllocation
            {
                FarmUsed = farmUsed,
                ExternalUsed = externalUsed
            };
        }

        private int GetTotalAvailableStock(InventoryState inventory)
        {
            if (inventory == null) return 0;

            if (inventory.MenuItem.IsFarmGrownProduce)
            {
                return inventory.FarmAvailable + inventory.ExternalAvailable;
            }

            return inventory.ExternalAvailable;
        }

        private string BuildConfirmedReason(
            MenuItem item,
            StockAllocation allocation)
        {
            if (item.IsFarmGrownProduce && allocation.FarmUsed > 0)
            {
                if (allocation.ExternalUsed > 0)
                {
                    return string.Format(
                        "Confirmed using {0:N0} farm-grown portions and " +
                        "{1:N0} external supplier portions.",
                        allocation.FarmUsed, allocation.ExternalUsed);
                }
                return string.Format(
                    "Confirmed using {0:N0} farm-grown portions.",
                    allocation.FarmUsed);
            }

            return string.Format(
                "Confirmed using {0:N0} external supplier portions.",
                allocation.ExternalUsed);
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

        public class IngredientRequirement
        {
            public int IngredientId { get; set; }
            public string IngredientName { get; set; }
            public string Unit { get; set; }
            public int MenuItemId { get; set; }
            public string MenuItemName { get; set; }
            public int RecipeId { get; set; }
            public string RecipeName { get; set; }
            public decimal RequiredQuantity { get; set; }
        }

        private class IngredientShortage
        {
            public int IngredientId { get; set; }
            public string Name { get; set; }
            public string Unit { get; set; }
            public decimal Required { get; set; }
            public decimal Available { get; set; }
        }

        private class InventoryState
        {
            public MenuItem MenuItem { get; set; }
            public int FarmAvailable { get; set; }
            public int ExternalAvailable { get; set; }
        }

        private class IngredientState
        {
            public Ingredient Ingredient { get; set; }
            public decimal FarmAvailable { get; set; }
            public decimal ExternalAvailable { get; set; }
        }

        private class StockAllocation
        {
            public int FarmUsed { get; set; }
            public int ExternalUsed { get; set; }
            public int TotalAvailable { get { return FarmUsed + ExternalUsed; } }
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