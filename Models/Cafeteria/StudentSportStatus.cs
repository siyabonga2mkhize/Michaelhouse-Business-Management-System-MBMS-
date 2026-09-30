using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC12 rework — Tracks which student plays which sport and
    // whether they're currently available.
    //
    // A student can have more than one row (e.g. rugby + cricket).
    // The Coach owns this data.
    // ============================================================

    public class StudentSportStatus
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [Required]
        [StringLength(50)]
        public string Sport { get; set; }

        [Required]
        public SportArchetype Archetype { get; set; }

        /// <summary>
        /// True when the student is actively training / available for selection.
        /// False when injured, suspended, or on leave.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Null for "out indefinitely". Set for a specific return date.
        /// </summary>
        public DateTime? UnavailableUntil { get; set; }

        [StringLength(300)]
        public string StatusReason { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public virtual Student Student { get; set; }

        public StudentSportStatus()
        {
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }
    }
}