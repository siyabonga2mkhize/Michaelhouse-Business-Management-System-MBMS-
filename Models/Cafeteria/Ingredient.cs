using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.Cafeteria
{
    public class Ingredient
    {
        public Ingredient()
        {
            RecipeIngredients = new HashSet<RecipeIngredient>();
        }

        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(30)]
        public string Unit { get; set; }

        [Range(0, 1000000)]
        public decimal CaloriesPerUnit { get; set; }

        [Range(0, 1000000)]
        public decimal ProteinGramsPerUnit { get; set; }

        [Range(0, 1000000)]
        public decimal CarbohydrateGramsPerUnit { get; set; }

        [Range(0, 1000000)]
        public decimal FatGramsPerUnit { get; set; }

        // ─────────────────────────────────────────────────────────
        // Stock on hand, measured in this ingredient's Unit.
        // Farm-grown / seasonal produce is prioritised; external
        // supplier stock is the fallback.
        // ─────────────────────────────────────────────────────────
        [Range(0, 1000000000)]
        public decimal FarmAvailableQuantity { get; set; }

        [Range(0, 1000000000)]
        public decimal ExternalAvailableQuantity { get; set; }

        [Range(0, 1000000000)]
        public decimal ReorderLevel { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<RecipeIngredient> RecipeIngredients { get; set; }
    }
}