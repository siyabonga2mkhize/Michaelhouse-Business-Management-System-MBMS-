using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// End-of-year result for a student — aggregates all 4 term marks
    /// and records the promotion / retention decision.
    /// </summary>
    public class YearResult
    {
        [Key]
        public int YearResultId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [Required]
        public int Grade { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        // Average across all subjects (weighted average of all 4 terms)
        public decimal OverallPercent { get; set; }

        /// <summary>
        /// Promoted = moves to next grade.
        /// Retained = repeats the same grade.
        /// Pending = not yet decided (year not complete).
        /// </summary>
        [Required, MaxLength(20)]
        public string PromotionStatus { get; set; } = "Pending";

        // Admin notes on the promotion / retention decision
        [MaxLength(500)]
        public string AdminNotes { get; set; }

        public DateTime CalculatedAt { get; set; } = DateTime.Now;

        // ─── Navigation ───────────────────────────────────────────────────────────
        public virtual Student Student { get; set; }
    }
}