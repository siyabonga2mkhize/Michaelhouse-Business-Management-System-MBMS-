using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class Application
	{
        public int ApplicationId { get; set; }

        [Required, Display(Name = "Student First Name")]
        public string StudentFirstName { get; set; }

        [Required, Display(Name = "Student Last Name")]
        public string StudentLastName { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public string Gender { get; set; }

        [Display(Name = "Applying for Grade")]
        public int ApplyingForGrade { get; set; }

        [Display(Name = "Previous School")]
        public string PreviousSchool { get; set; }

        [Display(Name = "Previous School Marks (%)")]
        public double PreviousMarks { get; set; }

        [Display(Name = "Application Date")]
        [DataType(DataType.Date)]
        public DateTime ApplicationDate { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Pending, Approved, Rejected

        [Display(Name = "Admin Comments")]
        public string AdminComments { get; set; }

        [Display(Name = "Documents Uploaded")]
        public string DocumentPath { get; set; }

        [Display(Name = "Requires Boarding")]
        public bool RequiresBoarding { get; set; }

        // AI Review Score
        [Display(Name = "AI Review Score")]
        public int? AIReviewScore { get; set; }

        public int ParentId { get; set; }
        public virtual Parent Parent { get; set; }
    }
}