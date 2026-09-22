using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    public class RecipeIngredient
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }

        [Required]
        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }

        [Range(0.001, 1000000)]
        public decimal QuantityPerStandardPortion { get; set; }

        [StringLength(500)]
        public string PreparationNotes { get; set; }

        public virtual Recipe Recipe { get; set; }

        public virtual Ingredient Ingredient { get; set; }
    }
}