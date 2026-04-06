using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models.ViewModels
{
    // ─── Login ────────────────────────────────────────────────────────────────────
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    // ─── Register ─────────────────────────────────────────────────────────────────
    public class RegisterViewModel
    {
        [Required, MaxLength(200)]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }

        [Required, MaxLength(20)]
        [Display(Name = "Cell Phone Number")]
        public string CellPhone { get; set; }

        [MaxLength(100)]
        [Display(Name = "Relationship to Student")]
        public string Relationship { get; set; }
    }

    // ─── Edit Parent Profile ──────────────────────────────────────────────────────
    public class ParentProfileViewModel
    {
        [Required, MaxLength(200)]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Required, MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Contact { get; set; }

        [MaxLength(20)]
        [Display(Name = "Cell Phone")]
        public string CellPhone { get; set; }

        [MaxLength(20)]
        [Display(Name = "Work Phone")]
        public string WorkPhone { get; set; }

        [MaxLength(20)]
        [Display(Name = "Home Phone")]
        public string HomePhone { get; set; }

        [MaxLength(300)]
        [Display(Name = "Physical Address")]
        public string PhysicalAddress { get; set; }

        [MaxLength(300)]
        [Display(Name = "Postal Address")]
        public string PostalAddress { get; set; }

        [MaxLength(100)]
        [Display(Name = "Relationship to Student")]
        public string Relationship { get; set; }

        [MaxLength(200)]
        [Display(Name = "Occupation")]
        public string Occupation { get; set; }

        [MaxLength(200)]
        [Display(Name = "Employer / Company")]
        public string Employer { get; set; }

        [MaxLength(200)]
        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; }

        [MaxLength(20)]
        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; }
    }

    // ─── Add Student ──────────────────────────────────────────────────────────────
    public class AddStudentViewModel
    {
        [Required, MaxLength(200)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DOB { get; set; }

        [MaxLength(50)]
        [Display(Name = "Home Language")]
        public string HomeLanguage { get; set; }

        [MaxLength(20)]
        [Display(Name = "ID / Passport Number")]
        public string IdNumber { get; set; }

        [MaxLength(200)]
        [Display(Name = "Previous School")]
        public string PreviousSchool { get; set; }

        [MaxLength(50)]
        [Display(Name = "Current Grade")]
        public string CurrentGrade { get; set; }

        [MaxLength(500)]
        [Display(Name = "Medical Conditions / Allergies")]
        public string MedicalConditions { get; set; }
    }

    // ─── Submit Application ───────────────────────────────────────────────────────
    public class ApplicationCreateViewModel
    {
        [Required]
        [Display(Name = "Student")]
        public int StudentId { get; set; }

        [Required]
        [Display(Name = "Application Year")]
        public int ApplicationYear { get; set; }

        [Required]
        [Display(Name = "Grade Applying For")]
        public int GradeApplying { get; set; } // 8, 9, 10, 11, 12

        [DataType(DataType.MultilineText)]
        [Display(Name = "Additional Information")]
        public string AdditionalNotes { get; set; }

        public string[] DocumentTypes { get; set; }
    }

    // ─── Admin Confirm ────────────────────────────────────────────────────────────
    public class AdminConfirmViewModel
    {
        public int AppId { get; set; }

        [Required]
        public string Decision { get; set; }

        [DataType(DataType.MultilineText)]
        public string Notes { get; set; }

        public bool AgreedWithAi { get; set; }
    }

    //Checkout ViewModel
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string CustomerEmail { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Notes (Optional)")]
        public string Notes { get; set; } = string.Empty;
    }
}