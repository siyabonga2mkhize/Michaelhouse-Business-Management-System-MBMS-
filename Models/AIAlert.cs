using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// AI-generated alert for residence management (e.g., waiting list notification, sudden occupancy spike)
    /// </summary>
    public class AIAlert
    {
        [Key]
        public int AIAlertId { get; set; }

        public int ResidenceId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(4000)]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;

        public int? ResolvedByUserId { get; set; }

        public DateTime? ResolvedAt { get; set; }

        [ForeignKey("ResidenceId")]
        public virtual Residence Residence { get; set; }
    }
}