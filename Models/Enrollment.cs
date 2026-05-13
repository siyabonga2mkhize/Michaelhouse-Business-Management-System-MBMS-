using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        [Required]
        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        [Required]
        public int SubjectId { get; set; }
        public virtual Subject Subject { get; set; }

        [Display(Name = "Registration Date")]
        public DateTime EnrollmentDate { get; set; }

        public string AcademicYear { get; set; } // e.g., "2026"
    }
}