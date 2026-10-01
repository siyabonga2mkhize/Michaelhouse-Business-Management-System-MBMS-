using System;
using System.Collections.Generic;
using System.Globalization;

namespace Michaelhouse.Models.ViewModels
{
    // ============================================================
    // Kitchen plan (UC15) — built from students' SUBMITTED meal
    // plans: portions per meal are the number of students who chose
    // it, ingredients come from the recipe, and each requirement is
    // checked against the ingredient's current stock.
    // See MenuSchedulingService.BuildProductionPlan.
    // ============================================================

    public class ProductionPlanViewModel
    {
        public ProductionPlanViewModel()
        {
            Days = new List<ProductionDayViewModel>();
            WeekTotalIngredients = new List<IngredientLineViewModel>();
            Warnings = new List<KitchenWarning>();
        }

        public int MenuId { get; set; }

        public DateTime WeekStart { get; set; }

        public DateTime WeekEnd { get; set; }

        public bool IsProductionConfirmed { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        // Total portions to prepare across the week (students + staff)
        public int TotalMeals { get; set; }

        // Students can only choose from a published (accepted) menu
        public bool IsMenuPublished { get; set; }

        // Whose selections are counted
        public int SubmittedPlanCount { get; set; }
        public int StudentCount { get; set; }

        // Students who picked meals but haven't submitted — not counted
        public int UnsubmittedPlansWithPicks { get; set; }

        // Staff meals per meal from the menu; added to each meal's
        // most chosen dish
        public int StaffMeals { get; set; }

        public List<ProductionDayViewModel> Days { get; set; }

        public List<IngredientLineViewModel> WeekTotalIngredients { get; set; }

        // Ingredient problems that may stop meals being prepared
        public List<KitchenWarning> Warnings { get; set; }

        public bool HasIngredientProblems
        {
            get { return Warnings.Exists(w => w.Severity != KitchenWarningSeverity.Info); }
        }
    }

    public class ProductionDayViewModel
    {
        public ProductionDayViewModel()
        {
            Slots = new List<ProductionSlotViewModel>();
            DayTotalIngredients = new List<IngredientLineViewModel>();
        }

        public DateTime Date { get; set; }

        // Students can change this day's meals until this moment
        // (MealPlanService.SelectionCutoff). Until then the numbers can move.
        public DateTime SelectionCutoff { get; set; }
        public bool SelectionsFinal { get; set; }

        // Ingredients for this day have been issued from stock
        // (KitchenIssueService) — stock already went down for it
        public bool IsIssued { get; set; }
        public DateTime? IssuedAt { get; set; }
        public string IssuedBy { get; set; }

        public List<ProductionSlotViewModel> Slots { get; set; }

        public List<IngredientLineViewModel> DayTotalIngredients { get; set; }
    }

    public class ProductionSlotViewModel
    {
        public ProductionSlotViewModel()
        {
            Tasks = new List<ProductionTaskViewModel>();
            Ingredients = new List<IngredientLineViewModel>();
            NotSelectedOptions = new List<string>();
        }

        public string MealSlot { get; set; }

        public TimeSpan ServeTime { get; set; }

        // Portions for this meal across all dishes (students + staff)
        public int Portions { get; set; }

        // One task per dish students chose, in start-time order
        public List<ProductionTaskViewModel> Tasks { get; set; }

        // Ingredient totals for the whole meal, with availability
        public List<IngredientLineViewModel> Ingredients { get; set; }

        // Menu options nobody chose — nothing to prepare
        public List<string> NotSelectedOptions { get; set; }

        // Students with a submitted plan but no meal chosen here
        public int StudentsWithoutPick { get; set; }
    }

    public class ProductionTaskViewModel
    {
        public ProductionTaskViewModel()
        {
            Ingredients = new List<IngredientLineViewModel>();
        }

        public int MenuItemId { get; set; }

        public string DishName { get; set; }

        public string Station { get; set; }

        // StudentPortions + StaffPortions — what the kitchen prepares
        public int Portions { get; set; }

        // Students who chose this dish
        public int StudentPortions { get; set; }

        // The menu's staff meals, given to the meal's most chosen dish
        public int StaffPortions { get; set; }

        public int PrepMinutes { get; set; }

        public int CookMinutes { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan ReadyTime { get; set; }

        // Recipe.PreparationNotes
        public string Instructions { get; set; }

        // False when the meal has no recipe ingredients on file, so
        // its ingredients can't be calculated or checked
        public bool HasRecipe { get; set; }

        public List<IngredientLineViewModel> Ingredients { get; set; }
    }

    public enum IngredientAvailability
    {
        Available = 1,
        Insufficient = 2,
        NotAvailable = 3
    }

    public class IngredientLineViewModel
    {
        public IngredientLineViewModel()
        {
            AffectedMeals = new List<string>();
        }

        public int IngredientId { get; set; }

        public string IngredientName { get; set; }

        public string Unit { get; set; }

        public decimal QuantityPerPortion { get; set; }

        public decimal TotalRequired { get; set; }

        // Current stock: Ingredient farm + external quantity
        // (0 when the ingredient is inactive)
        public decimal StockAvailable { get; set; }

        public decimal Shortfall { get; set; }

        // Meals that need this ingredient
        public List<string> AffectedMeals { get; set; }

        public bool IsCovered
        {
            get { return Shortfall <= 0m; }
        }

        public IngredientAvailability Status
        {
            get
            {
                if (StockAvailable <= 0m) return IngredientAvailability.NotAvailable;
                if (StockAvailable < TotalRequired) return IngredientAvailability.Insufficient;
                return IngredientAvailability.Available;
            }
        }
    }

    public enum KitchenWarningSeverity
    {
        Info = 1,
        Insufficient = 2,
        NotAvailable = 3
    }

    public class KitchenWarning
    {
        public KitchenWarning()
        {
            AffectedMeals = new List<string>();
        }

        // e.g. "Wed 07 Oct Lunch" or "Whole week"
        public string When { get; set; }

        public KitchenWarningSeverity Severity { get; set; }

        // e.g. "Insufficient Rice. 5 kg required, 3 kg available."
        public string Message { get; set; }

        public List<string> AffectedMeals { get; set; }
    }

    // Shows recipe quantities the way a kitchen reads them:
    // 2500 g → "2.5 kg", 750 ml → "750 ml".
    public static class KitchenQuantity
    {
        public static string Format(decimal quantity, string unit)
        {
            var u = (unit ?? "").Trim();

            if (string.Equals(u, "g", StringComparison.OrdinalIgnoreCase) && Math.Abs(quantity) >= 1000m)
                return Number(quantity / 1000m) + " kg";

            if (string.Equals(u, "ml", StringComparison.OrdinalIgnoreCase) && Math.Abs(quantity) >= 1000m)
                return Number(quantity / 1000m) + " l";

            return Number(quantity) + " " + u;
        }

        private static string Number(decimal value)
        {
            return value.ToString("#,##0.##", CultureInfo.CurrentCulture);
        }
    }
}
