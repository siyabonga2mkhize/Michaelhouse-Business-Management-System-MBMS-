using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public enum TeacherStatus
    {
        Present,
        Absent,
        Leave
    }
    public class TeacherAttendance
    {
        public int TeacherAttendanceId { get; set; }
        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Display(Name = "Sign In Time")]
        public DateTime? SignInTime { get; set; }

        [Display(Name = "Sign Out Time")]
        public DateTime? SignOutTime { get; set; }

        public TeacherStatus Status { get; set; }

        [Display(Name = "Geofence Verified")]
        public bool IsVerified { get; set; }

        public int TeacherId { get; set; }
        public virtual Teacher Teacher { get; set; }
    }
}