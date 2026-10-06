using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    // ============================================================
    // Matches the JSON shape returned by:
    //   GET /api/mealplan/mine   (logged-in student)
    //
    // Same data as the web page Views/StudentMealPlan/Index.cshtml.
    // Field names must match the JSON keys (case doesn't matter).
    // ============================================================

    public class MealPlanDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";

        public int MealPlanId { get; set; }
        public string StudentName { get; set; } = "";
        public string WeekStart { get; set; } = "";
        public string WeekEnd { get; set; } = "";
        public string WeekLabel { get; set; } = "";

        public string Status { get; set; } = "";
        public string StatusText { get; set; } = "";
        public bool IsSubmitted { get; set; }
        public string? SubmittedAt { get; set; }
        public string? DietitianComment { get; set; }

        public bool IsEditable { get; set; }
        public bool CanSubmit { get; set; }
        public int OpenSlotCount { get; set; }
        public int OpenSlotsChosen { get; set; }
        public int LockedSlotCount { get; set; }

        public string? Allergies { get; set; }
        public string? DietaryPreference { get; set; }
        public string? MedicalDietaryRestrictions { get; set; }
        public string? MedicalConditions { get; set; }
        public string? Sports { get; set; }

        public List<MealPlanDayDto> Days { get; set; } = new List<MealPlanDayDto>();
    }

    public class MealPlanDayDto
    {
        public string Date { get; set; } = "";          // yyyy-MM-dd
        public string DayLabel { get; set; } = "";      // Monday
        public string DateLabel { get; set; } = "";     // 22 September
        public string ShortLabel { get; set; } = "";    // Mon 22 Sep
        public List<MealPlanSlotDto> Slots { get; set; } = new List<MealPlanSlotDto>();
    }

    public class MealPlanSlotDto
    {
        public string MealSlot { get; set; } = "";      // Breakfast / Lunch / Dinner
        public bool IsLocked { get; set; }
        public string ChooseByLabel { get; set; } = "";

        public int? CurrentPickMenuItemId { get; set; }
        public string? CurrentPickName { get; set; }
        public bool CurrentPickNoLongerSuitable { get; set; }
        public bool HasNoSuitableOption { get; set; }

        public string? SportsNote { get; set; }
        public bool IsMatchDay { get; set; }
        public bool IsDayBeforeMatch { get; set; }
        public bool IsTrainingDay { get; set; }
        public string? MatchDescription { get; set; }

        public List<MealPlanOptionDto> Options { get; set; } = new List<MealPlanOptionDto>();

        // Counts as chosen the same way the web page does
        public bool HasPick => CurrentPickMenuItemId.HasValue || (IsLocked && CurrentPickName != null);
    }

    public class MealPlanOptionDto
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; } = "";
        public bool IsAvailable { get; set; }
        public string? UnavailableReason { get; set; }
        public string? DietaryClassification { get; set; }
        public decimal Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbohydrate { get; set; }
        public decimal Fat { get; set; }
        public bool IsDefault { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        // "Recommended for your Rugby match" — safe options that suit
        // the day's training / match (web: blue label on the option)
        public string? Recommendation { get; set; }
    }

    // POST /api/mealplan/mine/pick
    public class MealPickRequestDto
    {
        public int MealPlanId { get; set; }
        public string Date { get; set; } = "";
        public string MealSlot { get; set; } = "";
        public int MenuItemId { get; set; }
    }

    // POST /api/mealplan/mine/submit
    public class MealPlanSubmitRequestDto
    {
        public int MealPlanId { get; set; }
    }

    // Reply to pick / submit
    public class ApiResultDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }
}
