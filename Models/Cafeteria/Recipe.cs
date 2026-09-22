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

        public bool IsActive { get; set; }

        public virtual ICollection<RecipeIngredient> RecipeIngredients { get; set; }

        public virtual ICollection<MenuItem> MenuItems { get; set; }
    }
}