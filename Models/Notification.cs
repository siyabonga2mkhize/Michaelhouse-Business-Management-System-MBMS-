using System;

namespace Michaelhouse.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string RelatedEntityType { get; set; } // e.g., 'TripRequest', 'TripSchedule'
        public int RelatedEntityId { get; set; }
        public int? ResidenceId { get; set; }

        // Navigation property
        public virtual AppUser User { get; set; }
        public string Type { get; internal set; }
    }
}
