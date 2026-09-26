using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    // ─── DIETARY ─────────────────────────────────────────────────────────
    public class StudentDietaryRecord
    {
        [Key] public int Id { get; set; }
        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public virtual Student Student { get; set; }

        public DietaryRecordType RecordType { get; set; }

        public int? AllergenId { get; set; }
        [ForeignKey("AllergenId")] public virtual Allergen Allergen { get; set; }

        public int? DietaryCategoryId { get; set; }
        [ForeignKey("DietaryCategoryId")] public virtual DietaryCategory DietaryCategory { get; set; }

        [StringLength(300)] public string CustomDescription { get; set; }
        public DietaryRequestStatus Status { get; set; } = DietaryRequestStatus.Pending;
        public bool IsActive { get; set; } = true;
        public bool RequestedByStudent { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        [StringLength(500)] public string ReviewNotes { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    public class DietaryChangeRequest
    {
        [Key] public int Id { get; set; }
        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public virtual Student Student { get; set; }

        public DietaryRecordType RecordType { get; set; }
        public DietaryChangeType ChangeType { get; set; } = DietaryChangeType.Add;

        public int? RequestedAllergenId { get; set; }
        public int? RequestedDietaryCategoryId { get; set; }
        [StringLength(300)] public string RequestedDescription { get; set; }
        [StringLength(500)] public string Reason { get; set; }

        public DietaryRequestStatus Status { get; set; } = DietaryRequestStatus.Pending;
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        [StringLength(500)] public string ReviewNotes { get; set; }
        public bool SafetyReviewRequired { get; set; }
    }

    // ─── MEAL PLAN ──────────────────────────────────────────────────────
    public class MealPlan
    {
        [Key] public int Id { get; set; }

        // ─── Backward-compat aliases for OLD views & controllers ──────
        [NotMapped]
        public int MealPlanID
        {
            get { return Id; }
            set { Id = value; }
        }

        [NotMapped]
        public int StudentID
        {
            get { return StudentId; }
            set { StudentId = value; }
        }

        [NotMapped]
        public DateTime CreatedDate
        {
            get { return GeneratedAt; }
            set { GeneratedAt = value; }
        }

        [NotMapped]
        public DateTime? ApprovedDate
        {
            get { return ConfirmedAt; }
            set { ConfirmedAt = value; }
        }

        [NotMapped] public int? ApprovedBy { get; set; }
        [NotMapped] public string DietitianComments { get; set; }
        [NotMapped] public bool UsedAIRecommendations { get; set; }

        // ─── Real columns ─────────────────────────────────────────────
        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public virtual Student Student { get; set; }

        public int WeeklyMenuId { get; set; }
        [ForeignKey("WeeklyMenuId")] public virtual WeeklyMenu WeeklyMenu { get; set; }

        [StringLength(40)]
        public string Status { get; set; } = "Pending";

        [NotMapped]
        public MealPlanStatus StatusEnum
        {
            get
            {
                if (string.IsNullOrEmpty(Status)) return MealPlanStatus.Proposed;
                switch (Status.ToLower())
                {
                    case "approved":
                    case "confirmed": return MealPlanStatus.Confirmed;
                    case "rejected":
                    case "superseded": return MealPlanStatus.Superseded;
                    case "needsreview": return MealPlanStatus.NeedsReview;
                    default: return MealPlanStatus.Proposed;
                }
            }
            set
            {
                switch (value)
                {
                    case MealPlanStatus.Confirmed: Status = "Approved"; break;
                    case MealPlanStatus.Superseded: Status = "Rejected"; break;
                    case MealPlanStatus.NeedsReview: Status = "NeedsReview"; break;
                    default: Status = "Pending"; break;
                }
            }
        }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public DateTime? ConfirmedAt { get; set; }

        public virtual ICollection<MealPlanItem> Items { get; set; } = new List<MealPlanItem>();
    }

    public class MealPlanItem
    {
        [Key] public int Id { get; set; }
        public int MealPlanId { get; set; }
        [ForeignKey("MealPlanId")] public virtual MealPlan MealPlan { get; set; }
        public int MenuMealId { get; set; }
        [ForeignKey("MenuMealId")] public virtual MenuMeal MenuMeal { get; set; }
        public int? SelectedMenuItemId { get; set; }
        public int? AlternativeMenuItemId { get; set; }
        public MealPlanItemStatus Status { get; set; } = MealPlanItemStatus.Recommended;
        [StringLength(300)] public string Reason { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public bool ModifiedByStudent { get; set; }
    }

    // ─── DAILY SELECTION ────────────────────────────────────────────────
    public class MealSelection
    {
        [Key] public int Id { get; set; }
        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public virtual Student Student { get; set; }
        public int MenuMealId { get; set; }
        [ForeignKey("MenuMealId")] public virtual MenuMeal MenuMeal { get; set; }
        public int MenuItemId { get; set; }
        [ForeignKey("MenuItemId")] public virtual MenuItem MenuItem { get; set; }
        public int? MealPlanItemId { get; set; }
        public MealSelectionStatus Status { get; set; } = MealSelectionStatus.Selected;
        public DateTime SelectedAt { get; set; } = DateTime.Now;
        public bool IsFallback { get; set; }
        public bool IsLateException { get; set; }
        public int? ExceptionAuthorizedByUserId { get; set; }
        [StringLength(500)] public string ExceptionReason { get; set; }
    }

    // ─── MEAL SERVICE ───────────────────────────────────────────────────
    public class MealService
    {
        [Key] public int Id { get; set; }
        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public virtual Student Student { get; set; }
        public int MenuMealId { get; set; }
        [ForeignKey("MenuMealId")] public virtual MenuMeal MenuMeal { get; set; }
        public int? MealSelectionId { get; set; }
        public int DiningHallId { get; set; }
        [ForeignKey("DiningHallId")] public virtual DiningHall DiningHall { get; set; }

        public MealServiceMethod Method { get; set; } = MealServiceMethod.QR;
        public MealSelectionStatus Status { get; set; } = MealSelectionStatus.Served;
        public DateTime ServedAt { get; set; } = DateTime.Now;
        public int? ServedByUserId { get; set; }
        public bool WasLateException { get; set; }
        public bool WasOverride { get; set; }
        [StringLength(500)] public string OverrideReason { get; set; }
        [StringLength(300)] public string Notes { get; set; }
    }

    // ─── PRODUCTION & WASTE ─────────────────────────────────────────────
    public class ProductionRecord
    {
        [Key] public int Id { get; set; }
        public int MenuMealId { get; set; }
        [ForeignKey("MenuMealId")] public virtual MenuMeal MenuMeal { get; set; }
        public int? DiningHallId { get; set; }
        public DateTime ProductionDate { get; set; }
        public int TargetQuantity { get; set; }
        public int ProducedQuantity { get; set; }
        public int? SpecialDietCount { get; set; }
        public int? VegetarianCount { get; set; }
        public int? VeganCount { get; set; }
        public int? AllergySafeCount { get; set; }
        [StringLength(500)] public string Notes { get; set; }
        public int? RecordedByUserId { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.Now;
    }

    public class WasteRecord
    {
        [Key] public int Id { get; set; }
        public int? MenuMealId { get; set; }
        public int? MenuItemId { get; set; }
        public int? InventoryItemId { get; set; }
        public int? DiningHallId { get; set; }
        public DateTime RecordedDate { get; set; } = DateTime.Now;
        public decimal Quantity { get; set; }
        [StringLength(20)] public string Unit { get; set; }
        public WasteReason Reason { get; set; }
        [StringLength(500)] public string Notes { get; set; }
        public int? RecordedByUserId { get; set; }
    }

    // ─── AUDIT & SETTINGS ───────────────────────────────────────────────
    public class CafeteriaAuditLog
    {
        [Key] public int Id { get; set; }
        public int? UserId { get; set; }
        [StringLength(150)] public string UserName { get; set; }
        [Required, StringLength(120)] public string Action { get; set; }
        [StringLength(120)] public string EntityName { get; set; }
        public int? EntityId { get; set; }
        [StringLength(2000)] public string PreviousValue { get; set; }
        [StringLength(2000)] public string NewValue { get; set; }
        [StringLength(500)] public string Reason { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        [StringLength(60)] public string IPAddress { get; set; }
    }

    public class CafeteriaSetting
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(80)] public string Key { get; set; }
        [StringLength(500)] public string Value { get; set; }
        [StringLength(300)] public string Description { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    // ─── RULES ──────────────────────────────────────────────────────────
    public class SportsMealRecommendationRule
    {
        [Key] public int Id { get; set; }
        public SportsActivityType ActivityType { get; set; }
        public int? MealSlotId { get; set; }
        [ForeignKey("MealSlotId")] public virtual MealSlot MealSlot { get; set; }
        [Required, StringLength(150)] public string RecommendedMealType { get; set; }
        [StringLength(300)] public string Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}