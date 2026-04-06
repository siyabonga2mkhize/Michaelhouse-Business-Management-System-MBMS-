using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
<<<<<<< HEAD
=======
using System.ComponentModel.DataAnnotations.Schema;
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f

namespace Michaelhouse.Models
{
    public class Teacher
    {
<<<<<<< HEAD
        public int? UserId { get; set; } // The Foreign Key
=======
        [Key]
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f
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

<<<<<<< HEAD
        [Display(Name = "Specialization")]
        public string Specialization { get; set; }

        [Display(Name = "Hire Date")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; }

        public string FullName => FirstName + " " + LastName;

        public virtual ICollection<Subject> Subjects { get; set; }
        public virtual ICollection<TeacherAttendance> TeacherAttendances { get; set; }
        public virtual AppUser User { get; set; } // The Navigation Property
=======
        // Timetable slots assigned to this teacher
        public virtual ICollection<TimetableSlot> TimetableSlots { get; set; }
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f
    }
}