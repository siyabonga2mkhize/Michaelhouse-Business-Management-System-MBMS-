using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        // Foreign Key to the related Invoice
        [ForeignKey("Invoice")]
        public int InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; }

        [Required]
        [DataType(DataType.Currency)]
        public decimal AmountPaid { get; set; }

        [Display(Name = "Payment Date")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // Stripe integration fields
        [Display(Name = "Stripe Charge ID")]
        public string StripeChargeId { get; set; }

        [Display(Name = "Stripe Intent ID")]
        public string StripePaymentIntentId { get; set; }

        // Status: "Success", "Failed", "Refunded"
        [Required]
        public string Status { get; set; }

        [Display(Name = "Reference")]
        public string PaymentReference { get; set; }

        [Display(Name = "Receipt Emailed")]
        public bool ProofEmailSent { get; set; }
    }
}