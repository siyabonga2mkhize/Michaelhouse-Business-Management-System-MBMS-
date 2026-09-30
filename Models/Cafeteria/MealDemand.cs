using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC12 rework — Computed portion demand for one meal slot
    // on one day. Produced by DemandService, consumed by the
    // generator and the Chef's production plan.
    // ============================================================

    public class MealDemand
    {
        public DateTime Date { get; set; }

        /// <summary>Breakfast, Lunch, or Dinner.</summary>
        public string MealSlot { get; set; }

        /// <summary>Sum of active students across all houses.</summary>
        public int HousePortions { get; set; }

        /// <summary>One constant number per meal (from the Schedule form).</summary>
        public int StaffPortions { get; set; }

        /// <summary>Extra portions for students with a match this day.</summary>
        public int SportsUplift { get; set; }

        public int TotalPortions
        {
            get { return HousePortions + StaffPortions + SportsUplift; }
        }

        public List<HouseBreakdown> Houses { get; set; }

        /// <summary>Human-readable match labels, e.g. "Rugby vs Hilton".</summary>
        public List<string> EventsThisMeal { get; set; }

        public MealDemand()
        {
            Houses = new List<HouseBreakdown>();
            EventsThisMeal = new List<string>();
        }
    }

    public class HouseBreakdown
    {
        public int ResidenceId { get; set; }
        public string ResidenceName { get; set; }

        /// <summary>Students available to eat from this house.</summary>
        public int ActiveStudents { get; set; }

        /// <summary>Students flagged unavailable (injury, leave).</summary>
        public int Unavailable { get; set; }

        /// <summary>Active students with a match this day (uplift).</summary>
        public int MatchPlayers { get; set; }

        public int TotalPortions
        {
            get { return ActiveStudents + MatchPlayers; }
        }
    }
}