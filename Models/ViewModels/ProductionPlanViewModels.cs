using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    public class ProductionPlanViewModel
    {
        public ProductionPlanViewModel()
        {
            Days = new List<ProductionDayViewModel>();
            WeekTotalIngredients = new List<IngredientLineViewModel>();
        }

        public int MenuId { get; set; }

        public DateTime WeekStart { get; set; }

        public DateTime WeekEnd { get; set; }

        public bool IsProductionConfirmed { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public int TotalMeals { get; set; }

        public List<ProductionDayViewModel> Days { get; set; }

        public List<IngredientLineViewModel> WeekTotalIngredients { get; set; }
    }

    public class ProductionDayViewModel
    {
        public ProductionDayViewModel()
        {
            Slots = new List<ProductionSlotViewModel>();
            DayTotalIngredients = new List<IngredientLineViewModel>();
        }

        public DateTime Date { get; set; }

        public List<ProductionSlotViewModel> Slots { get; set; }

        public List<IngredientLineViewModel> DayTotalIngredients { get; set; }
    }

    public class ProductionSlotViewModel
    {
        public ProductionSlotViewModel()
        {
            Tasks = new List<ProductionTaskViewModel>();
            HouseBreakdown = new List<HouseBreakdownLine>();
            EventsThisMeal = new List<string>();
        }

        public string MealSlot { get; set; }

        public TimeSpan ServeTime { get; set; }

        public int Portions { get; set; }

        public List<ProductionTaskViewModel> Tasks { get; set; }

        // UC12: per-house portions for this slot.
        public List<HouseBreakdownLine> HouseBreakdown { get; set; }

        // UC12: match labels for this slot, e.g. "Rugby vs Hilton".
        public List<string> EventsThisMeal { get; set; }
    }

    public class ProductionTaskViewModel
    {
        public ProductionTaskViewModel()
        {
            Ingredients = new List<IngredientLineViewModel>();
        }

        public string DishName { get; set; }

        public string Station { get; set; }

        public int Portions { get; set; }

        public int PrepMinutes { get; set; }

        public int CookMinutes { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan ReadyTime { get; set; }

        public List<IngredientLineViewModel> Ingredients { get; set; }
    }

    public class IngredientLineViewModel
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; }

        public string Unit { get; set; }

        public decimal QuantityPerPortion { get; set; }

        public decimal TotalRequired { get; set; }

        public decimal StockAvailable { get; set; }

        public decimal Shortfall { get; set; }

        public bool IsCovered
        {
            get { return Shortfall <= 0m; }
        }
    }

    // UC12: one row per house, per slot.
    public class HouseBreakdownLine
    {
        public string ResidenceName { get; set; }
        public int ActiveStudents { get; set; }
        public int Unavailable { get; set; }
        public int MatchPlayers { get; set; }

        public int TotalPortions
        {
            get { return ActiveStudents + MatchPlayers; }
        }
    }
}