using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class TeacherAttendance
	{
        public int TeacherAttendanceId { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Display(Name = "Sign In Time")]
        public DateTime? SignInTime { get; set; }

        [Display(Name = "Sign Out Time")]
        public DateTime? SignOutTime { get; set; }

        public string Status { get; set; } // Present, Absent, Leave

        public int TeacherId { get; set; }
        public virtual Teacher Teacher { get; set; }
    }
}