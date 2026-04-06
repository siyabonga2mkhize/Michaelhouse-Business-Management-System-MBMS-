using System;
using System.Collections.Generic; // Required for ICollection
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [Required]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } // e.g. MHS-2026-00001

        // Linked to registration
        [ForeignKey("Registration")]
        public int RegistrationId { get; set; }
        public virtual Registration Registration { get; set; }

        // Linked to student
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        // Linked to parent who pays
        [ForeignKey("Parent")]
        public int ParentId { get; set; }
        public virtual Parent Parent { get; set; }

        [Required]
        public string InvoiceType { get; set; }
        // Types include:
        // "RegistrationFee"          — ZAR 950
        // "AnnualFee"                — ZAR 417,000
        // "DevelopmentLevy"          — ZAR 6,800
        // "AnnualFeeAdvanceDiscount" — ZAR -16,680

        [Required]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        public string Description { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        public string Status { get; set; } = "Pending"; // Pending / Paid / Overdue / Waived

        // Navigation for related payments
        public virtual ICollection<Payment> Payments { get; set; }
    }
}