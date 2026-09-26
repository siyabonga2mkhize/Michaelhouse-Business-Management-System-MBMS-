using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class CoachRecommendation
    {
        [Key]
        public int RecommendationID { get; set; }

        [ForeignKey("Coach")]
        public int CoachID { get; set; }
        public virtual Coach Coach { get; set; }

        [ForeignKey("Team")]
        public int TeamID { get; set; }
        public virtual Team Team { get; set; }

        [Required, MaxLength(500)]
        public string RecommendationText { get; set; }

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}