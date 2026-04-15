using System;

namespace Michaelhouse.Models
{
    public class StudentAttendanceSummaryViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int TotalClasses { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public double AttendancePercentage => TotalClasses == 0 ? 0 : Math.Round((double)(PresentCount + LateCount) / TotalClasses * 100, 2);
    }
}