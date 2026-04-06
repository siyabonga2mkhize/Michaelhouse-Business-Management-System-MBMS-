<<<<<<< HEAD
﻿using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class StudentMark
    {
=======
﻿using System;
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
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f
        public int StudentMarkId { get; set; }

        [ForeignKey("Assessment")]
        public int AssessmentmentId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        // ─── The captured mark ─────────────────────────────────────────────────────

        // Null = not yet captured / absent
        public decimal? MarksObtained { get; set; }

        // True if student was legitimately absent (mark excluded from average)
        public bool IsAbsent { get; set; } = false;

        [MaxLength(300)]
        public string TeacherComment { get; set; }

        // When the mark was last saved
        public DateTime? CapturedAt { get; set; }

        // ─── Navigation ───────────────────────────────────────────────────────────
        public virtual Assessment Assessment { get; set; }
        public virtual Student Student { get; set; }
    }
}