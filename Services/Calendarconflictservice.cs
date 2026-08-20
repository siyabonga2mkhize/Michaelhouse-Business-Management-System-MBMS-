using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using static Michaelhouse.Models.Schoolcalendarevent;

namespace Michaelhouse.Services
{
    public class Calendarconflictservice
    {
        public class CalendarConflict
        {
            public int SchoolCalendarEventId { get; set; }
            public string Title { get; set; }
            public string Category { get; set; }
            public string ColorHex { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        /// <summary>
        /// Finds school calendar events (exams, test weeks, etc.) that overlap
        /// a proposed leave period, so a recommendation can be surfaced to the
        /// student, parent and house master. Comparison is date-only (not
        /// time-of-day), since calendar events are whole-day entries.
        /// </summary>
        public static class CalendarConflictService
        {
            public static List<CalendarConflict> GetConflicts(DBContextClass db, DateTime leaveStart, DateTime leaveEnd)
            {
                var start = leaveStart.Date;
                var end = leaveEnd.Date;

                return db.SchoolCalendarEvents
                    .Where(e => !e.IsArchived
                                && e.DiscourageLeave
                                && DbFunctions.TruncateTime(e.StartDate) <= end
                                && DbFunctions.TruncateTime(e.EndDate) >= start)
                    .OrderBy(e => e.StartDate)
                    .ToList()
                    .Select(e => new CalendarConflict
                    {
                        SchoolCalendarEventId = e.SchoolCalendarEventId,
                        Title = e.Title,
                        Category = e.Category,
                        ColorHex = CalendarEventCategory.ColorHex(e.Category),
                        StartDate = e.StartDate,
                        EndDate = e.EndDate
                    })
                    .ToList();
            }

            public static string BuildSummary(List<CalendarConflict> conflicts)
            {
                if (conflicts == null || conflicts.Count == 0) return null;
                return string.Join("; ", conflicts.Select(c =>
                    $"{c.Title} ({c.StartDate:dd MMM} - {c.EndDate:dd MMM})"));
            }

            public static string BuildRecommendationMessage(List<CalendarConflict> conflicts)
            {
                if (conflicts == null || conflicts.Count == 0) return null;
                var names = string.Join(", ", conflicts.Select(c => c.Title));
                return $"Your selected dates overlap with: {names}. It is recommended the student does not leave the residence during this period unless necessary.";
            }
        }
    }
}