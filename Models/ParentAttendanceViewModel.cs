using System;

namespace Michaelhouse.Models
{
    public class ParentAttendanceViewModel
    {
        public string StudentName { get; set; }
        public DateTime Date { get; set; }
        public string SubjectName { get; set; }
        public AttendanceStatus Status { get; set; }
        public string RecordedBy { get; set; }
    }
}