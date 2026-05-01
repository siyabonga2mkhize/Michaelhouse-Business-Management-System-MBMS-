using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

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
    }
}