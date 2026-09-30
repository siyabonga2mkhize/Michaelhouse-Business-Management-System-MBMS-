using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — Which staff member works an event, in what role,
    // for which slice of time.
    // ============================================================
    public class EventStaffAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Event")]
        public int EventId { get; set; }
        public virtual CafeteriaEvent Event { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public EventStaffRole Role { get; set; }

        // Optional working window (defaults to the event's own times)
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public EventStaffAssignment()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }
}