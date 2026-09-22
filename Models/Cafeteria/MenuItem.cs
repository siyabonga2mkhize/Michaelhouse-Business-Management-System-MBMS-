using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Michaelhouse.Models.Cafeteria
{
    public class MenuItem
    {
        public MenuItem()
        {
            ScheduleItems = new HashSet<MenuScheduleItem>();
            SubstitutionScheduleItems = new HashSet<MenuScheduleItem>();
        }

        [Key]
        public int Id { get; set; }

        [ForeignKey("Recipe")]
        public int? RecipeId { get; set; }

        public virtual Recipe Recipe { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(100)]
        public string DietaryClassification { get; set; }

        // ------------------------------------------------------------
        // Meal-slot eligibility
        // ------------------------------------------------------------

        public bool IsBreakfastItem { get; set; }

        public bool IsLunchItem { get; set; }

        public bool IsDinnerItem { get; set; }

        // ------------------------------------------------------------
        // Produce / nutrition
        // ------------------------------------------------------------

        public bool IsFarmGrownProduce { get; set; }

        public NutritionCategory NutritionCategory { get; set; }

        [Range(0, 10000)]
        public decimal CaloriesPerPortion { get; set; }

        [Range(0, 10000)]
        public decimal ProteinGramsPerPortion { get; set; }

        // ------------------------------------------------------------
        // Available portions
        // ------------------------------------------------------------

        [Range(0, 1000000)]
        public int FarmAvailablePortions { get; set; }

        [Range(0, 1000000)]
        public int ExternalAvailablePortions { get; set; }

        // ------------------------------------------------------------
        // Status
        // ------------------------------------------------------------

        public bool IsActive { get; set; }

        // ------------------------------------------------------------
        // Navigation
        // ------------------------------------------------------------

        public virtual ICollection<MenuScheduleItem> ScheduleItems { get; set; }

        public virtual ICollection<MenuScheduleItem> SubstitutionScheduleItems { get; set; }
    }
}