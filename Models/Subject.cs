using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Subject
    {
        public int SubjectId { get; set; }

        [Required, Display(Name = "Subject Name")]
        [StringLength(100)]
        public string SubjectName { get; set; }

        [Display(Name = "Subject Code")]
        [StringLength(10)]
        public string SubjectCode { get; set; }

        [MaxLength(100)]
        public string Category { get; set; }

        public bool IsCompulsory { get; set; }

        // Comma-separated grades e.g. "8,9" or "10,11,12"
        public string ApplicableGrades { get; set; }

        [Display(Name = "Credits")]
        public int Credits { get; set; }

        [Required]
        public int GradeLevel { get; set; }

        public int? TeacherId { get; set; }
        public virtual Teacher Teacher { get; set; }
        public virtual ICollection<StudentMark> StudentMarks { get; set; }
        // Navigation
        public virtual ICollection<StudentSubject> StudentSubjects { get; set; }

        //public int? TeacherId { get; set; }
        //public virtual Teacher Teacher { get; set; }
        //public virtual ICollection<StudentMark> StudentMarks { get; set; }
        //public virtual ICollection<TimetableEntry> TimetableEntries { get; set; }
    }
}