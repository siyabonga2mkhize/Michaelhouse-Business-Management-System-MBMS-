using Michaelhouse.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

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
        public string ApplicableGrades { get; set; }

        // Display order within its stream
        public int SortOrder { get; set; }

        // Navigation
        public virtual ICollection<StudentSubject> StudentSubjects { get; set; }
    

    //public int? TeacherId { get; set; }
    //public virtual Teacher Teacher { get; set; }
    //public virtual ICollection<StudentMark> StudentMarks { get; set; }
    //public virtual ICollection<TimetableEntry> TimetableEntries { get; set; }
    }
}