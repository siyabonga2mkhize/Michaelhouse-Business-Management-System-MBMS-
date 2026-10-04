using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — Physical spaces an event can be held in.
    // Used for conflict-checking: two events can't share a venue
    // at overlapping times.
    // ============================================================
    public class EventVenue
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public int Capacity { get; set; }

        [StringLength(200)]
        public string Location { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public bool IsActive { get; set; }

        public EventVenue()
        {
            IsActive = true;
        }
    }
}