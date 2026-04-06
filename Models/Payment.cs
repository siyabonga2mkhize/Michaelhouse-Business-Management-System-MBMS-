using System;
using System.ComponentModel.DataAnnotations;
<<<<<<< HEAD
=======
using System.ComponentModel.DataAnnotations.Schema;
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f

namespace Michaelhouse.Models
{
    public class Payment
    {
<<<<<<< HEAD
=======
        [Key]
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f
        public int PaymentId { get; set; }

        [ForeignKey("Invoice")]
        public int InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; }

        public decimal AmountPaid { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // Stripe charge ID
        public string StripeChargeId { get; set; }

        // Stripe payment intent ID (for newer Stripe API)
        public string StripePaymentIntentId { get; set; }

        public string Status { get; set; } // "Success", "Failed", "Refunded"

        // Reference for proof of payment
        public string PaymentReference { get; set; }

        // Was proof of payment emailed?
        public bool ProofEmailSent { get; set; }
    }
}