using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    // ─── Chef Review ─────────────────────────────────────────
    public class ChefReviewDay
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; }
        public List<ChefReviewMeal> Meals { get; set; } = new List<ChefReviewMeal>();
    }

    public class ChefReviewMeal
    {
        public int MealId { get; set; }
        public string SlotName { get; set; }
        public string Title { get; set; }
        public int ComponentCount { get; set; }
        public int AlternativeCount { get; set; }
        public List<string> Components { get; set; } = new List<string>();
        public List<string> Alternatives { get; set; } = new List<string>();
    }

    // ─── Student Weekly Meal Plan ────────────────────────────
    public class StudentMealPlanDay
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; }
        public List<StudentMealPlanMeal> Meals { get; set; } = new List<StudentMealPlanMeal>();
    }

    public class StudentMealPlanMeal
    {
        public int MenuMealId { get; set; }
        public string SlotName { get; set; }
        public string Title { get; set; }
        public int? SelectedItemId { get; set; }
        public int? RecommendedItemId { get; set; }
        public string RecommendationReason { get; set; }
        public List<StudentMealPlanItemOption> Options { get; set; } = new List<StudentMealPlanItemOption>();
    }

    public class StudentMealPlanItemOption
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public bool IsVegetarian { get; set; }
        public bool IsVegan { get; set; }
        public bool IsGlutenFree { get; set; }
        public bool IsLactoseFree { get; set; }
    }

    // ─── Student Today's Meals ───────────────────────────────
    public class TodayMealCard
    {
        public int MealSlotId { get; set; }
        public string SlotName { get; set; }
        public string Title { get; set; }
        public bool WindowOpen { get; set; }
        public string CutoffLabel { get; set; }
        public string CurrentSelectionName { get; set; }
        public bool IsCollected { get; set; }
        public DateTime? CollectedOn { get; set; }
        public List<StudentMealPlanItemOption> Options { get; set; } = new List<StudentMealPlanItemOption>();
    }

    // ─── Dietitian Meal Plan Review Row ──────────────────────
    public class MealPlanReviewRow
    {
        public Michaelhouse.Models.MealPlan Plan { get; set; }
        public Michaelhouse.Models.Student Student { get; set; }
        public Michaelhouse.Models.StudentProfile Profile { get; set; }
        public List<string> Flags { get; set; }
    }
    // ─── Kitchen Dashboard ───────────────────────────────────
    public class KitchenMealStats
    {
        public int MenuMealId { get; set; }
        public string SlotName { get; set; }
        public string Title { get; set; }
        public int Expected { get; set; }
        public int Selected { get; set; }
        public int Served { get; set; }
        public int Produced { get; set; }
        public int Target { get; set; }
        public int VegetarianCount { get; set; }
        public int VeganCount { get; set; }
        public int AllergySafeCount { get; set; }
        public int MedicalCount { get; set; }
        public bool HasProductionRecord { get; set; }
        public int Remaining
        {
            get { return Math.Max(0, Produced - Served); }
        }
    }
    // ─── Kitchen Prep Plan ───────────────────────────────────
    public class PrepPlanRow
    {
        public int MenuMealId { get; set; }
        public string SlotName { get; set; }
        public string Title { get; set; }
        public int Expected { get; set; }
        public int Selected { get; set; }
        public List<PrepIngredientLine> Ingredients { get; set; } = new List<PrepIngredientLine>();
    }

    public class PrepIngredientLine
    {
        public int InventoryItemId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal AvailableQty { get; set; }
        public bool IsShort
        {
            get { return AvailableQty < RequiredQty; }
        }
        public decimal Shortfall
        {
            get { return IsShort ? RequiredQty - AvailableQty : 0; }
        }
    }
    // ─── QR Meal Collection ──────────────────────────────────
    public class MealQRVerificationResult
    {
        public bool Success { get; set; }
        public bool AlreadyServed { get; set; }
        public bool SafetyBlocked { get; set; }
        public string Message { get; set; }

        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string GradeLevel { get; set; }

        public string MealSlotName { get; set; }
        public string MealTitle { get; set; }
        public string SelectedItemName { get; set; }

        public int? SelectionId { get; set; }
        public int? MenuMealId { get; set; }

        public string SafetyReason { get; set; }
        public DateTime? ServedAt { get; set; }
        public string DiningHallName { get; set; }
    }
    // ─── Reports ─────────────────────────────────────────────
    public class DailyMealReportRow
    {
        public string SlotName { get; set; }
        public int Expected { get; set; }
        public int Selected { get; set; }
        public int Served { get; set; }
        public int NotCollected { get; set; }
    }

    public class DiningHallReportRow
    {
        public string HallName { get; set; }
        public int Served { get; set; }
    }

    public class DietarySummaryRow
    {
        public string Category { get; set; }
        public int Count { get; set; }
    }

    public class WasteSummaryRow
    {
        public string Reason { get; set; }
        public decimal TotalQuantity { get; set; }
        public int Records { get; set; }
    }

    public class ProductionSummaryRow
    {
        public string SlotName { get; set; }
        public int Target { get; set; }
        public int Produced { get; set; }
        public int Served { get; set; }
        public int Leftover { get; set; }
    }
}