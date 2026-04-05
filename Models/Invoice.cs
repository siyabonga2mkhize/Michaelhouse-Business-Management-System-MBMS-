using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [Required]
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
        // "RegistrationFee"         — ZAR 950 (non-refundable, paid before registration)
        // "AnnualFee"               — ZAR 417,000 (board and tuition)
        // "DevelopmentLevy"         — ZAR 6,800 (voluntary)
        // "AnnualFeeAdvanceDiscount" — ZAR -16,680 (discount if paid in full upfront)

        [Required]
        public decimal Amount { get; set; }

        public string Description { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime DueDate { get; set; }

        public string Status { get; set; } = "Pending"; // Pending / Paid / Overdue / Waived

        // Navigation
        public virtual ICollection<Payment> Payments { get; set; }
    }
}
