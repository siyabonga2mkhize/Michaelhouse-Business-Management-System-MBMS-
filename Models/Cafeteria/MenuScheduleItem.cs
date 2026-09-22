using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    public enum MealSlot
    {
        Breakfast = 1,
        Lunch = 2,
        Dinner = 3
    }

    public enum ItemTagStatus
    {
        Confirmed = 1,
        NeedsSubstitution = 2,
        NeedsReview = 3
    }

    public class MenuScheduleItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("MealMenu")]
        public int MealMenuId { get; set; }

        [Required]
        [ForeignKey("MenuItem")]
        public int MenuItemId { get; set; }

        /*
         * If a manager manually substitutes an item, this points to
         * the replacement item.
         */
        [ForeignKey("SubstitutionMenuItem")]
        public int? SubstitutionMenuItemId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public MealSlot MealSlot { get; set; }

        [Range(1, 1000000)]
        public int CalculatedPortions { get; set; }

        [Required]
        public ItemTagStatus ItemTagStatus { get; set; }

        [StringLength(2000)]
        public string TagReason { get; set; }

        public virtual MealMenu MealMenu { get; set; }

        public virtual MenuItem MenuItem { get; set; }

        public virtual MenuItem SubstitutionMenuItem { get; set; }
    }
}