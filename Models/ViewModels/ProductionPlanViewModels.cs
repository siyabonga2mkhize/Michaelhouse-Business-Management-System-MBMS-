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

        // Week-level ingredient total — used for ordering
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

        // Sum of all ingredients across all 3 meal slots for this day
        public List<IngredientLineViewModel> DayTotalIngredients { get; set; }
    }

    public class ProductionSlotViewModel
    {
        public ProductionSlotViewModel()
        {
            Tasks = new List<ProductionTaskViewModel>();
        }

        public string MealSlot { get; set; }

        public TimeSpan ServeTime { get; set; }

        public int Portions { get; set; }

        public List<ProductionTaskViewModel> Tasks { get; set; }
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

        // The BOM for this dish at this portion count
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
}