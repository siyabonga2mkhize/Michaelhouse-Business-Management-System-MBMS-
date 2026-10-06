using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC19 — Colour and texture tags for a meal in the meal library.
    //
    // The SRS says meals "carry dietary, colour and texture tags".
    // Dietary tags already exist (MenuItem.DietaryClassification and
    // the recipe's ingredient tags). Colour and texture did not, so
    // they are held here, one row per meal, without changing the
    // team's MenuItem table.
    // ============================================================
    public class MenuItemBuffetTag
    {
        public static readonly string[] Colours =
        {
            "Brown", "White", "Cream", "Yellow", "Orange", "Red", "Green", "Purple", "Golden"
        };

        public static readonly string[] Textures =
        {
            "Soft", "Creamy", "Crispy", "Crunchy", "Tender", "Chewy", "Juicy", "Smooth", "Flaky"
        };

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("MenuItem")]
        [Index(IsUnique = true)]
        public int MenuItemId { get; set; }

        public virtual MenuItem MenuItem { get; set; }

        [Required]
        [StringLength(30)]
        public string Colour { get; set; }

        [Required]
        [StringLength(30)]
        public string Texture { get; set; }

        public DateTime UpdatedAt { get; set; }

        public MenuItemBuffetTag()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
