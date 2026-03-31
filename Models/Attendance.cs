using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Late
    }
    public class Attendance
    {
        public int AttendanceId { get; set; }

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Display(Name = "Status")]
        public AttendanceStatus Status { get; set; } // Present, Absent, Late

        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public int SubjectId { get; set; }
        public virtual Subject Subject { get; set; }

        [Display(Name = "Recorded By")]
        public string RecordedBy { get; set; }


    }
}