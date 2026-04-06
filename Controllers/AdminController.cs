using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOnly]
    public class AdminController : Controller
    {
        private readonly ApplicationService _appService = new ApplicationService();
        private readonly StudentAccountService _studentAccounts = new StudentAccountService();
        private readonly EmailService _email = new EmailService();
        private readonly InvoiceService _invoices = new InvoiceService();

        private string GetCurrentAdminId() => Session["UserId"]?.ToString();

        // ─── Dashboard ────────────────────────────────────────────────────────────

        public ActionResult Dashboard()
        {
            using (var db = new DBContextClass())
            {
                var apps = db.Applications
                    .Include("Student")
                    .Include("Parent")
                    .Include("Documents")
                    .Include("AdminReviews")
                    .OrderBy(a => a.Status)
                    .ThenByDescending(a => a.Date)
                    .ToList();

                ViewBag.TotalCount = apps.Count;
                ViewBag.PendingCount = apps.Count(a => a.Status == ApplicationStatus.Pending);
                ViewBag.UnderReviewCount = apps.Count(a => a.Status == ApplicationStatus.UnderAiReview);
                ViewBag.AwaitingDecisionCount = apps.Count(a => a.Status == ApplicationStatus.AwaitingAdminDecision);
                ViewBag.FlaggedCount = apps.Count(a => a.Status == ApplicationStatus.Flagged);
                ViewBag.ApprovedCount = apps.Count(a => a.Status == ApplicationStatus.Approved);
                ViewBag.RejectedCount = apps.Count(a => a.Status == ApplicationStatus.Rejected);

                return View(apps);
            }
        }

        // ─── Review ───────────────────────────────────────────────────────────────

        public ActionResult Review(int id)
        {
            using (var db = new DBContextClass())
            {
                var app = db.Applications
                    .Include("Student")
                    .Include("Parent")
                    .Include("Documents")
                    .Include("AdminReviews")
                    .FirstOrDefault(a => a.AppId == id);

                if (app == null) return HttpNotFound();

                // Show registration + invoice status for approved apps
                if (app.Status == ApplicationStatus.Approved)
                {
                    var reg = db.Registrations.FirstOrDefault(r => r.AppId == id);
                    if (reg != null)
                    {
                        ViewBag.Registration = reg;
                        ViewBag.RegFeePaid = _invoices.IsRegistrationFeePaid(reg.RegistrationId);

                        var invoice = db.Invoices.FirstOrDefault(i =>
                            i.RegistrationId == reg.RegistrationId &&
                            i.InvoiceType == "RegistrationFee");

                        ViewBag.RegFeeInvoice = invoice;
                    }
                }

                return View(app);
            }
        }

        // ─── Confirm Decision ─────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm(AdminConfirmViewModel vm)
        {
            if (string.IsNullOrEmpty(vm.Decision))
            {
                TempData["Error"] = "Please select a decision.";
                return RedirectToAction("Review", new { id = vm.AppId });
            }

            try
            {
                _appService.AdminConfirm(
                    vm.AppId,
                    GetCurrentAdminId(),
                    vm.Decision,
                    vm.Notes,
                    vm.AgreedWithAi);
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Error saving decision: {ex.Message}";
                return RedirectToAction("Review", new { id = vm.AppId });
            }

            if (vm.Decision == "Approved")
            {
                // Verify invoice was actually created — if not, create it now
                try
                {
                    EnsureRegistrationFeeInvoiceExists(vm.AppId);
                    TempData["Success"] =
                        $"Application #{vm.AppId} approved. " +
                        $"Registration record and ZAR 950 invoice created. " +
                        $"Parent has been notified by email.";
                }
                catch (System.Exception ex)
                {
                    TempData["Success"] = $"Application #{vm.AppId} approved.";
                    TempData["Error"] = $"Invoice creation warning: {ex.Message} — use 'Fix Invoice' on the Review page.";
                }
            }
            else if (vm.Decision == "Flagged")
            {
                TempData["Success"] = $"Application #{vm.AppId} flagged. Parent notified by email.";
            }
            else
            {
                TempData["Success"] = $"Application #{vm.AppId} marked as {vm.Decision}.";
            }

            return RedirectToAction("Dashboard");
        }

        // ─── Manually fix missing invoice ─────────────────────────────────────────
        // This handles the case where the invoice was not created on approval

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FixInvoice(int appId)
        {
            try
            {
                EnsureRegistrationFeeInvoiceExists(appId);
                TempData["Success"] = "Registration fee invoice created successfully.";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Failed to create invoice: {ex.Message}";
            }

            return RedirectToAction("Review", new { id = appId });
        }

        // ─── Re-trigger AI ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RetriggerAi(int id)
        {
            await _appService.RetriggerAiReviewAsync(id);
            TempData["Success"] = "AI review re-triggered. Refresh in a moment.";
            return RedirectToAction("Review", new { id });
        }

        // ─── Manually Create Student Account ─────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateStudentAccount(int appId)
        {
            try
            {
                using (var db = new DBContextClass())
                {
                    var app = db.Applications
                        .Include("Student")
                        .Include("Parent")
                        .FirstOrDefault(a => a.AppId == appId);

                    if (app == null)
                    {
                        TempData["Error"] = "Application not found.";
                        return RedirectToAction("Review", new { id = appId });
                    }

                    var (user, tempPassword) = _studentAccounts.CreateStudentAccount(app.StudentId);

                    if (tempPassword != null)
                    {
                        _email.SendStudentAccountCreated(
                            app.Parent.Contact,
                            app.Parent.Name,
                            app.Student.Name,
                            user.Email,
                            tempPassword,
                            app.GradeApplying);

                        TempData["Success"] =
                            $"Student account created: {user.Email}. Login details emailed to parent.";
                    }
                    else
                    {
                        TempData["Info"] = "Student account already exists.";
                    }
                }
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Account creation failed: {ex.Message}";
            }

            return RedirectToAction("Review", new { id = appId });
        }

        // ─── View Document ────────────────────────────────────────────────────────

        public ActionResult ViewDocument(int id)
        {
            using (var db = new DBContextClass())
            {
                var doc = db.Documents.Find(id);
                if (doc == null) return HttpNotFound();

                var uploadRoot = System.Web.Hosting.HostingEnvironment.MapPath(
                    System.Configuration.ConfigurationManager
                        .AppSettings["DocumentStorage:UploadRoot"] ?? "~/App_Data/Uploads");

                var fullPath = System.IO.Path.Combine(uploadRoot, doc.FilePath);
                if (!System.IO.File.Exists(fullPath)) return HttpNotFound();

                return File(System.IO.File.ReadAllBytes(fullPath), doc.ContentType, doc.FileName);
            }
        }

        // ─── Registrations ────────────────────────────────────────────────────────

        public ActionResult Registrations()
        {
            using (var db = new DBContextClass())
            {
                var regs = db.Registrations
                    .Include("Student")
                    .Include("Student.User")
                    .Include("Student.Parent")
                    .Include("Student.StudentSubjects")
                    .Include("Student.StudentSubjects.Subject")
                    .Include("Application")
                    .OrderBy(r => r.Status)
                    .ThenBy(r => r.Student.LastName)
                    .ToList();

                return View(regs);
            }
        }

        // ─── Private helper ───────────────────────────────────────────────────────

        /// <summary>
        /// Makes sure a Registration record and a RegistrationFee invoice
        /// exist for the given application. Safe to call multiple times.
        /// </summary>
        private void EnsureRegistrationFeeInvoiceExists(int appId)
        {
            using (var db = new DBContextClass())
            {
                var app = db.Applications
                    .Include("Student")
                    .Include("Parent")
                    .FirstOrDefault(a => a.AppId == appId);

                if (app == null)
                    throw new System.Exception("Application not found.");

                // Ensure registration record exists
                var reg = db.Registrations.FirstOrDefault(r => r.AppId == appId);
                if (reg == null)
                {
                    var regService = new RegistrationService();
                    reg = regService.CreateRegistration(appId, app.StudentId, app.GradeApplying);
                }

                // Ensure invoice exists
                var existing = db.Invoices.FirstOrDefault(i =>
                    i.RegistrationId == reg.RegistrationId &&
                    i.InvoiceType == "RegistrationFee");

                if (existing == null)
                {
                    var invoice = new Invoice
                    {
                        InvoiceNumber = $"MHS-REG-{System.DateTime.Now.Year}-{(db.Invoices.Count() + 1):D5}",
                        RegistrationId = reg.RegistrationId,
                        StudentId = app.StudentId,
                        ParentId = app.ParentId,
                        InvoiceType = "RegistrationFee",
                        Amount = InvoiceService.RegistrationFee,
                        Description = "Non-Refundable Registration Fee — Michaelhouse",
                        CreatedDate = System.DateTime.Now,
                        DueDate = System.DateTime.Now.AddDays(7),
                        Status = "Pending"
                    };
                    db.Invoices.Add(invoice);
                    db.SaveChanges();
                }
            }
        }
        public ActionResult StreamGroups()
        {
            int currentYear = DateTime.Now.Year;

            using (var db = new DBContextClass())
            {
                // Eager load everything needed for the 'White Dossier' view
                var enrolments = db.StreamEnrolments
                    .Include("Student")
                    .Include("Student.User")
                    .Include("Registration")
                    // Optional: Filter by current academic year if your model supports it
                    .Where(se => se.Grade >= 10)
                    .OrderBy(se => se.Grade)
                    .ThenBy(se => se.Stream)
                    .ThenBy(se => se.Student.LastName)
                    .ToList();

                // If the list is null (unlikely with .ToList()), initialize empty to prevent View crashes
                return View(enrolments ?? new List<Michaelhouse.Models.StreamEnrolment>());
            }
        }
        public void AssignTeacherToSubjects(int teacherId, int subjectId, int grade, AcademicStream stream = AcademicStream.None)
        {
            using (var db = new DBContextClass())
            {
                if (!db.TeacherSubjectGrades.Any(tsg => tsg.TeacherId == teacherId && tsg.SubjectId == subjectId))
                {
                    db.TeacherSubjectGrades.Add(new TeacherSubjectGrade
                    {
                        TeacherId = teacherId,
                        SubjectId = subjectId,
                        Grade = grade,
                        Stream = stream
                    });
                    db.SaveChanges();
                }
            }
        }
    }
}