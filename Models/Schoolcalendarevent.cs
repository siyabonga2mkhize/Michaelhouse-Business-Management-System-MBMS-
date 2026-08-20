using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class Schoolcalendarevent
    {
        /// <summary>
        /// Color/label lookup for calendar event categories. Kept as string
        /// constants (not an enum) to match the string-status convention already
        /// used elsewhere in this codebase.
        /// </summary>
        public static class CalendarEventCategory
        {
            public const string Exam = "Exam";
            public const string TestWeek = "TestWeek";
            public const string Sports = "Sports";
            public const string Cultural = "Cultural";
            public const string Holiday = "Holiday";
            public const string Other = "Other";

            public static readonly string[] All = { Exam, TestWeek, Sports, Cultural, Holiday, Other };

            public static string DisplayName(string category)
            {
                switch (category)
                {
                    case Exam: return "Exam";
                    case TestWeek: return "Test Week";
                    case Sports: return "Sports Event";
                    case Cultural: return "Cultural Event";
                    case Holiday: return "Holiday";
                    default: return "Other";
                }
            }

            public static string ColorHex(string category)
            {
                switch (category)
                {
                    case Exam: return "#dc3545";     // red
                    case TestWeek: return "#fd7e14"; // orange
                    case Sports: return "#0d6efd";   // blue
                    case Cultural: return "#6f42c1"; // purple
                    case Holiday: return "#20c997";  // teal
                    default: return "#6c757d";       // grey
                }
            }

            /// <summary>Sensible default for the "discourage leave" checkbox when an admin picks a category - always overridable.</summary>
            public static bool DefaultDiscourageLeave(string category)
            {
                return category == Exam || category == TestWeek;
            }
        }

        /// <summary>
        /// A school calendar entry managed by admin (exams, test weeks, sports
        /// fixtures, holidays, etc.). Shown as a color-coded calendar to
        /// students, parents and house masters, and used to generate
        /// leave-request recommendations when DiscourageLeave is true.
        /// </summary>
        public class SchoolCalendarEvent
        {
            [Key]
            public int SchoolCalendarEventId { get; set; }

            [Required, StringLength(200)]
            public string Title { get; set; }

            [StringLength(1000)]
            public string Description { get; set; }

            [Required, DataType(DataType.Date)]
            [Display(Name = "Start Date")]
            public DateTime StartDate { get; set; }

            [Required, DataType(DataType.Date)]
            [Display(Name = "End Date")]
            public DateTime EndDate { get; set; }

            [Required, StringLength(30)]
            public string Category { get; set; } = CalendarEventCategory.Other;

            /// <summary>
            /// When true, any leave request whose dates overlap this event will
            /// be flagged with a "recommend against leaving" warning to the
            /// student, parent and house master.
            /// </summary>
            [Display(Name = "Discourage leave requests during this period")]
            public bool DiscourageLeave { get; set; }

            [StringLength(200)]
            public string CreatedBy { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.Now;

            public bool IsArchived { get; set; }
        }
    }
}