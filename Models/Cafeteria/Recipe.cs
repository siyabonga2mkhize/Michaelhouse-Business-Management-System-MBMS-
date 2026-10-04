using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.Cafeteria
{
    public class Recipe
    {
        public Recipe()
        {
            RecipeIngredients = new HashSet<RecipeIngredient>();
            MenuItems = new HashSet<MenuItem>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [StringLength(1000)]
        public string PreparationNotes { get; set; }

        [Range(1, 100000)]
        public int StandardPortionCount { get; set; }
        
        /// <summary>
        /// Time to prep this dish before cooking starts (chopping, marinating, thawing), in minutes.
        /// </summary>
        [Range(0, 1440)]
        public int PrepTimeMinutes { get; set; }

        /// <summary>
        /// Active cooking time, in minutes.
        /// </summary>
        [Range(0, 1440)]
        public int CookTimeMinutes { get; set; }

        /// <summary>
        /// Which kitchen station handles this dish: Grill, Cold Prep, Bakery, Stove, Line, etc.
        /// </summary>
        [StringLength(50)]
        public string Station { get; set; }

       
        public bool IsActive { get; set; }

        public virtual ICollection<RecipeIngredient> RecipeIngredients { get; set; }

        public virtual ICollection<MenuItem> MenuItems { get; set; }
    }
}