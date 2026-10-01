using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // Students submit their plan and it goes straight to the kitchen
    // (Submitted). There is no Dietitian review of student plans any
    // more — the Dietitian approves the meals themselves. The review
    // statuses remain for plans created before that change; plans in
    // SubmittedToDietitian or Approved also count as submitted.
    public enum MealPlanStatus
    {
        Draft = 1,
        SubmittedToDietitian = 2,
        Approved = 3,
        SentBack = 4,
        Submitted = 5
    }

    // ============================================================
    // A student's personal weekly meal plan.
    // One per student per week. Contains 21 MealPlanItem rows —
    // one pick for each (day, meal slot) combination.
    // ============================================================

    public class MealPlan
    {
        public MealPlan()
        {
            Items = new HashSet<MealPlanItem>();
            CreatedAt = DateTime.UtcNow;
            Status = MealPlanStatus.Draft;
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [Required]
        public DateTime WeekStartDate { get; set; }

        [Required]
        public DateTime WeekEndDate { get; set; }

        [Required]
        public MealPlanStatus Status { get; set; }

        // AppUser.UserId of the Dietitian who reviewed this plan
        public int? DietitianUserId { get; set; }

        [StringLength(2000)]
        public string DietitianComment { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // ---------------------------------------------------------
        // Navigation
        // ---------------------------------------------------------

        public virtual Student Student { get; set; }

        public virtual ICollection<MealPlanItem> Items { get; set; }
    }

    // ============================================================
    // One meal in a student's plan.
    // The student picks one MenuItem for a specific date + slot.
    // ============================================================

    public class MealPlanItem
    {
        public MealPlanItem()
        {
            CreatedAt = DateTime.UtcNow;
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("MealPlan")]
        public int MealPlanId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public MealSlot MealSlot { get; set; }

        // The dish the student picked
        [Required]
        [ForeignKey("MenuItem")]
        public int MenuItemId { get; set; }

        // If the student accepted the UC12 default, this points to the
        // scheduled item. If they chose an alternative, it's null.
        public int? MenuScheduleItemId { get; set; }

        public DateTime CreatedAt { get; set; }

        // ---------------------------------------------------------
        // Navigation
        // ---------------------------------------------------------

        public virtual MealPlan MealPlan { get; set; }

        public virtual MenuItem MenuItem { get; set; }

        public virtual MenuScheduleItem MenuScheduleItem { get; set; }
    }
}