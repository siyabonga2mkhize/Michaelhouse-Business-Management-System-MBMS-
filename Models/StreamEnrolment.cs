using Michaelhouse.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Records which academic stream a student is enrolled in for a given grade.
    /// Allows the admin to see all students per stream per grade.
    /// e.g. "Grade 10 — Sciences stream" → list of all students
    /// </summary>
    public class StreamEnrolment
    {
        [Key]
        public int StreamEnrolmentId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [ForeignKey("Teacher")]
        public int? TeacherId { get; set; }

        [ForeignKey("Registration")]
        public int RegistrationId { get; set; }

        public int Grade { get; set; } // 10, 11, or 12

        public AcademicStream Stream { get; set; }

        // Maths or Mathematical Literacy — affects stream eligibility
        public bool TakesMathematics { get; set; } // true = Maths, false = Math Literacy

        public DateTime EnrolledAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual Student Student { get; set; }
        public virtual Registration Registration { get; set; }
        public virtual Teacher Teacher { get; set; }
    }
}