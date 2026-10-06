using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC18 — One additional guest on a parent's or staff member's
    // RSVP. Guests have no profile in the system, so the person
    // RSVP'ing gives their dietary group (and meal, when the event
    // offers a choice) here. Part of the RSVP — not a meal plan.
    // ============================================================
    public class EventRsvpGuest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Rsvp")]
        public int RsvpId { get; set; }
        public virtual EventRsvp Rsvp { get; set; }

        // None / Vegetarian / Vegan / Halal / Other
        // (DietaryProfileService.PreferenceOptions)
        [StringLength(50)]
        public string DietaryPreference { get; set; }

        // Details when the guest's requirement is "Other"
        [StringLength(200)]
        public string DietaryNotes { get; set; }

        // UC18 step 6 — optional cultural favourite dish for this guest.
        // UC19 step 3 matches it to the meal library and checks stock.
        [StringLength(150)]
        public string CulturalFavoriteDish { get; set; }

        // Their meal, when the event offers a choice
        [ForeignKey("MenuItem")]
        public int? MenuItemId { get; set; }
        public virtual MenuItem MenuItem { get; set; }
    }
}
