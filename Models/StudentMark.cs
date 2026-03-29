using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class StudentMark
	{
        public int StudentMarkId { get; set; }

        [Display(Name = "Assessment Name")]
        public string AssessmentName { get; set; } // Test 1, Exam, Assignment

        [Display(Name = "Assessment Type")]
        public string AssessmentType { get; set; } // Test, Exam, Assignment, Project

        [Display(Name = "Mark Obtained")]
        public double MarkObtained { get; set; }

        [Display(Name = "Total Marks")]
        public double TotalMarks { get; set; }

        [Display(Name = "Weight (%)")]
        public double Weight { get; set; }

        [Display(Name = "Term")]
        public int Term { get; set; }

        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Percentage")]
        public double Percentage => TotalMarks > 0 ? (MarkObtained / TotalMarks) * 100 : 0;

        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public int SubjectId { get; set; }
        public virtual Subject Subject { get; set; }
    }
}