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