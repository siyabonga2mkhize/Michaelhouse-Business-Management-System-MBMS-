using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    /// <summary>
    /// The final calculated term mark for a student in a subject.
    /// Computed from all StudentMarks for that subject+term using assessment weightings.
    /// This is what appears on the report card.
    /// </summary>
    public class TermResult
    {
        [Key]
        public int TermResultId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [ForeignKey("Subject")]
        public int SubjectId { get; set; }

        [Required]
        public int Grade { get; set; }

        [Required]
        [Range(1, 4)]
        public int Term { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        // Weighted average mark as a percentage (0–100)
        public decimal TermMarkPercent { get; set; }

        // Symbol: A (80–100), B (70–79), C (60–69), D (50–59), E (40–49), F (0–39)
        [MaxLength(2)]
        public string Symbol { get; set; }

        // True = passed this subject for the term (≥ 40%)
        public bool Passed { get; set; }

        // When this was last recalculated
        public DateTime CalculatedAt { get; set; } = DateTime.Now;

        // ─── Navigation ───────────────────────────────────────────────────────────
        public virtual Student Student { get; set; }
        public virtual Subject Subject { get; set; }
    }
}