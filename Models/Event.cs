using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    /// <summary>
    /// This stores special events that require catering.
    /// Examples: school plays, sports days, parent evenings.
    /// The Meal Coordinator schedules these events.
    /// </summary>
    public class Event
    {
        [Key]
        public int EventID { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [MaxLength(20)]
        public string Time { get; set; }

        [MaxLength(200)]
        public string Location { get; set; }

        [Display(Name = "Expected Guests")]
        public int ExpectedGuests { get; set; }

        [Display(Name = "Meal Type")]
        public string MealType { get; set; }

        [Display(Name = "Special Requirements")]
        public string SpecialRequirements { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Planned";
        // Status can be: Planned, Confirmed, Completed, Cancelled

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}