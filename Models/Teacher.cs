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

        // Linked AppUser account (Role = "Teacher")
        public int? UserId { get; set; }
        public virtual AppUser User { get; set; }

        // A teacher teaches up to 2 subjects
        // Each TeacherSubjectGrade record = one subject + grade assignment
        public virtual ICollection<TeacherSubjectGrade> SubjectAssignments { get; set; }

        // Timetable slots assigned to this teacher
        public virtual ICollection<TimetableSlot> TimetableSlots { get; set; }
        public virtual ICollection<StreamEnrolment> StreamEnrolments { get; set; }
    }
}