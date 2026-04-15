using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    public class AttendanceSummaryViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int GradeLevel { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int TotalClasses { get; set; }
        public int PresentCount { get; set; }          // Present + Late? Usually Late counts as present for percentage
        public double AttendancePercentage => TotalClasses == 0 ? 0 : Math.Round((double)PresentCount / TotalClasses * 100, 2);
    }

    // Optional: wrapper for the view with filters
    public class AttendanceSummaryFilterViewModel
    {
        public List<AttendanceSummaryViewModel> Summaries { get; set; }
        public List<Subject> Subjects { get; set; }
        public List<Student> Students { get; set; }
        public int? SelectedSubjectId { get; set; }
        public int? SelectedStudentId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}