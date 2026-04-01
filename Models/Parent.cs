using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class Parent
    {
        [Key]
        public int ParentId { get; set; }

        // ─── Linked User Account ──────────────────────────────────────────────────
        public int? UserId { get; set; }
        public virtual AppUser User { get; set; }

        // ─── Personal Information ─────────────────────────────────────────────────
        [Required, MaxLength(200)]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Email Address")]
        public string Contact { get; set; }

        [MaxLength(20)]
        [Display(Name = "Cell Phone Number")]
        public string CellPhone { get; set; }

        [MaxLength(20)]
        [Display(Name = "Work Phone Number")]
        public string WorkPhone { get; set; }

        [MaxLength(20)]
        [Display(Name = "Home Phone Number")]
        public string HomePhone { get; set; }

        // ─── Address ──────────────────────────────────────────────────────────────
        [MaxLength(300)]
        [Display(Name = "Physical Address")]
        public string PhysicalAddress { get; set; }

        [MaxLength(300)]
        [Display(Name = "Postal Address")]
        public string PostalAddress { get; set; }

        // ─── Relationship & Occupation ────────────────────────────────────────────
        [MaxLength(100)]
        [Display(Name = "Relationship to Student")]
        public string Relationship { get; set; } // e.g. Father, Mother, Guardian

        [MaxLength(200)]
        [Display(Name = "Occupation")]
        public string Occupation { get; set; }

        [MaxLength(200)]
        [Display(Name = "Employer / Company")]
        public string Employer { get; set; }

        // ─── Emergency Contact ────────────────────────────────────────────────────
        [MaxLength(200)]
        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; }

        [MaxLength(20)]
        [Display(Name = "Emergency Contact Number")]
        public string EmergencyContactPhone { get; set; }

        // ─── Navigation ───────────────────────────────────────────────────────────
        public virtual ICollection<Student> Students { get; set; }
    }
}