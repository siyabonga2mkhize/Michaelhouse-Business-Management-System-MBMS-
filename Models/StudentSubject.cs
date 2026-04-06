using Michaelhouse.Models.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class StudentSubject
    {

        [Key]
        public int StudentSubjectId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [ForeignKey("Subject")]
        public int SubjectId { get; set; }

        //public bool IsElective { get; set; }

        // Which stream this subject was selected under
        public AcademicStream Stream { get; set; }

        // Compulsory = auto-assigned, not chosen
        public bool IsCompulsory { get; set; }

        // Navigation
        public virtual Student Student { get; set; }
        public virtual Subject Subject { get; set; }

    }
}