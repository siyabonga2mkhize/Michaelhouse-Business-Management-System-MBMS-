using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC18 — One RSVP response to an event.
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
        }
    }
}