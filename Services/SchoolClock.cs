using System;

namespace Michaelhouse.Services
{
    // ============================================================
    // School clock — the date and time at Michaelhouse (SAST, UTC+2).
    //
    // The app is hosted on Azure, where the server clock is UTC, so
    // DateTime.Now / DateTime.Today would be two hours out. Use this
    // for rules that depend on the school day, such as the meal-plan
    // selection deadline.
    // ============================================================

    public static class SchoolClock
    {
        private static readonly TimeZoneInfo Zone = FindZone();

        public static DateTime Now
        {
            get { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone); }
        }

        public static DateTime Today
        {
            get { return Now.Date; }
        }

        // For displaying timestamps stored with DateTime.UtcNow
        public static DateTime FromUtc(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        }

        private static TimeZoneInfo FindZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Local;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}
