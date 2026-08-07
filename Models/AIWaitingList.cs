using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// AI-generated waiting list when no allocation is possible.
    /// </summary>
    public class AIWaitingList
    {
        [Key]
        public int WaitingListId { get; set; }

        [Required]
        public int StudentId { get; set; }

        public DateTime? RequestedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string Reason { get; set; }

        // Optional priority: higher number => higher priority
        public int Priority { get; set; } = 0;

        public bool NotifiedAdmissions { get; set; } = false;

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }
    }
}
