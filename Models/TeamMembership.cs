using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    public class TeamMembership
    {
        [Key]
        public int TeamMembershipId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        [ForeignKey("Team")]
        public int TeamId { get; set; }
        public virtual Team Team { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime? EndDate { get; set; }

        // Enum status — stored as int in DB
        public TeamMembershipStatus Status { get; set; } = TeamMembershipStatus.Pending;

        // Legacy string status — kept for compatibility with any old code
        [NotMapped]
        public string StatusString
        {
            get { return Status.ToString(); }
            set
            {
                TeamMembershipStatus parsed;
                if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out parsed))
                    Status = parsed;
            }
        }

        [MaxLength(100)]
        public string ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public bool RequestedByStudent { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}