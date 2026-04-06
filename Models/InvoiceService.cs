using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Michaelhouse.Models;


namespace Michaelhouse.Services
{
    public class InvoiceService
    {
        // ─── Michaelhouse Fee Structure ───────────────────────────────────────────
        public const decimal RegistrationFee = 950m;       // Non-refundable, paid BEFORE registration
        public const decimal AnnualFee = 417000m;    // Board and Tuition E to A Block 2026
        public const decimal DevelopmentLevy = 6800m;      // Voluntary Development Levy
        public const decimal AdvancePaymentDiscount = 16680m;   // Discount for full upfront payment

        // ─── Create Registration Fee Invoice ─────────────────────────────────────

        /// <summary>
        /// Called when admin approves an application.
        /// Creates the ZAR 950 non-refundable registration fee invoice.
        /// Parent must pay this BEFORE completing the registration form.
        /// </summary>
        public Invoice CreateRegistrationFeeInvoice(int registrationId, int studentId, int parentId)
        {
            using (var db = new DBContextClass())
            {
                // Don't create duplicate
                var existing = db.Invoices.FirstOrDefault(i =>
                    i.RegistrationId == registrationId &&
                    i.InvoiceType == "RegistrationFee");

                if (existing != null) return existing;

                var invoice = new Invoice
                {
                    InvoiceNumber = GenerateInvoiceNumber("REG", db),
                    RegistrationId = registrationId,
                    StudentId = studentId,
                    ParentId = parentId,
                    InvoiceType = "RegistrationFee",
                    Amount = RegistrationFee,
                    Description = "Non-Refundable Registration Fee — Michaelhouse",
                    CreatedDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(7),
                    Status = "Pending"
                };

                db.Invoices.Add(invoice);
                db.SaveChanges();
                return invoice;
            }
        }

        /// <summary>
        /// Called after parent completes registration.
        /// Creates Annual Fee, Development Levy invoices.
        /// </summary>
        public List<Invoice> CreateAnnualFeeInvoices(
            int registrationId, int studentId, int parentId,
            bool includeDevLevy = true, bool advancePayment = false)
        {
            var invoices = new List<Invoice>();

            using (var db = new DBContextClass())
            {
                // Annual Fee
                if (!db.Invoices.Any(i => i.RegistrationId == registrationId && i.InvoiceType == "AnnualFee"))
                {
                    var annual = new Invoice
                    {
                        InvoiceNumber = GenerateInvoiceNumber("ANN", db),
                        RegistrationId = registrationId,
                        StudentId = studentId,
                        ParentId = parentId,
                        InvoiceType = "AnnualFee",
                        Amount = AnnualFee,
                        Description = "Annual Board and Tuition (E to A Block) — 2026",
                        CreatedDate = DateTime.Now,
                        DueDate = new DateTime(DateTime.Now.Year, 1, 31),
                        Status = "Pending"
                    };
                    db.Invoices.Add(annual);
                    invoices.Add(annual);
                }

                // Development Levy (optional)
                if (includeDevLevy &&
                    !db.Invoices.Any(i => i.RegistrationId == registrationId && i.InvoiceType == "DevelopmentLevy"))
                {
                    var levy = new Invoice
                    {
                        InvoiceNumber = GenerateInvoiceNumber("LEV", db),
                        RegistrationId = registrationId,
                        StudentId = studentId,
                        ParentId = parentId,
                        InvoiceType = "DevelopmentLevy",
                        Amount = DevelopmentLevy,
                        Description = "Voluntary Development Levy — 2026",
                        CreatedDate = DateTime.Now,
                        DueDate = new DateTime(DateTime.Now.Year, 3, 31),
                        Status = "Pending"
                    };
                    db.Invoices.Add(levy);
                    invoices.Add(levy);
                }

                // Advance Payment Discount (if paying full annual fee upfront)
                if (advancePayment &&
                    !db.Invoices.Any(i => i.RegistrationId == registrationId && i.InvoiceType == "AdvanceDiscount"))
                {
                    var discount = new Invoice
                    {
                        InvoiceNumber = GenerateInvoiceNumber("DIS", db),
                        RegistrationId = registrationId,
                        StudentId = studentId,
                        ParentId = parentId,
                        InvoiceType = "AdvanceDiscount",
                        Amount = -AdvancePaymentDiscount, // Negative — it's a discount
                        Description = "Discount: Annual Fees Paid in Advance",
                        CreatedDate = DateTime.Now,
                        DueDate = DateTime.Now,
                        Status = "Waived"
                    };
                    db.Invoices.Add(discount);
                    invoices.Add(discount);
                }

                db.SaveChanges();
            }

            return invoices;
        }

        // ─── Get invoices for a parent ────────────────────────────────────────────

        public List<Invoice> GetInvoicesForParent(int parentId)
        {
            using (var db = new DBContextClass())
            {
                return db.Invoices
                    .Include("Student")
                    .Include("Payments")
                    .Where(i => i.ParentId == parentId)
                    .OrderByDescending(i => i.CreatedDate)
                    .ToList();
            }
        }

        public List<Invoice> GetInvoicesForRegistration(int registrationId)
        {
            using (var db = new DBContextClass())
            {
                return db.Invoices
                    .Include("Student")
                    .Include("Payments")
                    .Where(i => i.RegistrationId == registrationId)
                    .OrderBy(i => i.InvoiceType)
                    .ToList();
            }
        }

        // ─── Check if registration fee is paid ───────────────────────────────────

        public bool IsRegistrationFeePaid(int registrationId)
        {
            using (var db = new DBContextClass())
            {
                return db.Invoices.Any(i =>
                    i.RegistrationId == registrationId &&
                    i.InvoiceType == "RegistrationFee" &&
                    i.Status == "Paid");
            }
        }

        // ─── Mark invoice as paid ────────────────────────────────────────────────

        public void MarkInvoiceAsPaid(int invoiceId, string stripeChargeId, string reference)
        {
            using (var db = new DBContextClass())
            {
                var invoice = db.Invoices.Find(invoiceId);
                if (invoice == null) return;

                invoice.Status = "Paid";

                db.Payments.Add(new Payment
                {
                    InvoiceId = invoiceId,
                    AmountPaid = invoice.Amount,
                    PaymentDate = DateTime.Now,
                    StripeChargeId = stripeChargeId,
                    PaymentReference = reference,
                    Status = "Success"
                });

                db.SaveChanges();
            }
        }

        // ─── Invoice number generator ─────────────────────────────────────────────

        private string GenerateInvoiceNumber(string prefix, DBContextClass db)
        {
            int count = db.Invoices.Count() + 1;
            return $"MHS-{prefix}-{DateTime.Now.Year}-{count:D5}";
        }
    }
}