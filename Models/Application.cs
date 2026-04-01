using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    public class Application
    {
        [Key]
        public int AppId { get; set; }

        [ForeignKey("Parent")]
        public int ParentId { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.UtcNow;

        [Required]
        public int ApplicationYear { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

        // AI Review Summary stored here after AI processes it
        public string AiReviewSummary { get; set; }
        public string AiRecommendation { get; set; } // "Approve" | "Reject" | "Review"

        // Navigation
        public Parent Parent { get; set; }
        public Student Student { get; set; }
        public ICollection<Document> Documents { get; set; }
        public ICollection<AdminReview> AdminReviews { get; set; }
    }
}