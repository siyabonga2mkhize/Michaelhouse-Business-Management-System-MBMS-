using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — Reusable menu template ("Sports Day Braai").
    // When scheduling an event, picking a template pre-loads
    // its items into the event's menu at UC19 time.
    // ============================================================
    public class EventMenuTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        // If set, pre-selects this EventType when the template is chosen
        public EventType? DefaultEventType { get; set; }

        public int DefaultHeadcount { get; set; }
        public int MinGuests { get; set; }
        public int MaxGuests { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<EventMenuTemplateItem> Items { get; set; }

        public EventMenuTemplate()
        {
            IsActive = true;
            Items = new HashSet<EventMenuTemplateItem>();
        }
    }
}