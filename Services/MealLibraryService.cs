using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Dietitian Meal Library ("Create Meal")
    //
    // A meal is the existing MenuItem → Recipe → RecipeIngredient →
    // Ingredient structure. No separate Meal entity.
    //
    // What the Dietitian enters vs what is derived:
    //   Entered : name, description, meal slots, dietary classification
    //             (Standard / Vegetarian / Vegan), NutritionCategory,
    //             ingredients + batch quantities, instructions, timing,
    //             station.
    //   Derived : calories / protein / carbs / fat per portion, allergens
    //             and halal status — all from the ingredients, so the
    //             meal can never contradict its own recipe.
    //
    // Quantities are entered for the whole batch and stored per
    // portion (RecipeIngredient.QuantityPerStandardPortion), which is
    // what the scheduler, production plan and BOM already multiply by.
    //
    // Stock is intentionally not handled here.
    // ============================================================

    public class NutritionTotals
    {
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbohydrate { get; set; }
        public decimal Fat { get; set; }
    }

    public class MealLibraryService
    {
        private readonly DBContextClass _db;

        public MealLibraryService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // ============================================================
        // CLASSIFICATIONS, THRESHOLDS, UNITS
        // ============================================================

        public const string ClassificationStandard = "Standard";
        public const string ClassificationVegetarian = "Vegetarian";
        public const string ClassificationVegan = "Vegan";

        public static readonly List<DietaryOption> ClassificationOptions = new List<DietaryOption>
        {
            new DietaryOption(ClassificationStandard,   "Standard (may contain meat or fish)"),
            new DietaryOption(ClassificationVegetarian, "Vegetarian"),
            new DietaryOption(ClassificationVegan,      "Vegan")
        };

        // Per-portion thresholds used to SUGGEST a NutritionCategory
        public const decimal HighProteinGrams = 40m;
        public const decimal HighCarbGrams = 90m;
        public const decimal LightCalories = 450m;

        private static readonly string[] DefaultStations = { "Cold Prep", "Stove", "Grill", "Oven", "Bakery", "Line" };

        private static readonly string[] BaseUnits = { "g", "ml" };

        // Units the Dietitian may enter for an ingredient measured in baseUnit
        public static List<string> UnitsFor(string baseUnit)
        {
            if (string.Equals(baseUnit, "g", StringComparison.OrdinalIgnoreCase)) return new List<string> { "g", "kg" };
            if (string.Equals(baseUnit, "ml", StringComparison.OrdinalIgnoreCase)) return new List<string> { "ml", "l" };
            return new List<string> { baseUnit };
        }

        // Converts an entered quantity into the ingredient's own unit
        public static bool TryConvertToBase(decimal quantity, string unit, string baseUnit, out decimal baseQuantity)
        {
            baseQuantity = 0m;
            unit = (unit ?? "").Trim();

            if (string.Equals(unit, baseUnit, StringComparison.OrdinalIgnoreCase))
            {
                baseQuantity = quantity;
                return true;
            }

            if (string.Equals(baseUnit, "g", StringComparison.OrdinalIgnoreCase)
                && string.Equals(unit, "kg", StringComparison.OrdinalIgnoreCase))
            {
                baseQuantity = quantity * 1000m;
                return true;
            }

            if (string.Equals(baseUnit, "ml", StringComparison.OrdinalIgnoreCase)
                && string.Equals(unit, "l", StringComparison.OrdinalIgnoreCase))
            {
                baseQuantity = quantity * 1000m;
                return true;
            }

            return false;
        }

        // Accepts "0.5" and "0,5" whatever the server culture is.
        public static bool TryParseDecimal(string text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;

            return decimal.TryParse(
                text.Trim().Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
        }

        // ============================================================
        // DERIVED FACTS (static — also used by the seed)
        // ============================================================

        public static NutritionTotals CalculateNutrition(IEnumerable<RecipeIngredient> recipeIngredients)
        {
            var totals = new NutritionTotals();

            foreach (var ri in recipeIngredients)
            {
                if (ri.Ingredient == null) continue;

                decimal q = ri.QuantityPerStandardPortion;
                totals.Calories += ri.Ingredient.CaloriesPerUnit * q;
                totals.Protein += ri.Ingredient.ProteinGramsPerUnit * q;
                totals.Carbohydrate += ri.Ingredient.CarbohydrateGramsPerUnit * q;
                totals.Fat += ri.Ingredient.FatGramsPerUnit * q;
            }

            totals.Calories = Math.Round(totals.Calories, 2);
            totals.Protein = Math.Round(totals.Protein, 2);
            totals.Carbohydrate = Math.Round(totals.Carbohydrate, 2);
            totals.Fat = Math.Round(totals.Fat, 2);

            return totals;
        }

        public static void ApplyNutrition(MenuItem item, NutritionTotals totals)
        {
            item.CaloriesPerPortion = totals.Calories;
            item.ProteinGramsPerPortion = totals.Protein;
            item.CarbohydrateGramsPerPortion = totals.Carbohydrate;
            item.FatGramsPerPortion = totals.Fat;
        }

        public static NutritionCategory SuggestCategory(NutritionTotals totals)
        {
            if (totals.Protein >= HighProteinGrams) return NutritionCategory.HighProtein;
            if (totals.Carbohydrate >= HighCarbGrams) return NutritionCategory.HighCarb;
            if (totals.Calories < LightCalories) return NutritionCategory.Light;
            return NutritionCategory.Standard;
        }

        // A warning (never an error) when the chosen category disagrees
        // with the calculated values. Hydration is always a manual choice.
        public static string CategoryWarning(NutritionCategory category, NutritionTotals totals)
        {
            switch (category)
            {
                case NutritionCategory.HighProtein:
                    if (totals.Protein < HighProteinGrams)
                        return string.Format("Marked High protein, but it has {0:0.#} g protein per portion (suggested minimum {1:0} g).",
                            totals.Protein, HighProteinGrams);
                    break;

                case NutritionCategory.HighCarb:
                    if (totals.Carbohydrate < HighCarbGrams)
                        return string.Format("Marked High carb, but it has {0:0.#} g carbohydrate per portion (suggested minimum {1:0} g).",
                            totals.Carbohydrate, HighCarbGrams);
                    break;

                case NutritionCategory.Light:
                    if (totals.Calories >= LightCalories)
                        return string.Format("Marked Light, but it has {0:0} kcal per portion (suggested maximum {1:0} kcal).",
                            totals.Calories, LightCalories);
                    break;
            }

            return null;
        }

        // The strictest classification the ingredients allow
        public static string StrictestClassification(IEnumerable<Ingredient> ingredients)
        {
            var list = ingredients.ToList();

            if (DietaryProfileService.NonVeganIngredients(list).Count == 0) return ClassificationVegan;
            if (DietaryProfileService.NonVegetarianIngredients(list).Count == 0) return ClassificationVegetarian;
            return ClassificationStandard;
        }

        private static int Rank(string classification)
        {
            if (string.Equals(classification, ClassificationVegan, StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(classification, ClassificationVegetarian, StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        private static string SlotSummary(MenuItem item)
        {
            var slots = new List<string>();
            if (item.IsBreakfastItem) slots.Add("Breakfast");
            if (item.IsLunchItem) slots.Add("Lunch");
            if (item.IsDinnerItem) slots.Add("Dinner");
            return string.Join(", ", slots);
        }

        private static List<Ingredient> IngredientsOf(MenuItem item)
        {
            if (item.Recipe == null || item.Recipe.RecipeIngredients == null) return new List<Ingredient>();

            return item.Recipe.RecipeIngredients
                .Where(ri => ri.Ingredient != null)
                .Select(ri => ri.Ingredient)
                .ToList();
        }

        // ============================================================
        // QUERIES
        // ============================================================

        public List<MealListItemViewModel> GetMeals(bool includeInactive)
        {
            var items = _db.MenuItems
                .Include(m => m.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .Where(m => includeInactive || m.IsActive)
                .OrderBy(m => m.Name)
                .ToList();

            return items.Select(m =>
            {
                var ingredients = IngredientsOf(m);

                return new MealListItemViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    DietaryClassification = m.DietaryClassification,
                    IsHalal = ingredients.Count > 0 && DietaryProfileService.GetHalalProblem(ingredients) == null,
                    NutritionCategory = m.NutritionCategory,
                    MealSlots = SlotSummary(m),
                    CaloriesPerPortion = m.CaloriesPerPortion,
                    ProteinGramsPerPortion = m.ProteinGramsPerPortion,
                    Allergens = string.Join(", ", DietaryProfileService.GetAllergens(ingredients)),
                    IngredientCount = ingredients.Count,
                    IsActive = m.IsActive
                };
            }).ToList();
        }

        public MealDetailsViewModel GetDetails(int id)
        {
            var item = LoadMeal(id);
            if (item == null) return null;

            var ingredients = IngredientsOf(item);
            var lines = item.Recipe != null ? item.Recipe.RecipeIngredients.ToList() : new List<RecipeIngredient>();
            var totals = CalculateNutrition(lines);
            int batch = item.Recipe != null ? Math.Max(1, item.Recipe.StandardPortionCount) : 1;
            var halalProblem = ingredients.Count > 0
                ? DietaryProfileService.GetHalalProblem(ingredients)
                : "no ingredients recorded";

            return new MealDetailsViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                MealSlots = SlotSummary(item),
                IsFarmGrownProduce = item.IsFarmGrownProduce,
                IsActive = item.IsActive,

                DietaryClassification = item.DietaryClassification,
                StrictestAllowedClassification = StrictestClassification(ingredients),
                IsHalal = halalProblem == null,
                HalalProblem = halalProblem,
                Allergens = DietaryProfileService.GetAllergens(ingredients),

                NutritionCategory = item.NutritionCategory,
                SuggestedNutritionCategory = SuggestCategory(totals),
                NutritionCategoryWarning = CategoryWarning(item.NutritionCategory, totals),

                // Stored values (recalculated on every save)
                CaloriesPerPortion = item.CaloriesPerPortion,
                ProteinGramsPerPortion = item.ProteinGramsPerPortion,
                CarbohydrateGramsPerPortion = item.CarbohydrateGramsPerPortion,
                FatGramsPerPortion = item.FatGramsPerPortion,

                PortionsPerBatch = batch,
                Instructions = item.Recipe != null ? item.Recipe.PreparationNotes : null,
                PrepTimeMinutes = item.Recipe != null ? item.Recipe.PrepTimeMinutes : 0,
                CookTimeMinutes = item.Recipe != null ? item.Recipe.CookTimeMinutes : 0,
                Station = item.Recipe != null ? item.Recipe.Station : null,

                Ingredients = lines
                    .Where(ri => ri.Ingredient != null)
                    .OrderBy(ri => ri.Ingredient.Name)
                    .Select(ri => new MealDetailsIngredientViewModel
                    {
                        Name = ri.Ingredient.Name,
                        Unit = ri.Ingredient.Unit,
                        QuantityPerPortion = ri.QuantityPerStandardPortion,
                        QuantityPerBatch = ri.QuantityPerStandardPortion * batch,
                        Allergens = string.Join(", ", DietaryProfileService.GetAllergens(new[] { ri.Ingredient })),
                        Notes = ri.PreparationNotes
                    })
                    .ToList(),

                UsageSummary = BuildUsageSummary(item)
            };
        }

        public MealFormViewModel NewForm()
        {
            return new MealFormViewModel();
        }

        public MealFormViewModel GetForm(int id)
        {
            var item = LoadMeal(id);
            if (item == null) return null;

            var recipe = item.Recipe;
            int batch = recipe != null ? Math.Max(1, recipe.StandardPortionCount) : 1;

            var vm = new MealFormViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IsBreakfastItem = item.IsBreakfastItem,
                IsLunchItem = item.IsLunchItem,
                IsDinnerItem = item.IsDinnerItem,
                IsFarmGrownProduce = item.IsFarmGrownProduce,
                DietaryClassification = item.DietaryClassification,
                NutritionCategory = Enum.IsDefined(typeof(NutritionCategory), item.NutritionCategory)
                    ? item.NutritionCategory
                    : NutritionCategory.Standard,
                PortionsPerBatch = batch,
                Instructions = recipe != null ? recipe.PreparationNotes : null,
                PrepTimeMinutes = recipe != null ? recipe.PrepTimeMinutes : 0,
                CookTimeMinutes = recipe != null ? recipe.CookTimeMinutes : 0,
                Station = recipe != null ? recipe.Station : null,
                UsageWarning = BuildUsageSummary(item)
            };

            if (recipe != null)
            {
                vm.Ingredients = recipe.RecipeIngredients
                    .Where(ri => ri.Ingredient != null)
                    .OrderBy(ri => ri.Ingredient.Name)
                    .Select(ri => new MealIngredientLineViewModel
                    {
                        IngredientId = ri.IngredientId,
                        Quantity = (ri.QuantityPerStandardPortion * batch).ToString("0.####", CultureInfo.InvariantCulture),
                        Unit = ri.Ingredient.Unit,
                        Notes = ri.PreparationNotes
                    })
                    .ToList();
            }

            return vm;
        }

        // Active ingredients, plus any inactive ones already on this meal
        public List<IngredientOptionViewModel> GetIngredientOptions(IEnumerable<int> alsoInclude = null)
        {
            var extra = (alsoInclude ?? Enumerable.Empty<int>()).ToList();

            return _db.Ingredients
                .Where(i => i.IsActive || extra.Contains(i.Id))
                .OrderBy(i => i.Name)
                .ToList()
                .Select(ToOption)
                .ToList();
        }

        public static IngredientOptionViewModel ToOption(Ingredient i)
        {
            var single = new[] { i };

            return new IngredientOptionViewModel
            {
                Id = i.Id,
                Name = i.Name,
                Unit = i.Unit,
                CaloriesPerUnit = i.CaloriesPerUnit,
                ProteinGramsPerUnit = i.ProteinGramsPerUnit,
                CarbohydrateGramsPerUnit = i.CarbohydrateGramsPerUnit,
                FatGramsPerUnit = i.FatGramsPerUnit,
                Allergens = DietaryProfileService.GetAllergens(single),
                IsNonVegetarian = DietaryProfileService.NonVegetarianIngredients(single).Count > 0,
                IsNonVegan = DietaryProfileService.NonVeganIngredients(single).Count > 0,
                HalalProblem = DietaryProfileService.GetHalalProblem(single)
            };
        }

        public List<string> GetStationSuggestions()
        {
            var used = _db.Recipes
                .Where(r => r.Station != null && r.Station != "")
                .Select(r => r.Station)
                .Distinct()
                .ToList();

            return DefaultStations
                .Concat(used)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
        }

        // ============================================================
        // VALIDATION
        // ============================================================

        public List<string> Validate(MealFormViewModel vm)
        {
            var errors = new List<string>();

            if (vm == null)
            {
                errors.Add("No meal information was received.");
                return errors;
            }

            // ── Basic information ──
            var name = (vm.Name ?? "").Trim();

            if (name.Length == 0)
            {
                errors.Add("Meal name is required.");
            }
            else if (name.Length > 200)
            {
                errors.Add("Meal name must be 200 characters or fewer.");
            }
            else
            {
                bool duplicate = _db.MenuItems.Any(m =>
                    m.Name.ToLower() == name.ToLower()
                    && (!vm.Id.HasValue || m.Id != vm.Id.Value));

                if (duplicate)
                    errors.Add(string.Format("A meal called \"{0}\" already exists.", name));
            }

            if ((vm.Description ?? "").Trim().Length > 500)
                errors.Add("Description must be 500 characters or fewer.");

            if (!vm.IsBreakfastItem && !vm.IsLunchItem && !vm.IsDinnerItem)
                errors.Add("Choose at least one meal type (breakfast, lunch or dinner).");

            if (!ClassificationOptions.Any(o => string.Equals(o.Code, vm.DietaryClassification, StringComparison.OrdinalIgnoreCase)))
                errors.Add("Choose a dietary classification.");

            if (!Enum.IsDefined(typeof(NutritionCategory), vm.NutritionCategory))
                errors.Add("Choose a nutrition category.");

            // ── Preparation ──
            if (vm.PortionsPerBatch < 1 || vm.PortionsPerBatch > 100000)
                errors.Add("Portions this recipe makes must be between 1 and 100 000.");

            if (vm.PrepTimeMinutes < 0 || vm.PrepTimeMinutes > 1440)
                errors.Add("Preparation time must be between 0 and 1440 minutes.");

            if (vm.CookTimeMinutes < 0 || vm.CookTimeMinutes > 1440)
                errors.Add("Cooking time must be between 0 and 1440 minutes.");

            if ((vm.Station ?? "").Trim().Length > 50)
                errors.Add("Kitchen station must be 50 characters or fewer.");

            if ((vm.Instructions ?? "").Trim().Length > 1000)
                errors.Add("Recipe / instructions must be 1000 characters or fewer.");

            // ── Ingredients ──
            var lines = ResolveLines(vm, errors);

            if (lines.Count == 0 && !errors.Any(e => e.StartsWith("Ingredient")))
                errors.Add("Add at least one ingredient.");

            // ── Classification must not contradict the ingredients ──
            if (lines.Count > 0)
            {
                var ingredients = lines.Select(l => l.Ingredient).ToList();

                if (Rank(vm.DietaryClassification) == 2)
                {
                    var hits = DietaryProfileService.NonVeganIngredients(ingredients);
                    if (hits.Count > 0)
                        errors.Add("This meal can't be Vegan — it contains " + string.Join(", ", hits.Select(h => h.Name)) + ".");
                }
                else if (Rank(vm.DietaryClassification) == 1)
                {
                    var hits = DietaryProfileService.NonVegetarianIngredients(ingredients);
                    if (hits.Count > 0)
                        errors.Add("This meal can't be Vegetarian — it contains " + string.Join(", ", hits.Select(h => h.Name)) + ".");
                }
            }

            return errors;
        }

        private class ResolvedLine
        {
            public Ingredient Ingredient { get; set; }
            public decimal QuantityPerPortion { get; set; }
            public string Notes { get; set; }
        }

        // Turns the posted rows into (ingredient, per-portion quantity).
        // Adds user-facing errors for anything invalid.
        private List<ResolvedLine> ResolveLines(MealFormViewModel vm, List<string> errors)
        {
            var result = new List<ResolvedLine>();
            var rows = (vm.Ingredients ?? new List<MealIngredientLineViewModel>())
                .Where(r => r != null && !(r.IngredientId == 0 && string.IsNullOrWhiteSpace(r.Quantity)))
                .ToList();

            if (rows.Count == 0) return result;

            var ids = rows.Select(r => r.IngredientId).Distinct().ToList();
            var ingredients = _db.Ingredients
                .Where(i => ids.Contains(i.Id))
                .ToDictionary(i => i.Id);

            int batch = Math.Max(1, vm.PortionsPerBatch);
            var seen = new HashSet<int>();

            foreach (var row in rows)
            {
                Ingredient ingredient;
                if (row.IngredientId == 0 || !ingredients.TryGetValue(row.IngredientId, out ingredient))
                {
                    errors.Add("Ingredient: choose an ingredient for every row, or remove the empty row.");
                    continue;
                }

                if (!seen.Add(ingredient.Id))
                {
                    errors.Add(string.Format("Ingredient: {0} is listed more than once — combine the quantities into one row.", ingredient.Name));
                    continue;
                }

                decimal quantity;
                if (!TryParseDecimal(row.Quantity, out quantity) || quantity <= 0)
                {
                    errors.Add(string.Format("Ingredient: the quantity for {0} must be a number greater than zero.", ingredient.Name));
                    continue;
                }

                decimal baseQuantity;
                if (!TryConvertToBase(quantity, row.Unit, ingredient.Unit, out baseQuantity))
                {
                    errors.Add(string.Format("Ingredient: {0} is measured in {1}; \"{2}\" can't be used.", ingredient.Name, ingredient.Unit, row.Unit));
                    continue;
                }

                decimal perPortion = Math.Round(baseQuantity / batch, 4);
                if (perPortion < 0.001m)
                {
                    errors.Add(string.Format("Ingredient: the quantity of {0} is too small per portion.", ingredient.Name));
                    continue;
                }

                if ((row.Notes ?? "").Trim().Length > 500)
                {
                    errors.Add(string.Format("Ingredient: the note for {0} must be 500 characters or fewer.", ingredient.Name));
                    continue;
                }

                result.Add(new ResolvedLine
                {
                    Ingredient = ingredient,
                    QuantityPerPortion = perPortion,
                    Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim()
                });
            }

            return result;
        }

        // ============================================================
        // CREATE / UPDATE
        // ============================================================

        public int Create(MealFormViewModel vm)
        {
            var errors = new List<string>();
            var lines = ThrowIfInvalid(vm, errors);

            var recipe = new Recipe { IsActive = true };
            var item = new MenuItem
            {
                Recipe = recipe,
                IsActive = true,
                FarmAvailablePortions = 0,
                ExternalAvailablePortions = 0
            };

            ApplyForm(item, recipe, vm, lines);

            _db.MenuItems.Add(item);
            _db.SaveChanges();

            return item.Id;
        }

        public void Update(MealFormViewModel vm)
        {
            if (vm == null || !vm.Id.HasValue)
                throw new InvalidOperationException("Meal not found.");

            var errors = new List<string>();
            var lines = ThrowIfInvalid(vm, errors);

            var item = LoadMeal(vm.Id.Value);
            if (item == null)
                throw new InvalidOperationException("Meal not found.");

            var recipe = item.Recipe;
            if (recipe == null)
            {
                recipe = new Recipe { IsActive = item.IsActive };
                item.Recipe = recipe;
            }

            // Remove ingredients no longer on the meal
            var keepIds = new HashSet<int>(lines.Select(l => l.Ingredient.Id));
            foreach (var removed in recipe.RecipeIngredients.Where(ri => !keepIds.Contains(ri.IngredientId)).ToList())
            {
                _db.RecipeIngredients.Remove(removed);
            }

            ApplyForm(item, recipe, vm, lines);

            _db.SaveChanges();
        }

        private List<ResolvedLine> ThrowIfInvalid(MealFormViewModel vm, List<string> errors)
        {
            errors.AddRange(Validate(vm));
            if (errors.Count > 0)
                throw new InvalidOperationException(errors[0]);

            var lineErrors = new List<string>();
            return ResolveLines(vm, lineErrors);
        }

        private void ApplyForm(MenuItem item, Recipe recipe, MealFormViewModel vm, List<ResolvedLine> lines)
        {
            var name = vm.Name.Trim();

            item.Name = name;
            item.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
            item.IsBreakfastItem = vm.IsBreakfastItem;
            item.IsLunchItem = vm.IsLunchItem;
            item.IsDinnerItem = vm.IsDinnerItem;
            item.IsFarmGrownProduce = vm.IsFarmGrownProduce;
            item.DietaryClassification = ClassificationOptions
                .First(o => string.Equals(o.Code, vm.DietaryClassification, StringComparison.OrdinalIgnoreCase)).Code;
            item.NutritionCategory = vm.NutritionCategory;

            // Keep the recipe name in step with the meal unless another
            // meal shares this recipe.
            bool shared = recipe.Id > 0 && _db.MenuItems.Any(m => m.RecipeId == recipe.Id && m.Id != item.Id);
            if (!shared || string.IsNullOrWhiteSpace(recipe.Name))
            {
                recipe.Name = name;
            }

            recipe.PreparationNotes = string.IsNullOrWhiteSpace(vm.Instructions) ? null : vm.Instructions.Trim();
            recipe.StandardPortionCount = vm.PortionsPerBatch;
            recipe.PrepTimeMinutes = vm.PrepTimeMinutes;
            recipe.CookTimeMinutes = vm.CookTimeMinutes;
            recipe.Station = string.IsNullOrWhiteSpace(vm.Station) ? null : vm.Station.Trim();

            foreach (var line in lines)
            {
                var existing = recipe.RecipeIngredients.FirstOrDefault(ri => ri.IngredientId == line.Ingredient.Id);

                if (existing == null)
                {
                    recipe.RecipeIngredients.Add(new RecipeIngredient
                    {
                        Recipe = recipe,
                        IngredientId = line.Ingredient.Id,
                        Ingredient = line.Ingredient,
                        QuantityPerStandardPortion = line.QuantityPerPortion,
                        PreparationNotes = line.Notes
                    });
                }
                else
                {
                    existing.QuantityPerStandardPortion = line.QuantityPerPortion;
                    existing.PreparationNotes = line.Notes;
                }
            }

            // Nutrition is always recalculated from the final ingredient list
            var current = recipe.RecipeIngredients
                .Where(ri => lines.Any(l => l.Ingredient.Id == ri.IngredientId))
                .ToList();

            ApplyNutrition(item, CalculateNutrition(current));
        }

        // ============================================================
        // ACTIVATE / DEACTIVATE — never delete
        // ============================================================

        public void SetActive(int id, bool active)
        {
            var item = _db.MenuItems.Include(m => m.Recipe).FirstOrDefault(m => m.Id == id);
            if (item == null) throw new InvalidOperationException("Meal not found.");

            item.IsActive = active;

            if (item.Recipe != null)
            {
                if (active)
                {
                    item.Recipe.IsActive = true;
                }
                else
                {
                    bool otherActiveUsers = _db.MenuItems.Any(m =>
                        m.RecipeId == item.RecipeId && m.Id != item.Id && m.IsActive);

                    if (!otherActiveUsers) item.Recipe.IsActive = false;
                }
            }

            _db.SaveChanges();
        }

        // ============================================================
        // NEW INGREDIENT (from the Create Meal page)
        // ============================================================

        public Ingredient CreateIngredient(NewIngredientViewModel vm, List<string> errors)
        {
            if (vm == null)
            {
                errors.Add("No ingredient information was received.");
                return null;
            }

            var name = (vm.Name ?? "").Trim();

            if (name.Length == 0) errors.Add("Ingredient name is required.");
            else if (name.Length > 200) errors.Add("Ingredient name must be 200 characters or fewer.");
            else
            {
                var existing = _db.Ingredients.FirstOrDefault(i => i.Name.ToLower() == name.ToLower());
                if (existing != null)
                {
                    errors.Add(existing.IsActive
                        ? string.Format("\"{0}\" already exists — choose it from the ingredient list.", existing.Name)
                        : string.Format("\"{0}\" already exists but is inactive.", existing.Name));
                }
            }

            var unit = (vm.Unit ?? "").Trim().ToLowerInvariant();
            if (!BaseUnits.Contains(unit)) errors.Add("Choose a unit (g or ml).");

            decimal kcal, protein, carbs, fat;
            ParseNutrient(vm.CaloriesPer100, "Calories", 1000m, errors, out kcal);
            ParseNutrient(vm.ProteinPer100, "Protein", 100m, errors, out protein);
            ParseNutrient(vm.CarbohydratePer100, "Carbohydrate", 100m, errors, out carbs);
            ParseNutrient(vm.FatPer100, "Fat", 100m, errors, out fat);

            // Allergens: checklist + "other", same names as student allergies
            var allergenNames = new List<string>();
            foreach (var code in (vm.Allergens ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                var option = DietaryProfileService.AllergyOptions.FirstOrDefault(o =>
                    o.Code != DietaryProfileService.OtherCode
                    && string.Equals(o.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

                if (option == null) errors.Add("One of the selected allergens is not recognised.");
                else if (!allergenNames.Contains(option.Code)) allergenNames.Add(option.Code);
            }

            foreach (var other in (vm.OtherAllergens ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var normalised = DietaryProfileService.NormaliseAllergenTag(other);
                if (normalised.Length > 0 && !allergenNames.Contains(normalised, StringComparer.OrdinalIgnoreCase))
                    allergenNames.Add(normalised);
            }

            var allergens = string.Join(", ", allergenNames);
            if (allergens.Length > 200) errors.Add("Allergens must be 200 characters or fewer in total.");

            var tags = new List<string>();
            foreach (var tag in (vm.DietaryTags ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                var option = DietaryProfileService.IngredientTagOptions.FirstOrDefault(o =>
                    string.Equals(o.Code, tag.Trim(), StringComparison.OrdinalIgnoreCase));

                if (option == null) errors.Add("One of the selected dietary tags is not recognised.");
                else if (!tags.Contains(option.Code)) tags.Add(option.Code);
            }

            if (errors.Count > 0) return null;

            var ingredient = new Ingredient
            {
                Name = name,
                Unit = unit,
                CaloriesPerUnit = Math.Round(kcal / 100m, 4),
                ProteinGramsPerUnit = Math.Round(protein / 100m, 4),
                CarbohydrateGramsPerUnit = Math.Round(carbs / 100m, 4),
                FatGramsPerUnit = Math.Round(fat / 100m, 4),
                Allergens = allergenNames.Count > 0 ? allergens : null,
                DietaryTags = tags.Count > 0 ? string.Join(",", tags) : null,
                IsActive = true
            };

            _db.Ingredients.Add(ingredient);
            _db.SaveChanges();

            return ingredient;
        }

        private static void ParseNutrient(string text, string label, decimal max, List<string> errors, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrWhiteSpace(text)) return;   // blank = 0

            if (!TryParseDecimal(text, out value) || value < 0 || value > max)
            {
                errors.Add(string.Format("{0} per 100 must be a number between 0 and {1:0}.", label, max));
                value = 0m;
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private MenuItem LoadMeal(int id)
        {
            return _db.MenuItems
                .Include(m => m.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .FirstOrDefault(m => m.Id == id);
        }

        // Where the meal is already used — shown before editing
        private string BuildUsageSummary(MenuItem item)
        {
            int menus = _db.MenuScheduleItems
                .Where(s => s.MenuItemId == item.Id || s.SubstitutionMenuItemId == item.Id)
                .Select(s => s.MealMenuId)
                .Distinct()
                .Count();

            int planPicks = _db.MealPlanItems.Count(p => p.MenuItemId == item.Id);
            int templates = _db.EventMenuTemplateItems.Count(t => t.MenuItemId == item.Id);

            int sharedWith = item.RecipeId.HasValue
                ? _db.MenuItems.Count(m => m.RecipeId == item.RecipeId && m.Id != item.Id)
                : 0;

            var parts = new List<string>();
            if (menus > 0) parts.Add(menus + " menu(s)");
            if (planPicks > 0) parts.Add(planPicks + " student meal-plan pick(s)");
            if (templates > 0) parts.Add(templates + " event menu template(s)");

            var summary = parts.Count > 0
                ? "Used in " + string.Join(", ", parts) + ". Changes apply wherever the meal is used, including production plans for current menus."
                : null;

            if (sharedWith > 0)
            {
                summary = (summary == null ? "" : summary + " ")
                          + string.Format("Its recipe is shared with {0} other meal(s); ingredient changes affect them too.", sharedWith);
            }

            return summary;
        }
    }
}
