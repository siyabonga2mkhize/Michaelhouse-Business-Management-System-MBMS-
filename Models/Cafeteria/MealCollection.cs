using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    public enum CollectionMethod
    {
        FaceMatch = 1,
        ManualOverride = 2
    }

    // ============================================================
    // UC16 — One row per meal collected. Anti-fraud: only one
    // collection per (StudentId, Date, MealSlot).
    // ============================================================

    public class MealCollection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Student")]
        public int StudentId { get; set; }

        // The UC13 meal plan item that was collected
        [ForeignKey("MealPlanItem")]
        public int? MealPlanItemId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public MealSlot MealSlot { get; set; }

        [Required]
        public DateTime CollectedAt { get; set; }

        [Required]
        public CollectionMethod VerifiedByMethod { get; set; }

        /// <summary>
        /// Match confidence in the range 0.00 to 1.00.
        /// 1.00 = perfect match, 0.00 = no confidence.
        /// Null when verified by manual override.
        /// </summary>
        [Range(0.0, 1.0)]
        public decimal? MatchConfidence { get; set; }

        [ForeignKey("CollectedByUser")]
        public int? CollectedByUserId { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public virtual Student Student { get; set; }

        public virtual MealPlanItem MealPlanItem { get; set; }

        public virtual AppUser CollectedByUser { get; set; }

        public MealCollection()
        {
            CollectedAt = DateTime.UtcNow;
            Date = DateTime.Today;
        }
    }
}