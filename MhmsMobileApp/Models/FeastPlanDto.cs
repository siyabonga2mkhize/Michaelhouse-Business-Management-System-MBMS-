using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    public class FeastPlanDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; }

        public int EventId { get; set; }
        public string EventName { get; set; }
        public string EventDate { get; set; }
        public string EventDateLabel { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string VenueName { get; set; }
        public string MenuTemplateName { get; set; }

        public int TotalGuests { get; set; }
        public int StandardGuests { get; set; }
        public int VegetarianGuests { get; set; }
        public int OtherGuests { get; set; }
        public int ShortageCount { get; set; }

        public List<string> DietaryNotes { get; set; }
        public List<FeastTimelineDayDto> Timeline { get; set; }
        public List<FeastDishDto> Dishes { get; set; }
        public List<FeastIngredientDto> TotalIngredients { get; set; }

        public FeastPlanDto()
        {
            DietaryNotes = new List<string>();
            Timeline = new List<FeastTimelineDayDto>();
            Dishes = new List<FeastDishDto>();
            TotalIngredients = new List<FeastIngredientDto>();
        }
    }

    public class FeastTimelineDayDto
    {
        public string Phase { get; set; }
        public string DayLabel { get; set; }
        public string CalendarDate { get; set; }
        public List<FeastTimelineDishDto> Dishes { get; set; }

        public FeastTimelineDayDto()
        {
            Dishes = new List<FeastTimelineDishDto>();
        }
    }

    public class FeastTimelineDishDto
    {
        public string DishName { get; set; }
        public string Station { get; set; }
        public int Portions { get; set; }
        public int PrepMinutes { get; set; }
        public int CookMinutes { get; set; }
    }

    public class FeastDishDto
    {
        public string Section { get; set; }
        public string DishName { get; set; }
        public string Classification { get; set; }
        public int Portions { get; set; }
        public string Station { get; set; }
        public string StartTime { get; set; }
        public string ReadyTime { get; set; }
        public int PrepMinutes { get; set; }
        public int CookMinutes { get; set; }
        public List<FeastIngredientDto> Ingredients { get; set; }

        public FeastDishDto()
        {
            Ingredients = new List<FeastIngredientDto>();
        }
    }

    public class FeastIngredientDto
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal QuantityPerPortion { get; set; }
        public decimal TotalRequired { get; set; }
        public decimal StockAvailable { get; set; }
        public decimal Shortfall { get; set; }
        public bool IsCovered { get; set; }
    }
}