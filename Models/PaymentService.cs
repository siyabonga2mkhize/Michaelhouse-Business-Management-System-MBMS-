using Microsoft.EntityFrameworkCore;
using Michaelhouse.Infrastructure;
﻿using System;
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
            StripeConfiguration.ApiKey = AppConfig.AppSettings("Stripe:SecretKey");
        }

        // ─── Process Payment via Stripe ───────────────────────────────────────────

        /// <summary>
        /// Charges the card via Stripe and marks the invoice as paid.
        /// Returns (success, errorMessage, chargeId).
        /// </summary>
        public (bool Success, string Error, string ChargeId) ProcessPayment(
            int invoiceId, string stripeToken, string parentEmail, string parentName)
        {
            using (var db = DbContextFactory.Create())
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
            using (var db = DbContextFactory.Create())
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

		private string BuildProofOfPaymentHtml(Michaelhouse.Models.Payment payment, Michaelhouse.Models.Invoice invoice, string parentName)
		{
			// Fixes the DynamicProxy name issue for the student in the email
			string studentName = invoice.Student != null ? invoice.Student.Name : "Prospective Student";
			string logoUrl = "https://i.postimg.cc/Ss8DWBVf/logo-svg.png"; // Replace with your actual hosted absolute URL

			return $@"
<html>
<head>
    <link href='https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&family=Montserrat:wght@400;600;700&display=swap' rel='stylesheet'>
</head>
<body style='font-family: ""Montserrat"", Segoe UI, sans-serif; color: #1a1a1a; background-color: #f9f9f9; margin: 0; padding: 40px 0;'>
    <table align='center' border='0' cellpadding='0' cellspacing='0' width='600' style='background-color: #ffffff; border: 1px solid #eeeeee; box-shadow: 0 10px 30px rgba(0,0,0,0.05);'>
        
        <tr>
            <td style='padding: 40px 40px 20px 40px; border-bottom: 1px solid #f0f0f0;'>
                <table width='100%'>
                    <tr>
                        <td width='70'>
                            <img src='{logoUrl}' alt='Michaelhouse' width='60' style='display: block; border: 0;' />
                        </td>
                        <td>
                            <h1 style='font-family: ""Playfair Display"", serif; font-size: 20px; color: #1a1a1a; margin: 0; letter-spacing: 1px; text-transform: uppercase;'>Michaelhouse</h1>
                            <p style='font-size: 10px; color: #C21E2E; font-weight: 700; margin: 4px 0 0; text-transform: uppercase; letter-spacing: 3px;'>Official Financial Dispatch</p>
                        </td>
                        <td align='right' valign='top'>
                            <div style='border: 1px solid #C21E2E; color: #C21E2E; font-size: 10px; font-weight: 700; padding: 4px 12px; letter-spacing: 2px;'>PAID</div>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>

        <tr>
            <td style='padding: 40px;'>
                <p style='font-size: 14px; line-height: 1.6; margin-bottom: 24px;'>Dear {parentName},</p>
                <p style='font-size: 14px; line-height: 1.6; margin-bottom: 30px;'>Please find below the official confirmation for your recent transaction. This document serves as a valid proof of payment for the account of <strong>{studentName}</strong>.</p>

                <table width='100%' cellpadding='0' cellspacing='0' style='border-left: 4px solid #1a1a1a; background-color: #fcfcfc; padding: 25px;'>
                    <tr>
                        <td colspan='2' style='padding-bottom: 20px;'>
                            <h2 style='font-size: 11px; font-weight: 700; color: #999999; text-transform: uppercase; letter-spacing: 2px; margin: 0;'>Transaction Dossier</h2>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-size: 11px; color: #888888; text-transform: uppercase; letter-spacing: 1px;'>Invoice Number</td>
                        <td style='padding: 8px 0; font-size: 13px; font-weight: 700; text-align: right;'>{invoice.InvoiceNumber}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-size: 11px; color: #888888; text-transform: uppercase; letter-spacing: 1px;'>Payment Ref</td>
                        <td style='padding: 8px 0; font-size: 13px; font-weight: 600; text-align: right; font-family: monospace;'>{payment.PaymentReference}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-size: 11px; color: #888888; text-transform: uppercase; letter-spacing: 1px;'>Student Name</td>
                        <td style='padding: 8px 0; font-size: 13px; font-weight: 600; text-align: right;'>{studentName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-size: 11px; color: #888888; text-transform: uppercase; letter-spacing: 1px;'>Description</td>
                        <td style='padding: 8px 0; font-size: 13px; font-style: italic; text-align: right;'>{invoice.Description}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; font-size: 11px; color: #888888; text-transform: uppercase; letter-spacing: 1px;'>Date of Settlement</td>
                        <td style='padding: 8px 0; font-size: 13px; text-align: right;'>{payment.PaymentDate:dd MMMM yyyy}</td>
                    </tr>
                    <tr>
                        <td style='padding: 20px 0 0; font-size: 13px; font-weight: 700; color: #C21E2E; text-transform: uppercase; letter-spacing: 2px;'>Amount Paid</td>
                        <td style='padding: 20px 0 0; font-size: 22px; font-family: ""Playfair Display"", serif; font-weight: 700; text-align: right; color: #C21E2E;'>ZAR {payment.AmountPaid:N2}</td>
                    </tr>
                </table>

                <p style='font-size: 11px; color: #999999; line-height: 1.6; margin-top: 30px; border-top: 1px solid #f0f0f0; padding-top: 20px;'>
                    Should you require further financial assistance, please contact the Michaelhouse Bursary Office quoting reference <strong>{payment.PaymentReference}</strong>.
                </p>
            </td>
        </tr>

        <tr>
            <td style='background-color: #1a1a1a; padding: 30px 40px; text-align: center;'>
                <p style='color: #ffffff; font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 2px; margin: 0 0 10px 0;'>Michaelhouse</p>
                <p style='color: #888888; font-size: 10px; margin: 0;'>
                    Private Bag X1, Balgowan, KwaZulu-Natal, 3275 &bull; +27 (0)33 234 4001<br/>
                    <span style='opacity: 0.5;'>Automated System Dispatch &mdash; Do Not Reply</span>
                </p>
            </td>
        </tr>
    </table>
</body>
</html>";
		}

		// ─── Email helper ─────────────────────────────────────────────────────────

		private void SendEmail(string toEmail, string subject, string htmlBody)
        {
            var smtpHost = AppConfig.AppSettings("Email:SmtpHost") ?? "";
            var smtpPort = int.TryParse(AppConfig.AppSettings("Email:SmtpPort"), out int p) ? p : 587;
            var smtpUser = AppConfig.AppSettings("Email:Username") ?? "";
            var smtpPass = AppConfig.AppSettings("Email:Password") ?? "";
            var fromEmail = AppConfig.AppSettings("Email:FromAddress") ?? "";
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