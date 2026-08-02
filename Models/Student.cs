using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Student
    {
        [Key]
        public int StudentId { get; set; }

        // ─── Basic Info ───────────────────────────────────────────────────────────
        [Required, MaxLength(200)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        // Full name computed for display
        public string Name => $"{FirstName} {LastName}";

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DOB { get; set; }

        [MaxLength(50)]
        [Display(Name = "Home Language")]
        public string HomeLanguage { get; set; }

        [MaxLength(20)]
        [Display(Name = "ID / Passport Number")]
        public string IdNumber { get; set; }

        // ─── Previous School ──────────────────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Previous School")]
        public string PreviousSchool { get; set; }

        [MaxLength(50)]
        [Display(Name = "Current Grade")]
        public string CurrentGrade { get; set; } // Grade they are currently in

        // ─── Medical ──────────────────────────────────────────────────────────────
        [MaxLength(500)]
        [Display(Name = "Medical Conditions / Allergies")]
        public string MedicalConditions { get; set; }


        [ForeignKey("Parent")]
        public int ParentId { get; set; }

        // Set after parent completes registration
        public int? UserId { get; set; }


        // --- MISSING PROPERTIES TO ADD ---

        
        [Display(Name = "Student Number")]
        public string StudentNumber { get; set; }

        //[Required]
        //public string Gender { get; set; }

        [Required]
        [Display(Name = "Grade Level")]
        public int GradeLevel { get; set; } // Matches Subject.GradeLevel

        [Required]
        [Display(Name = "Enrollment Date")]
        public DateTime EnrollmentDate { get; set; } = DateTime.Now;

        public int? ClassId { get; set; }

        // Navigation
        public Parent Parent { get; set; }
        public virtual AppUser User { get; set; }
        public ICollection<Application> Applications { get; set; }
        public virtual ICollection<StudentSubject> StudentSubjects { get; set; }

    }
}