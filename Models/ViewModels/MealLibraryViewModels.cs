using Michaelhouse.Models.Cafeteria;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.ViewModels
{
    // ============================================================
    // Dietitian meal library ("Create Meal").
    //
    // A meal is an existing MenuItem plus its Recipe and
    // RecipeIngredient rows. Nutrition, allergens and halal status
    // are derived from the ingredients by MealLibraryService.
    // ============================================================

    // ── LIST ─────────────────────────────────────────────────────

    public class MealListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DietaryClassification { get; set; }
        public bool IsHalal { get; set; }
        public NutritionCategory NutritionCategory { get; set; }
        public string MealSlots { get; set; }
        public decimal CaloriesPerPortion { get; set; }
        public decimal ProteinGramsPerPortion { get; set; }
        public string Allergens { get; set; }
        public int IngredientCount { get; set; }
        public bool IsActive { get; set; }
    }

    // ── CREATE / EDIT FORM ───────────────────────────────────────

    public class MealFormViewModel
    {
        public MealFormViewModel()
        {
            DietaryClassification = "Standard";
            NutritionCategory = NutritionCategory.Standard;
            PortionsPerBatch = 1;
            Ingredients = new List<MealIngredientLineViewModel>();
        }

        // Null when creating
        public int? Id { get; set; }

        // 1. Basic information
        [Display(Name = "Meal name")]
        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsBreakfastItem { get; set; }
        public bool IsLunchItem { get; set; }
        public bool IsDinnerItem { get; set; }

        public bool IsFarmGrownProduce { get; set; }

        // 2. Dietary information — Standard / Vegetarian / Vegan
        public string DietaryClassification { get; set; }

        // 3. Nutrition — classification only; values are calculated
        public NutritionCategory NutritionCategory { get; set; }

        // 4. Ingredients — quantities are for the whole batch
        [Display(Name = "Portions this recipe makes")]
        public int PortionsPerBatch { get; set; }

        public List<MealIngredientLineViewModel> Ingredients { get; set; }

        // 5. Preparation (stored on Recipe)
        [Display(Name = "Recipe / instructions")]
        public string Instructions { get; set; }

        public int PrepTimeMinutes { get; set; }
        public int CookTimeMinutes { get; set; }
        public string Station { get; set; }

        // Display only (edit): where the meal is already used
        public string UsageWarning { get; set; }
    }

    public class MealIngredientLineViewModel
    {
        public int IngredientId { get; set; }

        // Amount for the whole batch, in Unit. Text so that "0.5" and
        // "0,5" both parse regardless of the server culture (en-ZA uses
        // a comma); MealLibraryService.TryParseDecimal reads it.
        public string Quantity { get; set; }

        // "g", "kg", "ml" or "l" — converted to the ingredient's own unit
        public string Unit { get; set; }

        public string Notes { get; set; }
    }

    // ── INGREDIENT PICKER DATA (sent to the page as JSON) ────────

    public class IngredientOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal CaloriesPerUnit { get; set; }
        public decimal ProteinGramsPerUnit { get; set; }
        public decimal CarbohydrateGramsPerUnit { get; set; }
        public decimal FatGramsPerUnit { get; set; }
        public List<string> Allergens { get; set; }
        public bool IsNonVegetarian { get; set; }
        public bool IsNonVegan { get; set; }
        public string HalalProblem { get; set; }
    }

    // ── NEW INGREDIENT (posted from the Create Meal page) ────────

    public class NewIngredientViewModel
    {
        public NewIngredientViewModel()
        {
            Allergens = new List<string>();
            DietaryTags = new List<string>();
        }

        public string Name { get; set; }

        // "g" or "ml"
        public string Unit { get; set; }

        // Entered per 100 g / 100 ml, stored per unit. Text for the same
        // culture reason as MealIngredientLineViewModel.Quantity.
        public string CaloriesPer100 { get; set; }
        public string ProteinPer100 { get; set; }
        public string CarbohydratePer100 { get; set; }
        public string FatPer100 { get; set; }

        public List<string> Allergens { get; set; }
        public string OtherAllergens { get; set; }

        public List<string> DietaryTags { get; set; }
    }

    // ── DETAILS ──────────────────────────────────────────────────

    public class MealDetailsViewModel
    {
        public MealDetailsViewModel()
        {
            Allergens = new List<string>();
            Ingredients = new List<MealDetailsIngredientViewModel>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string MealSlots { get; set; }
        public bool IsFarmGrownProduce { get; set; }
        public bool IsActive { get; set; }

        public string DietaryClassification { get; set; }
        public string StrictestAllowedClassification { get; set; }
        public bool IsHalal { get; set; }
        public string HalalProblem { get; set; }
        public List<string> Allergens { get; set; }

        public NutritionCategory NutritionCategory { get; set; }
        public NutritionCategory SuggestedNutritionCategory { get; set; }
        public string NutritionCategoryWarning { get; set; }

        public decimal CaloriesPerPortion { get; set; }
        public decimal ProteinGramsPerPortion { get; set; }
        public decimal CarbohydrateGramsPerPortion { get; set; }
        public decimal FatGramsPerPortion { get; set; }

        public int PortionsPerBatch { get; set; }
        public string Instructions { get; set; }
        public int PrepTimeMinutes { get; set; }
        public int CookTimeMinutes { get; set; }
        public string Station { get; set; }

        public List<MealDetailsIngredientViewModel> Ingredients { get; set; }

        public string UsageSummary { get; set; }
    }

    public class MealDetailsIngredientViewModel
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal QuantityPerPortion { get; set; }
        public decimal QuantityPerBatch { get; set; }
        public string Allergens { get; set; }
        public string Notes { get; set; }
    }
}
