using System;

namespace Michaelhouse.Models
{
    public class TeacherAttendanceRecordViewModel
    {
        public string TeacherName { get; set; }
        public DateTime Date { get; set; }
        public DateTime? SignInTime { get; set; }
        public DateTime? SignOutTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool IsVerified { get; set; }
    }
}