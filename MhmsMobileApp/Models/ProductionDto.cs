using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    public class ProductionPlanDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public int MenuId { get; set; }
        public string WeekStart { get; set; }
        public string WeekEnd { get; set; }
        public string WeekLabel { get; set; }
        public bool IsProductionConfirmed { get; set; }
        public int TotalMeals { get; set; }
        public List<ProductionDayDto> Days { get; set; }
        public List<ProductionIngredientDto> WeekIngredients { get; set; }

        public ProductionPlanDto()
        {
            Days = new List<ProductionDayDto>();
            WeekIngredients = new List<ProductionIngredientDto>();
        }
    }

    public class ProductionDayDto
    {
        public string Date { get; set; }
        public string DayLabel { get; set; }
        public List<ProductionSlotDto> Slots { get; set; }

        public ProductionDayDto()
        {
            Slots = new List<ProductionSlotDto>();
        }
    }

    public class ProductionSlotDto
    {
        public string MealSlot { get; set; }
        public string ServeTime { get; set; }
        public int Portions { get; set; }
        public List<ProductionTaskDto> Tasks { get; set; }

        public ProductionSlotDto()
        {
            Tasks = new List<ProductionTaskDto>();
        }
    }

    public class ProductionTaskDto
    {
        public string DishName { get; set; }
        public string Station { get; set; }
        public int Portions { get; set; }
        public int PrepMinutes { get; set; }
        public int CookMinutes { get; set; }
        public string StartTime { get; set; }
        public string ReadyTime { get; set; }
    }

    public class ProductionIngredientDto
    {
        public int IngredientId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal TotalRequired { get; set; }
        public decimal StockAvailable { get; set; }
        public decimal Shortfall { get; set; }
        public bool IsCovered { get; set; }
    }
}