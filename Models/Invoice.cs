using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class Invoice
	{
        public int InvoiceId { get; set; }

        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "Amount Paid")]
        [DataType(DataType.Currency)]
        public decimal AmountPaid { get; set; }

        [Display(Name = "Balance")]
        public decimal Balance => Amount - AmountPaid;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Display(Name = "Issue Date")]
        [DataType(DataType.Date)]
        public DateTime IssueDate { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Unpaid, Partial, Paid

        [Display(Name = "Category")]
        public string Category { get; set; } // Tuition, Boarding, Transport, Cafeteria

        public int StudentId { get; set; }
        public virtual Student Student { get; set; }
    }
}