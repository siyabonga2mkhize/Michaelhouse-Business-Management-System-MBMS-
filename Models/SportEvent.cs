using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class SportEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Sport { get; set; }

        // "Training" or "Match"
        [Required]
        [StringLength(20)]
        public string EventType { get; set; }

        [Required]
        public DateTime ScheduledDate { get; set; }

        public TimeSpan StartTime { get; set; }

        [Range(0, 600)]
        public int DurationMinutes { get; set; }

        // "Light", "Moderate", "High"
        [Required]
        [StringLength(20)]
        public string Intensity { get; set; }

        [Required]
        [ForeignKey("Residence")]
        public int ResidenceId { get; set; }

        // Null for training sessions
        [StringLength(200)]
        public string Opponent { get; set; }

        public bool IsHome { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public int CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsCancelled { get; set; }

        /// <summary>
        /// One of: "Scheduled", "Postponed", "Cancelled".
        /// </summary>
        [StringLength(20)]
        public string Status { get; set; }

        public virtual Residence Residence { get; set; }

        public SportEvent()
        {
            ScheduledDate = DateTime.Today;
            StartTime = new TimeSpan(16, 0, 0);
            DurationMinutes = 90;
            Intensity = "Moderate";
            IsHome = true;
            CreatedAt = DateTime.UtcNow;
            IsCancelled = false;
            Status = "Scheduled";
        }
    }
}