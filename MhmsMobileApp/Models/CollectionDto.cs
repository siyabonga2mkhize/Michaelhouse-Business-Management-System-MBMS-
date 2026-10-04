using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    // ============================================================
    // UC16 — Face meal collection
    // Matches the JSON from the web app's MealCollectionApiController:
    //   GET  /api/mealcollection/status
    //   POST /api/mealcollection/verify  { imageBase64 }
    //   POST /api/mealcollection/record  { token }
    // ============================================================

    public class CollectionStatusDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
        public string? CurrentSlot { get; set; }         // null: no meal being served
        public string? CurrentSlotHours { get; set; }
        public bool IsManualOpening { get; set; }
        public string CollectionHours { get; set; } = "";
        public int TodayCount { get; set; }
    }

    public class CollectionVerifyRequestDto
    {
        public string ImageBase64 { get; set; } = "";
    }

    public class CollectionRecordRequestDto
    {
        public string Token { get; set; } = "";
    }

    // Reply to both verify and record (same shape as the web terminal's)
    public class CollectionResultDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }

        // ReadyForCollection, Collected, FaceNotRecognised, InvalidPhoto,
        // NoMealBeingServed, MealPlanNotFound, NoMealSelected,
        // AlreadyCollected, InvalidMeal, VerificationExpired
        public string Outcome { get; set; } = "";
        public string? Title { get; set; }
        public string? Message { get; set; }

        public string? StudentName { get; set; }
        public string? StudentNumber { get; set; }
        public string? MealName { get; set; }
        public string? MealSlot { get; set; }
        public string? MealDate { get; set; }
        public string? CollectedAt { get; set; }

        // One-time code to confirm the collection (ReadyForCollection only)
        public string? Token { get; set; }

        public string? Allergens { get; set; }
        public string? StudentAllergies { get; set; }
        public string? DietaryPreference { get; set; }
        public string? MedicalDietaryRestrictions { get; set; }
        public string? DietaryNotes { get; set; }
        public bool HasDietaryConflict { get; set; }
        public List<string> DietaryConflicts { get; set; } = new List<string>();

        public bool IsReady => Outcome == "ReadyForCollection";
        public bool IsCollected => Outcome == "Collected";
    }
}
