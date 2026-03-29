using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class Payment
	{
        public int PaymentId { get; set; }

        [Display(Name = "Reference Number")]
        public string ReferenceNumber { get; set; }

        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; }

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } // EFT, Card, Cash

        [Display(Name = "Status")]
        public string Status { get; set; } // Confirmed, Pending

        public int InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; }
    }
}
