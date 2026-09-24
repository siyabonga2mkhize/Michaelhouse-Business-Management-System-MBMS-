using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    // ============================================================
    // STUDENT VIEW — the whole week they're planning
    // ============================================================

    public class MealPlanBuildViewModel
    {
        public MealPlanBuildViewModel()
        {
            Days = new List<MealPlanDayViewModel>();
        }

        public int MealPlanId { get; set; }

        public string StudentName { get; set; }

        public DateTime WeekStartDate { get; set; }

        public DateTime WeekEndDate { get; set; }

        public MealPlanStatus Status { get; set; }

        public string DietitianComment { get; set; }

        // True only when Status is Draft or SentBack
        public bool IsEditable { get; set; }

        // Shown to the student so they understand WHY certain meals are filtered
        public string Allergies { get; set; }
        public string MedicalConditions { get; set; }
        public string Sports { get; set; }

        public List<MealPlanDayViewModel> Days { get; set; }

        // The UC12 menu this plan is built from
        public int SourceMenuId { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    // ============================================================
    // ONE DAY
    // ============================================================

    public class MealPlanDayViewModel
    {
        public MealPlanDayViewModel()
        {
            Slots = new List<MealPlanSlotViewModel>();
        }

        public DateTime Date { get; set; }

        public List<MealPlanSlotViewModel> Slots { get; set; }
    }

    // ============================================================
    // ONE MEAL SLOT — Breakfast / Lunch / Dinner for a specific day
    // ============================================================

    public class MealPlanSlotViewModel
    {
        public MealPlanSlotViewModel()
        {
            Options = new List<MealPlanOptionViewModel>();
        }

        public MealSlot MealSlot { get; set; }

        // What the UC12 menu has scheduled for this slot
        public string DefaultItemName { get; set; }

        // The student's current pick (null if they haven't chosen yet)
        public int? CurrentPickMenuItemId { get; set; }

        // The MealPlanItem.Id that we'll update when the student changes their pick
        public int? CurrentPickMealPlanItemId { get; set; }

        // Match-day context pulled from UC12 — drives option ranking
        public bool IsMatchDay { get; set; }

        public bool IsDayBeforeMatch { get; set; }

        public string MatchDescription { get; set; }

        public List<MealPlanOptionViewModel> Options { get; set; }
    }

    // ============================================================
    // ONE OPTION — a specific dish the student can pick
    // ============================================================

    public class MealPlanOptionViewModel
    {
        public int MenuItemId { get; set; }

        public string Name { get; set; }

        public string DietaryClassification { get; set; }

        public decimal CaloriesPerPortion { get; set; }

        public decimal ProteinGramsPerPortion { get; set; }

        // Marks the UC12 scheduled item — the "chef's choice" of the day
        public bool IsDefault { get; set; }

        // Nutrition category (from UC12) so we can show HighProtein/HighCarb tags
        public NutritionCategory NutritionCategory { get; set; }

        // Human-readable reasons why this is a good option for THIS student
        // e.g. "Matches your Rugby match-day needs", "High protein"
        public List<string> Tags { get; set; }

        public MealPlanOptionViewModel()
        {
            Tags = new List<string>();
        }
    }

    // ============================================================
    // STUDENT SUBMIT ACTION — posted data
    // ============================================================

    public class MealPlanSaveRequest
    {
        public int MealPlanId { get; set; }

        // Dictionary keyed by slot key "yyyy-MM-dd|Lunch" → MenuItemId
        public Dictionary<string, int> Picks { get; set; }

        public MealPlanSaveRequest()
        {
            Picks = new Dictionary<string, int>();
        }
    }
}