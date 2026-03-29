using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls.WebParts;

namespace MIEMS.Models
{
	public class Student
	{
        public int StudentId { get; set; }

        [Required, Display(Name = "First Name")]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required, Display(Name = "Last Name")]
        [StringLength(50)]
        public string LastName { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(10)]
        public string Gender { get; set; }

        [Display(Name = "Grade")]
        public int GradeLevel { get; set; }

        [Display(Name = "Student Number")]
        public string StudentNumber { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Phone]
        [Display(Name = "Phone")]
        public string Phone { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Enrollment Date")]
        [DataType(DataType.Date)]
        public DateTime EnrollmentDate { get; set; }

        [Display(Name = "Is Boarder")]
        public bool IsBoarder { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Active, Graduated, Withdrawn

        [Display(Name = "Full Name")]
        public string FullName => FirstName + " " + LastName;

        // Navigation
        public int? BoardingHouseId { get; set; }
        public virtual BoardingHouse BoardingHouse { get; set; }
        public virtual ICollection<Attendance> Attendances { get; set; }
        public virtual ICollection<StudentMark> StudentMarks { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; }
        public virtual Parent Parent { get; set; }
        public int? ParentId { get; set; }
    }
}