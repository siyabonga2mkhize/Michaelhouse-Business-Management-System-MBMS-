using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — One buffet meal selected for an event.
    //
    // Links the event to a meal from the Dietitian's meal library
    // (MenuItem → Recipe → RecipeIngredient → Ingredient), so the
    // dietary checks and the feast plan use the same data as the
    // daily menu. Not a separate meal: just "this meal is served at
    // this event".
    //
    // Events without any rows fall back to their menu template.
    // ============================================================
    public class CafeteriaEventMenuItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Event")]
        public int EventId { get; set; }
        public virtual CafeteriaEvent Event { get; set; }

        [Required]
        [ForeignKey("MenuItem")]
        public int MenuItemId { get; set; }
        public virtual MenuItem MenuItem { get; set; }

        // "Main" / "Side" / "Dessert" … — same sections as templates
        [StringLength(50)]
        public string Section { get; set; }

        // Portions to prepare per guest (1.0 = one each)
        public decimal QuantityPerGuest { get; set; }

        public int SortOrder { get; set; }

        public CafeteriaEventMenuItem()
        {
            Section = "Main";
            QuantityPerGuest = 1.0m;
        }
    }
}
