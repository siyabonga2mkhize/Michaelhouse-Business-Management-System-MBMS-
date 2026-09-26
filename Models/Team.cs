using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Team
    {
        [Key]
        public int TeamID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [ForeignKey("Sport")]
        public int? SportId { get; set; }
        public virtual Sport Sport { get; set; }

        [MaxLength(50)]
        public string SportName { get; set; }

        [ForeignKey("Coach")]
        public int? CoachID { get; set; }
        public virtual Coach Coach { get; set; }

        [MaxLength(2000)]
        public string Players { get; set; }

        [MaxLength(20)]
        public string AgeGroup { get; set; }

        [MaxLength(20)]
        public string Gender { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public virtual ICollection<TeamMembership> TeamMemberships { get; set; } = new List<TeamMembership>();
        public virtual ICollection<SportsActivity> Activities { get; set; } = new List<SportsActivity>();
        public virtual ICollection<CoachMessage> Messages { get; set; } = new List<CoachMessage>();
    }
}