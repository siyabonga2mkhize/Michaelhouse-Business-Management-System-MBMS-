using Michaelhouse.Models.Enums; // Required for AcademicStream enum
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Subject
    {
        [Key]
        public int SubjectId { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Subject Name")]
        public string Name { get; set; }

        [MaxLength(50)]
        [Display(Name = "Subject Code")]
        public string Code { get; set; }

        // Which stream this subject belongs to (e.g., Science, Humanities)
        [Display(Name = "Academic Stream")]
        public AcademicStream Stream { get; set; }

        [Display(Name = "Compulsory")]
        public bool IsCompulsory { get; set; }

        [Display(Name = "Language Choice")]
        public bool IsLanguage { get; set; }

        [Display(Name = "Maths Option")]
        public bool IsMathsOption { get; set; }

        [Display(Name = "Requires Core Maths")]
        public bool RequiresMaths { get; set; }

        // Comma-separated grades e.g. "8,9" or "10,11,12"
        [Display(Name = "Applicable Grades")]
        public string ApplicableGrades { get; set; }

        public int SortOrder { get; set; }

        [Required]
        [Display(Name = "Grade Level")]
        public int GradeLevel { get; set; }

        // --- Relationships ---

        // The Head of Subject or primary teacher
        public int? TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public virtual Teacher Teacher { get; set; }

        // Navigation Collections
        public virtual ICollection<StudentMark> StudentMarks { get; set; }
        public virtual ICollection<StudentSubject> StudentSubjects { get; set; }

        // Ensure this matches your TimetableSlot model name
        public virtual ICollection<TimetableSlot> TimetableSlots { get; set; }


    }
}