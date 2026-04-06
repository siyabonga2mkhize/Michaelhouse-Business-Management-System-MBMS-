using Michaelhouse.Models.Enums;
using System;
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
        public string Name { get; set; }

        [MaxLength(50)]
        public string Code { get; set; }

        // Which stream this subject belongs to
        public AcademicStream Stream { get; set; }

        // Compulsory = all students must take it (English, LO)
        public bool IsCompulsory { get; set; }

        // Is this a language choice (Afrikaans, isiZulu, French etc.)
        public bool IsLanguage { get; set; }

        // Is this a maths option (Mathematics, Math Literacy, Further Studies Maths)
        public bool IsMathsOption { get; set; }

        // Requires Mathematics (not Math Literacy) as prerequisite
        public bool RequiresMaths { get; set; }

        // Comma-separated grades e.g. "8,9" or "10,11,12"
        [Display(Name = "Applicable Grades")]
        public string ApplicableGrades { get; set; }

        // Display order within its stream
        public int SortOrder { get; set; }
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
    

    //public int? TeacherId { get; set; }
    //public virtual Teacher Teacher { get; set; }
    //public virtual ICollection<StudentMark> StudentMarks { get; set; }
    //public virtual ICollection<TimetableEntry> TimetableEntries { get; set; }

        // Ensure this matches your TimetableSlot model name
        public virtual ICollection<TimetableSlot> TimetableSlots { get; set; }


    }
}