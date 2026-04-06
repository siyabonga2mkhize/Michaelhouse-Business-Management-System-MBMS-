using System;

namespace Michaelhouse.Models
{
    public class AttendanceRecordViewModel
    {
        public DateTime Date { get; set; }
        public string StudentName { get; set; }
        public int StudentGrade { get; set; }
        public string SubjectName { get; set; }
        public AttendanceStatus Status { get; set; }
        public string RecordedBy { get; set; }
    }
}