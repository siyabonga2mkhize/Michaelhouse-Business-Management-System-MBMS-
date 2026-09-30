using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    // ============================================================
    // UC19 — Feast Plan
    // Generated on demand from a closed-RSVP event + its menu template.
    // ============================================================
    public class FeastPlanViewModel
    {
        public FeastPlanViewModel()
        {
            Dishes = new List<FeastPlanDish>();
            TotalIngredients = new List<FeastPlanIngredient>();
            DietaryNotes = new List<string>();
            Timeline = new List<FeastPlanTimelineDay>();
        }

        public int EventId { get; set; }
        public string EventName { get; set; }
        public DateTime EventDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string VenueName { get; set; }
        public string MenuTemplateName { get; set; }

        public int TotalGuests { get; set; }
        public int StandardGuests { get; set; }
        public int VegetarianGuests { get; set; }
        public int OtherGuests { get; set; }

        public List<string> DietaryNotes { get; set; }

        public List<FeastPlanDish> Dishes { get; set; }
        public List<FeastPlanIngredient> TotalIngredients { get; set; }
        public List<FeastPlanTimelineDay> Timeline { get; set; }

        public int ShortageCount
        {
            get
            {
                int c = 0;
                foreach (var i in TotalIngredients) if (!i.IsCovered) c++;
                return c;
            }
        }
    }

    public class FeastPlanDish
    {
        public FeastPlanDish()
        {
            Ingredients = new List<FeastPlanIngredient>();
        }

        public string Section { get; set; }
        public string DishName { get; set; }
        public string Classification { get; set; }
        public int Portions { get; set; }
        public decimal QuantityPerGuest { get; set; }
        public List<FeastPlanIngredient> Ingredients { get; set; }

        public string Station { get; set; }
        public int PrepMinutes { get; set; }
        public int CookMinutes { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan ReadyTime { get; set; }

        public FeastPrepPhase PrepPhase { get; set; }
    }

    public class FeastPlanIngredient
    {
        public int IngredientId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal QuantityPerPortion { get; set; }
        public decimal TotalRequired { get; set; }
        public decimal StockAvailable { get; set; }
        public decimal Shortfall { get; set; }

        public bool IsCovered { get { return Shortfall <= 0m; } }
    }

    public class FeastPlanTimelineDay
    {
        public FeastPlanTimelineDay()
        {
            Dishes = new List<FeastPlanDish>();
        }

        public FeastPrepPhase Phase { get; set; }
        public DateTime CalendarDate { get; set; }
        public string DayLabel { get; set; }
        public List<FeastPlanDish> Dishes { get; set; }
    }

    public enum FeastPrepPhase
    {
        TwoDaysBefore = 1,
        DayBefore = 2,
        DayOf = 3
    }
}