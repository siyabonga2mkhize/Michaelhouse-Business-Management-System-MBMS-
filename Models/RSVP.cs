using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// This stores RSVP responses for events.
    /// Students and staff confirm attendance and meal preference.
    /// The system uses this to plan the feast.
    /// </summary>
    public class RSVP
    {
        [Key]
        public int RSVPID { get; set; }

        [Required]
        [ForeignKey("Event")]
        public int EventID { get; set; }
        public virtual Event Event { get; set; }

        [Required]
        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public virtual Student Student { get; set; }

        [Display(Name = "Attending")]
        public bool Attending { get; set; }

        [Display(Name = "Number of People")]
        public int NumberOfPeople { get; set; }

        [MaxLength(200)]
        [Display(Name = "Dietary Needs")]
        public string DietaryNeeds { get; set; }

        [Display(Name = "RSVP Date")]
        public DateTime RSVPDate { get; set; } = DateTime.Now;
    }
}