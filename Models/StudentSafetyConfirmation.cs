using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class StudentSafetyConfirmation
    {
        [Key]
        public int ConfirmationId { get; set; }

        [Required]
        public int AlertId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public DateTime ConfirmationTime { get; set; }

        // Student's location when they confirmed
        [Required]
        public double StudentLatitude { get; set; }

        [Required]
        public double StudentLongitude { get; set; }

        // Was student within the geofence?
        [Required]
        public bool WithinGeofence { get; set; }

        // Distance from assembly point in meters
        public double DistanceFromAssemblyPointMeters { get; set; }

        // Calculated status
        [Required]
        public SafetyStatus Status { get; set; } = SafetyStatus.Confirmed;

        // Navigation properties
        [ForeignKey("AlertId")]
        public virtual EmergencyAlert Alert { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }
    }

    public enum SafetyStatus
    {
        Confirmed = 0,        // Student is safe and within zone
        OutsideZone = 1,      // Student confirmed but outside geofence
        Pending = 2,          // Alert sent, student hasn't confirmed yet
        NoResponse = 3        // Alert sent, no response
    }
}