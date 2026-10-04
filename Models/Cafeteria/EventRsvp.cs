using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC18 — One RSVP response to an event.
    //
    // Invited students, parents and staff RSVP through their login
    // (RespondedByUserId, ResponderGroup = Student / Parent / Staff).
    // Anyone else uses the public link and is counted as a guest.
    //
    // Dietary needs: students' come from their StudentProfile;
    // parents and staff give theirs on the RSVP (the fields below,
    // same format as StudentProfile so the same rules apply).
    // ============================================================
    public class EventRsvp
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Event")]
        public int EventId { get; set; }
        public virtual CafeteriaEvent Event { get; set; }

        public int? RespondedByUserId { get; set; }

        [Required]
        [StringLength(150)]
        public string ResponderName { get; set; }

        [StringLength(200)]
        public string ResponderEmail { get; set; }

        [StringLength(30)]
        public string ResponderPhone { get; set; }

        [StringLength(30)]
        public string ResponderGroup { get; set; }

        [Range(0, 20)]
        public int TotalAttendees { get; set; }

        public int StandardCount { get; set; }
        public int VegetarianCount { get; set; }
        public int OtherDietCount { get; set; }

        [StringLength(500)]
        public string DietaryNotes { get; set; }

        // ── Dietary needs given on the RSVP (parents / staff) ────
        [StringLength(50)]
        public string DietaryPreference { get; set; }

        [StringLength(200)]
        public string DietaryPreferenceOther { get; set; }

        [StringLength(500)]
        public string Allergies { get; set; }

        [StringLength(200)]
        public string MedicalDietaryRestrictions { get; set; }

        [StringLength(200)]
        public string MedicalDietaryRestrictionOther { get; set; }

        // ── Meal chosen from the event's meals (when offered) ────
        [ForeignKey("MenuItem")]
        public int? MenuItemId { get; set; }
        public virtual MenuItem MenuItem { get; set; }

        // ── Additional guests (parents / staff, when allowed) ────
        public int GuestCount { get; set; }
        public virtual ICollection<EventRsvpGuest> Guests { get; set; }

        [Required]
        [StringLength(20)]
        public string ResponseStatus { get; set; }

        [StringLength(500)]
        public string Comments { get; set; }

        public DateTime RespondedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public EventRsvp()
        {
            ResponseStatus = "Attending";
            RespondedAt = DateTime.UtcNow;
            Guests = new HashSet<EventRsvpGuest>();
        }
    }
}