using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Teacher
    {
        [Key]
        public int TeacherId { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        public string Name => $"{FirstName} {LastName}";

        [Required, MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [MaxLength(20)]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Display(Name = "Specialization")]
        public string Specialization { get; set; }

        [Display(Name = "Hire Date")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; }

        [MaxLength(100)]
        [Display(Name = "Department")]
        public string? Department { get; set; }

        [MaxLength(50)]
        [Display(Name = "Employee Number")]
        public string? EmployeeNumber { get; set; }

        // --- Relationships ---

        // Foreign Key to AppUser (Role = "Teacher")
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; }

        // Collections for Academic and Attendance data
        public virtual ICollection<TeacherSubjectGrade> SubjectAssignments { get; set; }
        public virtual ICollection<TeacherAttendance> TeacherAttendances { get; set; }
        public virtual ICollection<TimetableSlot> TimetableSlots { get; set; }
        public virtual ICollection<Subject> Subjects { get; set; }
        public virtual ICollection<StreamEnrolment> StreamEnrolments { get; set; }
    }
}