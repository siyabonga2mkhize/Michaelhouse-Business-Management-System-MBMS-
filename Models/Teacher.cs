using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Teacher
    {
        public int TeacherId { get; set; }

        [Required, Display(Name = "First Name")]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required, Display(Name = "Last Name")]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Phone]
        public string Phone { get; set; }

        [Display(Name = "Employee Number")]
        public string EmployeeNumber { get; set; }

        [Display(Name = "Department")]
        public string Department { get; set; }

        [Display(Name = "Specialization")]
        public string Specialization { get; set; }

        [Display(Name = "Hire Date")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; }

        public string FullName => FirstName + " " + LastName;

        public virtual ICollection<Subject> Subjects { get; set; }
        public virtual ICollection<TeacherAttendance> TeacherAttendances { get; set; }
    }
}