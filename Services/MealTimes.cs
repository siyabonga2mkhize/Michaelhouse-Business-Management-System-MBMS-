using Michaelhouse.Models.Cafeteria;
using System;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Cafeteria meal times — one place for when each meal is served
    // and when it can be collected.
    //
    // Serve times are the ones the kitchen plan works back from
    // (MenuSchedulingService.BuildProductionPlan). A meal can be
    // collected from one hour before its serve time until two hours
    // after. The collection terminal uses school time (SchoolClock)
    // to decide which meal is being served now.
    // ============================================================

    public static class MealTimes
    {
        private static readonly TimeSpan CollectionOpensBefore = TimeSpan.FromHours(1);
        private static readonly TimeSpan CollectionClosesAfter = TimeSpan.FromHours(2);

        public static TimeSpan ServeTime(MealSlot slot)
        {
            switch (slot)
            {
                case MealSlot.Breakfast: return new TimeSpan(7, 0, 0);
                case MealSlot.Lunch: return new TimeSpan(12, 30, 0);
                case MealSlot.Dinner: return new TimeSpan(18, 0, 0);
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        public static TimeSpan CollectionOpens(MealSlot slot)
        {
            return ServeTime(slot) - CollectionOpensBefore;
        }

        public static TimeSpan CollectionCloses(MealSlot slot)
        {
            return ServeTime(slot) + CollectionClosesAfter;
        }

        // The meal being served at this school time, or null between meals
        public static MealSlot? CurrentSlot(DateTime schoolNow)
        {
            var time = schoolNow.TimeOfDay;

            foreach (var slot in new[] { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Dinner })
            {
                if (time >= CollectionOpens(slot) && time < CollectionCloses(slot))
                {
                    return slot;
                }
            }

            return null;
        }

        // e.g. "Breakfast 06:00–09:00, Lunch 11:30–14:30, Dinner 17:00–20:00"
        public static string CollectionHours()
        {
            return string.Join(", ", new[] { MealSlot.Breakfast, MealSlot.Lunch, MealSlot.Dinner }
                .Select(s => string.Format("{0} {1:hh\\:mm}–{2:hh\\:mm}", s, CollectionOpens(s), CollectionCloses(s))));
        }
    }
}
