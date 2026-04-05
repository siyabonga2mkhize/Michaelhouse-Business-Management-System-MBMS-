using System;
using System.Collections.Generic;

namespace Michaelhouse.Models
{
    public class MarkAttendanceViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public DateTime Date { get; set; }
        public List<StudentAttendanceSelection> Students { get; set; }
    }
    public class StudentAttendanceSelection
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; } // Present, Absent, Late
    }
}