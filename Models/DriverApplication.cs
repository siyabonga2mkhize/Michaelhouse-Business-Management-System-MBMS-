using Michaelhouse.Controllers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
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
    }
}