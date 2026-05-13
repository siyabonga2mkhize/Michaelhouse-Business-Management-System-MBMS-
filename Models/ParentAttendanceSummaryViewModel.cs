using System;

namespace Michaelhouse.Models
{
    public class ParentAttendanceSummaryViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int GradeLevel { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int TotalClasses { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public double AttendancePercentage => TotalClasses == 0 ? 0 : Math.Round((double)(PresentCount + LateCount) / TotalClasses * 100, 2);
    }
}