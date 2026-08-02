using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Links a student to a residence/room/bed for a period of time.
    /// This is the canonical allocation record used by the AI allocation engine.
    /// </summary>
    public class ResidenceAllocation
    {
        [Key]
        public int ResidenceAllocationId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ResidenceId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public int BedId { get; set; }

        /// <summary>
        /// Whether this allocation is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When the student was allocated to this bed
        /// </summary>
        public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the allocation ended (if released)
        /// </summary>
        public DateTime? ReleasedAt { get; set; }

        // Audit
        [StringLength(200)]
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(200)]
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("ResidenceId")]
        public virtual Residence Residence { get; set; }

        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; }

        [ForeignKey("BedId")]
        public virtual Bed Bed { get; set; }
    }
}
