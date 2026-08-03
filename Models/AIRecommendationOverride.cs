using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class AIRecommendationOverride
    {
        [Key]
        public int AIRecommendationOverrideId { get; set; }

        public int RecommendationId { get; set; }

        public double OriginalScore { get; set; }

        public int AdminUserId { get; set; }
        public string AdminName { get; set; }

        [MaxLength(1000)]
        public string OverrideReason { get; set; }

        public int NewResidenceId { get; set; }
        public int NewRoomId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("RecommendationId")]
        public virtual AIResidenceRecommendation Recommendation { get; set; }
    }
}
