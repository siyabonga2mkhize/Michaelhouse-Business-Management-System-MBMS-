<<<<<<< HEAD
﻿using System.Collections.Generic;
=======
﻿using Michaelhouse.Models.Enums;
using System;
using System.Collections.Generic;
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
<<<<<<< HEAD
    public class Subject
    {
=======
	public class Subject
	{
        [Key]
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f
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