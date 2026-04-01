using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class AdminReview
    {
        [Key]
        public int ReviewId { get; set; }

        [ForeignKey("Application")]
        public int AppId { get; set; }

        [Required]
        public string AdminId { get; set; } // ASP.NET Identity UserId

        public DateTime Date { get; set; } = DateTime.UtcNow;

        [Required]
        public string Decision { get; set; } // "Approved" | "Rejected" | "Waitlisted"


        [DataType(DataType.MultilineText)]
        public string AdminNotes { get; set; }

        // Did admin agree with AI?
        public bool AgreedWithAi { get; set; }

        // Navigation
        public virtual Application Application { get; set; }
    }
}