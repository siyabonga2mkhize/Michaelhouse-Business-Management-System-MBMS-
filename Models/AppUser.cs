using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class AppUser
    {
        [Key]
        public int UserId { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Required, MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public string Role { get; set; } // "Admin" or "Parent"
    }
}