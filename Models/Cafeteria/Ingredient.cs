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
        /// <summary>
        /// Comma-separated allergen tags. Possible values:
        /// Nuts, Dairy, Gluten, Egg, Soy, Fish, Shellfish, Sesame.
        /// Empty if the ingredient contains none of these.
        /// </summary>
        [StringLength(200)]
        public string Allergens { get; set; }

        /// <summary>
        /// Comma-separated dietary tags used for dietary preferences
        /// (vegetarian, vegan, halal). Possible values:
        /// Meat, Poultry, Pork, Gelatin, Honey, Alcohol, Halal.
        /// "Halal" marks a Meat / Poultry / Gelatin ingredient as halal-certified.
        /// Fish, Shellfish, Egg and Dairy are NOT repeated here — they are
        /// read from Allergens.
        /// </summary>
        [StringLength(200)]
        public string DietaryTags { get; set; }

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

        // Optional: the level to order back up to (cafeteria inventory)
        [Range(0, 1000000000)]
        public decimal? TargetStockLevel { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<RecipeIngredient> RecipeIngredients { get; set; }
    }
}