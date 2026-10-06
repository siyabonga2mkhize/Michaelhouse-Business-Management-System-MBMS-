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
            Weeks = new List<MealPlanWeekOption>();
        }

        public int MealPlanId { get; set; }

        public string StudentName { get; set; }

        public DateTime WeekStartDate { get; set; }

        public DateTime WeekEndDate { get; set; }

        public MealPlanStatus Status { get; set; }

        public string DietitianComment { get; set; }

        // True while at least one meal can still be chosen / changed
        // (selection closes the day before each meal)
        public bool IsEditable { get; set; }

        // True when the plan can be submitted (Draft or SentBack)
        public bool CanSubmit { get; set; }

        // Meals still open for selection that have a suitable option,
        // and how many of those the student has chosen
        public int OpenSlotCount { get; set; }
        public int OpenSlotsChosen { get; set; }

        // School time used for the selection deadline
        public DateTime Now { get; set; }

        // Shown to the student so they understand WHY certain meals are filtered
        public string Allergies { get; set; }
        public string MedicalConditions { get; set; }
        public string Sports { get; set; }

        // From the student's dietary profile ("" when not set)
        public string DietaryPreference { get; set; }
        public string MedicalDietaryRestrictions { get; set; }

        public List<MealPlanDayViewModel> Days { get; set; }

        // The UC12 menu this plan is built from
        public int SourceMenuId { get; set; }

        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        // Published weeks the student can switch between (this week,
        // and any menu accepted ahead of time)
        public List<MealPlanWeekOption> Weeks { get; set; }
    }

    // ============================================================
    // ONE PUBLISHED WEEK (week tabs)
    // ============================================================

    public class MealPlanWeekOption
    {
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public bool IsCurrentWeek { get; set; }
        public bool IsSelected { get; set; }

        // The student's plan for that week, if one has been started
        public MealPlanStatus? Status { get; set; }
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

        // The student's sport has training that day
        public bool IsTrainingDay { get; set; }

        // e.g. "No high-carb option on this menu suits your dietary
        // profile — choose the closest match."
        public string SportsNote { get; set; }

        // Selection closes at 00:00 the day before the meal
        public DateTime SelectionCutoff { get; set; }
        public bool IsLocked { get; set; }

        // Name of the chosen meal (also shown when the slot is locked)
        public string CurrentPickName { get; set; }

        // True when nothing on the menu fits the student's dietary
        // profile for this slot — the Dietitian resolves it.
        public bool HasNoSuitableOption { get; set; }

        // True when the saved pick conflicts with the (updated)
        // dietary profile and must be changed.
        public bool CurrentPickNoLongerSuitable { get; set; }

        public List<MealPlanOptionViewModel> Options { get; set; }
    }

    // ============================================================
    // ONE OPTION — a specific dish the student can pick
    // ============================================================

    public class MealPlanOptionViewModel
    {
        public int MenuItemId { get; set; }

        public string Name { get; set; }

        // False when the meal conflicts with the student's dietary
        // profile: it is shown, greyed out, but can't be chosen.
        public bool IsAvailable { get; set; }

        // Why it can't be chosen, e.g. "Peanuts allergy — contains Peanut Butter"
        public string UnavailableReason { get; set; }

        public string DietaryClassification { get; set; }

        public decimal CaloriesPerPortion { get; set; }

        public decimal ProteinGramsPerPortion { get; set; }

        public decimal CarbohydrateGramsPerPortion { get; set; }

        public decimal FatGramsPerPortion { get; set; }

        // Marks the UC12 scheduled item — the "chef's choice" of the day
        public bool IsDefault { get; set; }

        // Nutrition category (from UC12) so we can show HighProtein/HighCarb tags
        public NutritionCategory NutritionCategory { get; set; }

        // Human-readable reasons why this is a good option for THIS student
        // e.g. "Matches your Rugby match-day needs", "High protein"
        public List<string> Tags { get; set; }

        // "Recommended for your Rugby match" — set on safe options in
        // the category the student's day calls for; null otherwise
        public string RecommendationLabel { get; set; }

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