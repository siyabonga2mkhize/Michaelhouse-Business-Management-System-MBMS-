using Microsoft.EntityFrameworkCore;
using Michaelhouse.Infrastructure;
﻿using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    [ParentOnly]
    public class PaymentController : BaseController
    {
        private readonly InvoiceService _invoices = new InvoiceService();
        private readonly PaymentService _payments = new PaymentService();

        private int GetCurrentParentId() => (int)Session["ParentId"];

        // ─── Payment Dashboard ────────────────────────────────────────────────────

        public ActionResult Index(int? registrationId)
        {
            int parentId = GetCurrentParentId();

            var invoices = registrationId.HasValue
                ? _invoices.GetInvoicesForRegistration(registrationId.Value)
                    .Where(i => i.ParentId == parentId).ToList()
                : _invoices.GetInvoicesForParent(parentId);

            ViewBag.RegistrationId = registrationId;

            if (registrationId.HasValue)
            {
                ViewBag.RegistrationFeePaid =
                    _invoices.IsRegistrationFeePaid(registrationId.Value);

                // Also get the appId so the "Continue Registration" button works
                using (var db = DbContextFactory.Create())
                {
                    var reg = db.Registrations
                        .FirstOrDefault(r => r.RegistrationId == registrationId.Value);
                    if (reg != null)
                        ViewBag.AppId = reg.AppId;
                }
            }

            return View(invoices);
        }

        // ─── Pay Invoice ──────────────────────────────────────────────────────────

        public ActionResult Pay(int id)
        {
            using (var db = DbContextFactory.Create())
            {
                var invoice = db.Invoices
                    .Include("Student")
                    .Include("Payments")
                    .FirstOrDefault(i => i.InvoiceId == id);

                if (invoice == null) return NotFound();

                if (invoice.ParentId != GetCurrentParentId())
                    return StatusCode(403);

                if (invoice.Status == "Paid")
                {
                    TempData["Info"] = "This invoice has already been paid.";
                    return RedirectToAction("Receipt", new { id });
                }

                /*ViewBag.StripePublishableKey =
                    System.Configuration.ConfigurationManager
                        .AppSettings["Stripe:PublishableKey"];*/
                ViewBag.StripePublishableKey = "pk_test_51TGkmpCX1ORzTt6Nf1bjL74efaKA6lbyHx2h2n7N9wcnhcMIgbjGeuAidiEwVVPZbXPMU9UHXE7Z1jA1zHqtapSQ00rqr8of2D";

                return View(invoice);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Pay(int id, string stripeToken)
        {
            int parentId = GetCurrentParentId();

            using (var db = DbContextFactory.Create())
            {
                // 1. Include Student and Parent so the View has the data it needs to re-render
                var invoice = db.Invoices
                    .Include("Student")
                    .Include("Parent")
                    .FirstOrDefault(i => i.InvoiceId == id);

                if (invoice == null) return NotFound();
                if (invoice.ParentId != parentId) return StatusCode(403);

                var parent = db.Parents.Find(parentId);

                // 2. Process the payment
                var (success, error, chargeId) = _payments.ProcessPayment(
                    id, stripeToken, parent?.Contact, parent?.Name);

                if (success)
                {
                    TempData["Success"] = "Payment successful! A proof of payment has been sent to your email.";

                    if (invoice.InvoiceType == "RegistrationFee")
                    {
                        return RedirectToAction("Index", new { registrationId = invoice.RegistrationId });
                    }

                    return RedirectToAction("Receipt", new { id });
                }

                // 3. IF IT FAILS: We must return the View WHILE INSIDE this using block
                TempData["Error"] = error;

                // Ensure the Stripe key is passed back so the fields stay clickable
                ViewBag.StripePublishableKey = "pk_test_51TGkmpCX1ORzTt6Nf1bjL74efaKA6lbyHx2h2n7N9wcnhcMIgbjGeuAidiEwVVPZbXPMU9UHXE7Z1jA1zHqtapSQ00rqr8of2D";

                return View(invoice); // <--- MOVE THIS INSIDE THE CURLY BRACES
            }
        }
        // ─── Receipt ──────────────────────────────────────────────────────────────

        public ActionResult Receipt(int id)
        {
            using (var db = DbContextFactory.Create())
            {
                var invoice = db.Invoices
                    .Include("Student")
                    .Include("Payments")
                    .FirstOrDefault(i => i.InvoiceId == id);

                if (invoice == null) return NotFound();
                if (invoice.ParentId != GetCurrentParentId())
                    return StatusCode(403);

                return View(invoice);
            }
        }

        // ─── Resend Proof of Payment ──────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResendProof(int paymentId)
        {
            using (var db = DbContextFactory.Create())
            {
                var payment = db.Payments
                    .Include("Invoice")
                    .Include("Invoice.Parent")
                    .FirstOrDefault(p => p.PaymentId == paymentId);

                if (payment == null) return NotFound();
                if (payment.Invoice.ParentId != GetCurrentParentId())
                    return StatusCode(403);

                var parent = db.Parents.Find(GetCurrentParentId());

                _payments.SendProofOfPaymentEmail(
                    paymentId, parent?.Contact, parent?.Name);

                TempData["Success"] = "Proof of payment resent to your email.";
                return RedirectToAction("Receipt", new { id = payment.InvoiceId });
            }
        }
    }
}