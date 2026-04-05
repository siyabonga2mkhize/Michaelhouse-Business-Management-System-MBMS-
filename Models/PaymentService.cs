using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using Michaelhouse.Models;
using Stripe;
using MHSInvoice = Michaelhouse.Models.Invoice;

namespace Michaelhouse.Services
{
    public class PaymentService
    {
        private readonly EmailService _email = new EmailService();
        private readonly InvoiceService _invoices = new InvoiceService();

        public PaymentService()
        {
            //StripeConfiguration.ApiKey =
            //  ConfigurationManager.AppSettings["Stripe:SecretKey"];
            StripeConfiguration.ApiKey = "sk_test_51TGkmpCX1ORzTt6NEmDOc0b3JZfikEKfkgwHx2ulmuep6cuNGTAFlY3WE65A5bUy8BQ2A0HShSCBqze2A4jFGkJ300YdaTPxkN";
        }

        // ─── Process Payment via Stripe ───────────────────────────────────────────

        /// <summary>
        /// Charges the card via Stripe and marks the invoice as paid.
        /// Returns (success, errorMessage, chargeId).
        /// </summary>
        public (bool Success, string Error, string ChargeId) ProcessPayment(
            int invoiceId, string stripeToken, string parentEmail, string parentName)
        {
            using (var db = new DBContextClass())
            {
                var invoice = db.Invoices
                    .Include("Student")
                    .Include("Parent")
                    .FirstOrDefault(i => i.InvoiceId == invoiceId);

                if (invoice == null)
                    return (false, "Invoice not found.", null);

                if (invoice.Status == "Paid")
                    return (false, "This invoice has already been paid.", null);

                try
                {
                    var options = new ChargeCreateOptions
                    {
                        Amount = (long)(invoice.Amount * 100), // Stripe uses cents
                        Currency = "zar",
                        Description = $"{invoice.Description} | {invoice.InvoiceNumber}",
                        Source = stripeToken,
                        ReceiptEmail = parentEmail,
                        Metadata = new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "InvoiceNumber", invoice.InvoiceNumber },
                            { "StudentName",   invoice.Student?.Name ?? "" },
                            { "InvoiceType",   invoice.InvoiceType }
                        }
                    };

                    var service = new ChargeService();
                    var charge = service.Create(options);

                    if (charge.Status == "succeeded")
                    {
                        var reference = GenerateReference();

                        // Mark invoice paid
                        invoice.Status = "Paid";

                        var payment = new Payment
                        {
                            InvoiceId = invoiceId,
                            AmountPaid = invoice.Amount,
                            PaymentDate = DateTime.Now,
                            StripeChargeId = charge.Id,
                            StripePaymentIntentId = charge.PaymentIntentId,
                            PaymentReference = reference,
                            Status = "Success",
                            ProofEmailSent = false
                        };

                        db.Payments.Add(payment);
                        db.SaveChanges();

                        // Send proof of payment email
                        try
                        {
                            SendProofOfPaymentEmail(payment.PaymentId, parentEmail, parentName);
                            payment.ProofEmailSent = true;
                            db.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Proof email failed: {ex.Message}");
                        }

                        return (true, null, charge.Id);
                    }

                    return (false, $"Payment failed: {charge.FailureMessage}", null);
                }
                catch (StripeException ex)
                {
                    return (false, $"Stripe error: {ex.StripeError?.Message ?? ex.Message}", null);
                }
            }
        }

        // ─── Send Proof of Payment Email ──────────────────────────────────────────

        public void SendProofOfPaymentEmail(int paymentId, string toEmail, string toName)
        {
            using (var db = new DBContextClass())
            {
                var payment = db.Payments
                    .Include("Invoice")
                    .Include("Invoice.Student")
                    .Include("Invoice.Parent")
                    .FirstOrDefault(p => p.PaymentId == paymentId);

                if (payment == null) return;

                var invoice = payment.Invoice;
                var subject = $"Payment Confirmation — {invoice.InvoiceNumber} — Michaelhouse";

                var body = BuildProofOfPaymentHtml(payment, invoice, toName);

                SendEmail(toEmail, subject, body);
            }
        }

        // ─── HTML Proof of Payment ────────────────────────────────────────────────

        private string BuildProofOfPaymentHtml(Payment payment, MHSInvoice invoice, string parentName)
        {
            return $@"
<html>
<body style='font-family: Segoe UI, sans-serif; color: #333; max-width: 650px; margin: 0 auto;'>
 
    <!-- Header -->
    <div style='background: #1a3c5e; padding: 28px 32px; display: flex; justify-content: space-between; align-items: center;'>
        <div>
            <h2 style='color: #fff; margin: 0; font-size: 1.4rem;'>Michaelhouse</h2>
            <p style='color: rgba(255,255,255,0.65); margin: 4px 0 0; font-size: 0.85rem;'>Proof of Payment</p>
        </div>
        <div style='background: #e8a020; color: #000; font-weight: 700; padding: 6px 14px; border-radius: 6px; font-size: 0.85rem;'>
            PAID
        </div>
    </div>
 
    <!-- Body -->
    <div style='padding: 32px; background: #fff;'>
        <p>Dear {parentName},</p>
        <p>Thank you. Your payment has been received successfully. Please retain this email as your proof of payment.</p>
 
        <!-- Payment Details Box -->
        <div style='background: #f8f9fa; border: 1px solid #e0e0e0; border-radius: 8px; padding: 24px; margin: 24px 0;'>
            <h3 style='margin: 0 0 16px; color: #1a3c5e; font-size: 1rem;'>Payment Details</h3>
            <table style='width: 100%; border-collapse: collapse;'>
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px 0; color: #666; width: 45%;'>Invoice Number</td>
                    <td style='padding: 10px 0; font-weight: 600;'>{invoice.InvoiceNumber}</td>
                </tr>
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px 0; color: #666;'>Payment Reference</td>
                    <td style='padding: 10px 0; font-weight: 600; font-family: monospace;'>{payment.PaymentReference}</td>
                </tr>
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px 0; color: #666;'>Student Name</td>
                    <td style='padding: 10px 0;'>{invoice.Student?.Name}</td>
                </tr>
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px 0; color: #666;'>Description</td>
                    <td style='padding: 10px 0;'>{invoice.Description}</td>
                </tr>
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px 0; color: #666;'>Payment Date</td>
                    <td style='padding: 10px 0;'>{payment.PaymentDate:dd MMMM yyyy HH:mm}</td>
                </tr>
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px 0; color: #666;'>Payment Method</td>
                    <td style='padding: 10px 0;'>Credit / Debit Card (Stripe)</td>
                </tr>
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px 0; color: #666;'>Transaction ID</td>
                    <td style='padding: 10px 0; font-family: monospace; font-size: 0.85rem;'>{payment.StripeChargeId}</td>
                </tr>
                <tr>
                    <td style='padding: 14px 0 0; color: #1a3c5e; font-weight: 700; font-size: 1rem;'>Amount Paid</td>
                    <td style='padding: 14px 0 0; font-weight: 700; font-size: 1.2rem; color: #1a3c5e;'>
                        ZAR {payment.AmountPaid:N2}
                    </td>
                </tr>
            </table>
        </div>
 
        <p style='color: #888; font-size: 0.88rem;'>
            If you have any queries regarding this payment, please contact the Michaelhouse bursary office
            and quote your payment reference number <strong>{payment.PaymentReference}</strong>.
        </p>
    </div>
 
    <!-- Footer -->
    <div style='background: #f4f6f9; padding: 16px 32px; text-align: center; color: #888; font-size: 0.78rem;'>
        Michaelhouse &bull; Private Bag X1, Balgowan, KwaZulu-Natal, 3275 &bull; +27 (0)33 234 4001<br/>
        This is an automated payment confirmation. Please do not reply directly to this email.
    </div>
</body>
</html>";
        }

        // ─── Email helper ─────────────────────────────────────────────────────────

        private void SendEmail(string toEmail, string subject, string htmlBody)
        {
            var smtpHost = ConfigurationManager.AppSettings["Email:SmtpHost"] ?? "";
            var smtpPort = int.TryParse(ConfigurationManager.AppSettings["Email:SmtpPort"], out int p) ? p : 587;
            var smtpUser = ConfigurationManager.AppSettings["Email:Username"] ?? "";
            var smtpPass = ConfigurationManager.AppSettings["Email:Password"] ?? "";
            var fromEmail = ConfigurationManager.AppSettings["Email:FromAddress"] ?? "";
            var fromName = "Michaelhouse Bursary";

            if (string.IsNullOrEmpty(smtpHost))
            {
                System.Diagnostics.Debug.WriteLine($"[PROOF OF PAYMENT NOT SENT — SMTP not configured]");
                System.Diagnostics.Debug.WriteLine($"To: {toEmail}");
                return;
            }

            try
            {
                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    var msg = new MailMessage
                    {
                        From = new MailAddress(fromEmail, fromName),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };
                    msg.To.Add(toEmail);
                    client.Send(msg);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email send failed: {ex.Message}");
            }
        }

        // ─── Reference generator ──────────────────────────────────────────────────

        private string GenerateReference()
        {
            return $"MHS-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        }
    }
}