using Michaelhouse.Models.Cafeteria;
using System;
using System.Linq;
using System.Web;
using System.Web.Caching;

namespace Michaelhouse.Services
{
    // ============================================================
    // Manual opening of meal collection
    //
    // Normally the meal being collected comes from the timetable
    // (MealTimes). The Cafeteria Manager can open one of today's
    // meals for collection outside those hours — for testing or a
    // demonstration — for a limited time, and close it again.
    //
    // Only the time window changes. Every other check (face match,
    // submitted meal plan, a meal chosen, not already collected, meal
    // on the menu) still applies.
    //
    // Held in memory: an opening ends if the site restarts.
    // ============================================================

    public class ManualOpening
    {
        public MealSlot Slot { get; set; }
        public DateTime Date { get; set; }          // school date
        public DateTime OpenUntil { get; set; }     // school time
        public string OpenedBy { get; set; }
    }

    public static class CollectionOpening
    {
        // Minutes the manager can choose from
        public static readonly int[] DurationOptions = { 30, 60, 120, 240 };

        private const string CacheKey = "MealCollection.ManualOpening";

        // The opening in effect now, or null
        public static ManualOpening Current(DateTime schoolNow)
        {
            var opening = HttpRuntime.Cache[CacheKey] as ManualOpening;

            if (opening == null || opening.Date != schoolNow.Date || schoolNow >= opening.OpenUntil)
            {
                return null;
            }

            return opening;
        }

        // The meal being collected now: a manual opening wins over the timetable
        public static MealSlot? ServingSlot(DateTime schoolNow)
        {
            var opening = Current(schoolNow);
            return opening != null ? opening.Slot : MealTimes.CurrentSlot(schoolNow);
        }

        // Opens one of today's meals; ends at midnight at the latest.
        // Replaces any opening already in effect.
        public static ManualOpening Open(MealSlot slot, int minutes, string openedBy, DateTime schoolNow)
        {
            if (!DurationOptions.Contains(minutes))
            {
                throw new ArgumentOutOfRangeException(nameof(minutes));
            }

            var until = schoolNow.AddMinutes(minutes);
            if (until.Date != schoolNow.Date) until = schoolNow.Date.AddDays(1);

            var opening = new ManualOpening
            {
                Slot = slot,
                Date = schoolNow.Date,
                OpenUntil = until,
                OpenedBy = openedBy
            };

            HttpRuntime.Cache.Insert(CacheKey, opening, null,
                DateTime.UtcNow.Add(until - schoolNow), Cache.NoSlidingExpiration);

            return opening;
        }

        public static void Close()
        {
            HttpRuntime.Cache.Remove(CacheKey);
        }
    }
}
