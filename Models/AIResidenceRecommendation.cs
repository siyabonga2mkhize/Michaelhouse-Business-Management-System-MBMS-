using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Stores an AI-generated residence allocation recommendation for auditing and review.
    /// Explanation should contain human-readable reasons describing why the recommendation
    /// was produced (features, matching factors and any important notes).
    /// </summary>
    public class AIResidenceRecommendation
    {
        [Key]
        public int RecommendationId { get; set; }

        [Required]
        public int StudentId { get; set; }

        public int ResidenceId { get; set; }

        public int RoomId { get; set; }

        /// <summary>
        /// Compatibility score (0..100)
        /// </summary>
        public double CompatibilityScore { get; set; }

        /// <summary>
        /// Confidence score (0..1)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Human-readable explanation of why this recommendation was selected.
        /// </summary>
        [MaxLength(4000)]
        public string Explanation { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether staff accepted the recommendation and committed the allocation.
        /// </summary>
        public bool Accepted { get; set; } = false;

        /// <summary>
        /// Optional: user id of the staff member who accepted the recommendation.
        /// </summary>
        public int? AcceptedBy { get; set; }

        /// <summary>
        /// Version identifier of the algorithm used to generate this recommendation.
        /// Helpful for auditing and rollbacks.
        /// </summary>
        [MaxLength(50)]
        public string AlgorithmVersion { get; set; }

        // Navigation properties (optional)
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("ResidenceId")]
        public virtual Residence Residence { get; set; }

        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; }
    }
}
