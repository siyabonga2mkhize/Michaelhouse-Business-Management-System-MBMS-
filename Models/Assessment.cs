using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    /// <summary>
    /// A planned assessment (test, exam, assignment) for a specific subject,
    /// grade, and term — created by the teacher assigned to that subject+grade.
    /// </summary>
    public class Assessment
    {
        [Key]
        public int AssessmentId { get; set; }

        // ─── Who owns this assessment ──────────────────────────────────────────────
        [ForeignKey("Teacher")]
        public int TeacherId { get; set; }

        [ForeignKey("Subject")]
        public int SubjectId { get; set; }

        // Grade this assessment is for (8–12)
        [Required]
        public int Grade { get; set; }

        // For Gr 10-12 stream subjects; None = applies to all (compulsory)
        public AcademicStream Stream { get; set; } = AcademicStream.None;

        // ─── Assessment details ────────────────────────────────────────────────────
        [Required, MaxLength(200)]
        public string Title { get; set; }

        /// <summary>Test, Exam, Assignment, Oral, Practical</summary>
        [Required, MaxLength(50)]
        public string AssessmentType { get; set; }

        // Term 1–4
        [Required]
        [Range(1, 4)]
        public int Term { get; set; }

        [Required]
        public int AcademicYear { get; set; }

        // The date the assessment is scheduled to be written
        [Required]
        [DataType(DataType.Date)]
        public DateTime ScheduledDate { get; set; }

        // Maximum mark this assessment is out of
        [Required]
        [Range(1, 1000)]
        public decimal TotalMarks { get; set; }

        /// <summary>
        /// Weighting as a percentage of the TERM mark (all weightings
        /// for a given subject+grade+term must add up to 100).
        /// e.g. Test 1 = 30%, Test 2 = 30%, Exam = 40%
        /// </summary>
        [Required]
        [Range(1, 100)]
        public decimal WeightingPercent { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; }

        // Whether marks have been fully captured by the teacher
        public bool MarksCaptureClosed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ─── Navigation ───────────────────────────────────────────────────────────
        public virtual Teacher Teacher { get; set; }
        public virtual Subject Subject { get; set; }
        public virtual ICollection<StudentMark> StudentMarks { get; set; }
    }
}