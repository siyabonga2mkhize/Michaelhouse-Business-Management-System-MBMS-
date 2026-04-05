using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Defines the time periods in a school day.
    /// e.g. Period 1 = 07:30-08:25, Period 2 = 08:25-09:20 etc.
    /// </summary>
    public class Period
    {
        [Key]
        public int PeriodId { get; set; }

        [Required]
        public int PeriodNumber { get; set; } // 1-7

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [MaxLength(50)]
        public string Label { get; set; } // e.g. "Period 1", "Break", "Lunch"

        public bool IsBreak { get; set; } // true = break/lunch, no lessons

        public int DurationMinutes => (int)(EndTime - StartTime).TotalMinutes;
    }

    /// <summary>
    /// A single slot in the timetable.
    /// Represents: On [Day], [Period] → [Teacher] teaches [Subject] to [Grade] ([Stream])
    /// </summary>
    public class TimetableSlot
    {
        [Key]
        public int SlotId { get; set; }

        // Which academic year this timetable is for
        public int AcademicYear { get; set; }

        // Day of week: 1=Monday, 2=Tuesday ... 5=Friday
        public int DayOfWeek { get; set; }

        [ForeignKey("Period")]
        public int PeriodId { get; set; }

        [ForeignKey("Teacher")]
        public int TeacherId { get; set; }

        [ForeignKey("Subject")]
        public int SubjectId { get; set; }

        // Which grade is being taught
        public int Grade { get; set; }

        // For Grade 10-12:
        // - Compulsory subjects: Stream = None (whole grade together)
        // - Stream subjects: Stream = specific stream (only that stream's students)
        public AcademicStream Stream { get; set; } = AcademicStream.None;

        // Navigation
        public virtual Period Period { get; set; }
        public virtual Teacher Teacher { get; set; }
        public virtual Subject Subject { get; set; }
    }
}