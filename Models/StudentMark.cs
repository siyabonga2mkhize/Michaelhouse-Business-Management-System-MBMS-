using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Stores the mark a specific student received for a specific assessment.
    /// Created by the teacher when capturing results after the assessment is written.
    /// </summary>
    public class StudentMark
    {
        [Key]
        public int StudentMarkId { get; set; }

        [ForeignKey("Assessment")]
        [Display(Name = "Assessment")]
        public int AssessmentmentId { get; set; }

        [ForeignKey("Student")]
        [Display(Name = "Student")]
        public int StudentId { get; set; }

        // ─── The captured mark ─────────────────────────────────────────────────────

        // Null = not yet captured / absent
        [Display(Name = "Marks Obtained")]
        public decimal? MarksObtained { get; set; }

        // True if student was legitimately absent (mark excluded from average)
        [Display(Name = "Absent?")]
        public bool IsAbsent { get; set; } = false;

        [MaxLength(300)]
        [Display(Name = "Teacher Comment")]
        public string TeacherComment { get; set; }

        // When the mark was last saved
        [Display(Name = "Date Captured")]
        public DateTime? CapturedAt { get; set; }

        // ─── Navigation ───────────────────────────────────────────────────────────
        public virtual Assessment Assessment { get; set; }
        public virtual Student Student { get; set; }
    }
}
