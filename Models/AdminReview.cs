using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class AdminReview
    {
        [Key]
        public int ReviewId { get; set; }

        // Application review (nullable so row can represent either a student app or a driver app)
        [ForeignKey("Application")]
        public int? AppId { get; set; }

        // link to a DriverApplication when review is for a driver
        [ForeignKey("DriverApplication")]
        public int? DriverAppId { get; set; }

        [Required]
        public string AdminId { get; set; } // ASP.NET Identity UserId

        public DateTime? Date { get; set; } = DateTime.UtcNow;

        [Required]
        public string Decision { get; set; } // "Approved" | "Rejected" | "Waitlisted"

        [DataType(DataType.MultilineText)]
        public string AdminNotes { get; set; }

        public bool AgreedWithAi { get; set; }

        // Navigation
        public virtual Application Application { get; set; }
        public virtual DriverApplication DriverApplication { get; set; }
    }
}