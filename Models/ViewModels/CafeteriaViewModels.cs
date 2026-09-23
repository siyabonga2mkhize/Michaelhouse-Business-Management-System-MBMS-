using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.ViewModels
{
    public class BoardingHouseScheduleOption
    {
        public int Id { get; set; }

        public string Name { get; set; }

        [Range(0, 100000)]
        public int StudentCount { get; set; }

        public bool IsInSeason { get; set; }

        [StringLength(100)]
        public string ActiveSport { get; set; }


        // ────────────────────────────────────────────────────────
        // UC12: optional match date for this house's squad.
        // Null = no match this week (normal rotation).
        // Set  = generator will steer Fri (day-before) to high-carb
        //        and Sat (match day) to high-protein for this house.
        // ────────────────────────────────────────────────────────
        [DataType(DataType.Date)]
        [Display(Name = "Next match date")]
        public DateTime? MatchDate { get; set; }

    }

    public class ScheduleMenuInputViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "End date")]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(0, 100000)]
        [Display(Name = "Staff meals per meal")]
        public int StaffMeals { get; set; }

        [StringLength(2000)]
        [Display(Name = "Special event notes")]
        public string SpecialEventNotes { get; set; }

        public IList<BoardingHouseScheduleOption> BoardingHouses { get; set; }

        public ScheduleMenuInputViewModel()
        {
            BoardingHouses = new List<BoardingHouseScheduleOption>();
        }
    }

    public class MenuScheduleItemDisplay
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public string MealSlot { get; set; }

        public string MenuItemName { get; set; }

        public string DietaryClassification { get; set; }

        public int CalculatedPortions { get; set; }

        public ItemTagStatus ItemTagStatus { get; set; }

        public string StatusText { get; set; }

        public string StatusCssClass { get; set; }

        public string TagReason { get; set; }

        public bool IsFarmGrownProduce { get; set; }

        public decimal CaloriesPerPortion { get; set; }

        public decimal ProteinGramsPerPortion { get; set; }

        public string SubstitutionMenuItemName { get; set; }
    }

    public class ProposedMenuViewModel
    {
        public int Id { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int StaffMeals { get; set; }

        public string SpecialEventNotes { get; set; }

        public MenuStatus MenuStatus { get; set; }
        public bool IsKitchenReady { get; set; }

        public DateTime? KitchenReadyDate { get; set; }

        public string RejectionReason { get; set; }

        public IList<MenuScheduleItemDisplay> ScheduleItems { get; set; }

        public int ConfirmedCount { get; set; }

        public int NeedsSubstitutionCount { get; set; }

        public int NeedsReviewCount { get; set; }

        public ProposedMenuViewModel()
        {
            ScheduleItems = new List<MenuScheduleItemDisplay>();
        }
    }

    public class ModifyMenuItemRequest
    {
        [Required]
        public int ScheduleItemId { get; set; }

        [Required]
        [Display(Name = "New Menu Item")]
        public int NewMenuItemId { get; set; }

        [Required]
        [Range(1, 1000000)]
        [Display(Name = "New Portions")]
        public int NewPortions { get; set; }
    }

    public class SubstituteOption
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public bool IsValid { get; set; }
        public bool IsInStock { get; set; }
        public bool IsCooldownOk { get; set; }
        public string Reason { get; set; }
    }
}