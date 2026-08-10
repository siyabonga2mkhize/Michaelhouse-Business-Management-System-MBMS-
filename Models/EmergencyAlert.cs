using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class EmergencyAlert
    {
        [Key]
        public int AlertId { get; set; }

        [Required]
        public int InitiatedByStaffId { get; set; } // Staff/Admin ID who triggered alert

        [Required]
        public DateTime AlertTime { get; set; }

        [Required]
        [MaxLength(500)]
        public string AlertMessage { get; set; } = "EMERGENCY - Proceed to assembly point immediately";

        // Assembly point location coordinates
        [Required]
        public double AssemblyLatitude { get; set; }

        [Required]
        public double AssemblyLongitude { get; set; }

        [MaxLength(100)]
        public string AssemblyPointName { get; set; } = "Assembly Point";

        [Required]
        public double GeofenceRadiusMeters { get; set; } = 150; // Default 150m radius

        [Required]
        public AlertStatus Status { get; set; } = AlertStatus.Active;

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ResolvedDate { get; set; }

        [MaxLength(500)]
        public string ResolvedNotes { get; set; }

        // Navigation property
        [ForeignKey("InitiatedByStaffId")]
        public virtual AppUser Staff { get; set; }

        public virtual ICollection<StudentSafetyConfirmation> StudentConfirmations { get; set; }
            = new List<StudentSafetyConfirmation>();
    }

    public enum AlertStatus
    {
        Active = 0,
        Resolved = 1,
        Cancelled = 2
    }
}