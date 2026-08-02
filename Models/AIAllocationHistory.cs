using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Records every allocation decision for auditing and future ML analysis.
    /// Records MUST NOT be deleted.
    /// </summary>
    public class AIAllocationHistory
    {
        [Key]
        public int AllocationHistoryId { get; set; }

        [Required]
        public int StudentId { get; set; }

        public int ResidenceId { get; set; }

        public int RoomId { get; set; }

        /// <summary>
        /// Final compatibility score (0..100) at time of allocation
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Confidence associated with the recommendation (0..1)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Human-readable explanation produced by the ExplanationGenerator.
        /// Stored for audit and ML feature engineering.
        /// </summary>
        [MaxLength(4000)]
        public string Explanation { get; set; }

        /// <summary>
        /// Whether this recommendation/allocation was accepted by staff
        /// </summary>
        public bool Accepted { get; set; }

        /// <summary>
        /// Whether a staff member manually overrode the recommendation
        /// </summary>
        public bool Overridden { get; set; }

        /// <summary>
        /// Optional reason provided by staff when overriding
        /// </summary>
        [MaxLength(1000)]
        public string OverrideReason { get; set; }

        /// <summary>
        /// Point-in-time when this record was created
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation helpers
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("ResidenceId")]
        public virtual Residence Residence { get; set; }

        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; }
    }
}
