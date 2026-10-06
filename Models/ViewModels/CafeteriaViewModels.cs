using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Michaelhouse.Models.ViewModels
{
    public class BoardingHouseScheduleOption
    {
        public int Id { get; set; }

        public string Name { get; set; }

        // Active students living in the house (residence allocation)
        [Range(0, 100000)]
        public int StudentCount { get; set; }
    }

    // A Coach's fixture in the planning range, shown (read-only) on
    // the Schedule form — sports come from the Coaches, not this form
    public class ScheduledSportViewModel
    {
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string EventType { get; set; }

        // e.g. "Rugby vs Hilton"
        public string Label { get; set; }

        // Null = every player of the sport
        public string House { get; set; }

        public int Players { get; set; }

        // e.g. "High-protein on match day, high-carb the day before"
        public string MealNeed { get; set; }
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

        // How many meal options to offer in each breakfast / lunch /
        // dinner slot. Students choose one of them in their meal plan.
        [Range(1, 6)]
        [Display(Name = "Options per meal")]
        public int OptionsPerSlot { get; set; }

        public IList<BoardingHouseScheduleOption> BoardingHouses { get; set; }

        // Display only (not posted): the Coaches' fixtures in the
        // range, and the most match players on any one day
        public IList<ScheduledSportViewModel> ScheduledSports { get; set; }
        public int PeakMatchPlayers { get; set; }

        public ScheduleMenuInputViewModel()
        {
            ScheduledSports = new List<ScheduledSportViewModel>();
            BoardingHouses = new List<BoardingHouseScheduleOption>();
            OptionsPerSlot = 3;
        }
    }

    public class MenuScheduleItemDisplay
    {
        public int Id { get; set; }

        public int MenuItemId { get; set; }

        public NutritionCategory NutritionCategory { get; set; }

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
        public bool ChefChallengeRequested { get; set; }

        public string ChefChallengeReason { get; set; }

        public DateTime? ChefChallengeDate { get; set; }

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

    // ============================================================
    // Coach — My Sports (CoachSquad/Index): each sport's players,
    // from students' profiles, and its upcoming fixtures
    // ============================================================

    public class SportSquadViewModel
    {
        public SportSquadViewModel()
        {
            Players = new List<SquadPlayerViewModel>();
            Upcoming = new List<SquadFixtureViewModel>();
        }

        public string Sport { get; set; }
        public SportArchetype Archetype { get; set; }
        public List<SquadPlayerViewModel> Players { get; set; }
        public List<SquadFixtureViewModel> Upcoming { get; set; }

        public int AvailableCount
        {
            get { return Players.Count(p => p.IsAvailableToday); }
        }
    }

    public class SquadPlayerViewModel
    {
        // StudentSportStatus.Id — what Mark (un)available posts
        public int StatusId { get; set; }
        public string Name { get; set; }
        public string StudentNumber { get; set; }
        public string Grade { get; set; }
        public string House { get; set; }
        public bool IsAvailableToday { get; set; }
        public string StatusReason { get; set; }
        public DateTime? UnavailableUntil { get; set; }
    }

    public class SquadFixtureViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string EventType { get; set; }
        public string Label { get; set; }
        public string House { get; set; }
        public int Players { get; set; }
    }
}