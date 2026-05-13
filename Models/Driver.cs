using Michaelhouse.Controllers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Driver
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string IDNumber { get; set; }

        public string PhoneNumber { get; set; }

        public string Email { get; set; }

        public string LicenceNumber { get; set; }

        public DateTime? LicenceExpiryDate { get; set; }

        public bool HasPDP { get; set; }

        public bool IsActive { get; set; } // important

        public DateTime? DateCreated { get; set; }
        public string PasswordHash { get; internal set; }
        public int? UserId { get; set; }
        public virtual AppUser User { get; set; }

        // NEW: image support
        public string ImageUrl { get; set; }   // relative path or full URL


    }
    public class DriverApplication
    {
        public int Id { get; set; }
        [Required]
        public string FullName { get; set; }
        [Required]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "ID Number must be exactly 13 digits")]
        public string IDNumber { get; set; }
        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone Number must be exactly 10 digits")]
        public string PhoneNumber { get; set; }
        [Required]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; }

        // NEW IMPORTANT FIELDS 👇🏾
        [Required]
        [RegularExpression(@"^\d{10,13}$", ErrorMessage = "Licence Number must be 10-13 digits only")]
        public string LicenceNumber { get; set; }
        [Required]
        public DateTime LicenceExpiryDate { get; set; }
        [Required]
        public bool HasPDP { get; set; }

        public string DocumentPath { get; set; }

        public string Status { get; set; } // Pending / Approved / Rejected
        public string AdminNotes { get; set; }

        public DateTime? DateSubmitted { get; set; }
        public virtual ICollection<DriverDocument> Documents { get; set; }
        public DateTime? ReviewedDate { get; internal set; }
        public int? UserId { get; set; }
        public virtual AppUser User { get; set; }
        // Hashed public token and expiry for unauthenticated applicants to view/edit their submission
        public string PublicTokenHash { get; set; }
        public DateTime? PublicTokenExpiry { get; set; }

        public DateTime? InterviewDateTime { get; set; }      // when the interview is scheduled
        public string InterviewMeetingLink { get; set; }      // Jitsi/Google Meet URL
        public bool InterviewEmailSent { get; set; }          // to track if email already sent
        public DateTime? InterviewEmailSentAt { get; set; }
        public string AiReviewSummary { get; set; }
        public string AiRecommendation { get; set; }  // "APPROVE" / "FLAG" / "REJECT"
        public int?   AiScore { get; set; }            // 0-100 calculated score
        public bool   AiReviewComplete { get; set; }
        //
        // ── AiScore calculation ───────────────────────────────────────────────────────
        // Scoring criteria (out of 100):
        //   +30  PDP present
        //   +25  Licence valid for > 2 years
        //   +15  Licence valid for 1-2 years
        //   +0   Licence expired or < 1 year
        //   +20  All required documents submitted (ID + Licence)
        //   +10  Only 1 required document missing
        //   +25  AI recommendation = APPROVE
        //   +10  AI recommendation = FLAG
        //   +0   AI recommendation = REJECT
        // Total possible: 100

        // ── DriverCriteria (transport manager sets the bar) ───────────────────────────
        // These are stored in the DB and shown on the AI review to let it
        // apply school-specific rules consistently.
        //
        // Default criteria:
        //   - Must have a valid driver's licence (not expired)
        //   - PDP is preferred but not mandatory (weighted in scoring)
        //   - Must submit ID document and driver's licence copy
        //   - Must not have criminal convictions (checked via document integrity)
    }
    public class DriverAvailability
    {
        public int Id { get; set; }

        public int DriverId { get; set; }
        public virtual Driver Driver { get; set; }
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
        [Required]
        public string Reason { get; set; }

        public DateTime? DateCreated { get; set; }
    }
}