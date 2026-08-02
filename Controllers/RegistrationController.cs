using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Services;


namespace Michaelhouse.Controllers
{
    [ParentOnly]
    public class RegistrationController : Controller
    {
        private readonly RegistrationService _regService = new RegistrationService();
        private readonly StudentAccountService _studentAccounts = new StudentAccountService();
        private readonly InvoiceService _invoices = new InvoiceService();
        private readonly EmailService _email = new EmailService();

        private int GetCurrentParentId() => (int)Session["ParentId"];

        // ─── Registration Home ────────────────────────────────────────────────────

        public ActionResult Index(int appId)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null)
            {
                TempData["Error"] = "No registration found.";
                return RedirectToAction("Index", "Applications");
            }

            if (reg.Status == RegistrationStatus.Completed)
                return RedirectToAction("Complete", new { appId });

            if (!_invoices.IsRegistrationFeePaid(reg.RegistrationId))
            {
                TempData["Info"] = "Please pay the registration fee first.";
                return RedirectToAction("Index", "Payment",
                    new { registrationId = reg.RegistrationId });
            }

            if (reg.GradeEnrolling >= 10 &&
                reg.Status != RegistrationStatus.SubjectsSelected)
                return RedirectToAction("SelectSubjects", new { appId });

            return RedirectToAction("Confirm", new { appId });
        }

        // ─── Select Subjects (Grade 10-12) ────────────────────────────────────────

        public ActionResult SelectSubjects(int appId)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            if (!_invoices.IsRegistrationFeePaid(reg.RegistrationId))
            {
                TempData["Info"] = "Please pay the registration fee first.";
                return RedirectToAction("Index", "Payment",
                    new { registrationId = reg.RegistrationId });
            }

            if (reg.GradeEnrolling <= 9)
                return RedirectToAction("Confirm", new { appId });

            using (var db = new DBContextClass())
            {
                var existing = db.StudentSubjects
                    .Include("Subject")
                    .Where(ss => ss.StudentId == reg.StudentId)
                    .ToList();

                var existingStream = db.StreamEnrolments
                    .FirstOrDefault(se => se.StudentId == reg.StudentId &&
                                          se.RegistrationId == reg.RegistrationId);

                ViewBag.AppId = appId;
                ViewBag.RegistrationId = reg.RegistrationId;
                ViewBag.Grade = reg.GradeEnrolling;
                ViewBag.StudentName = reg.Student?.Name;

                ViewBag.ExistingLanguage = existing
                    .FirstOrDefault(ss => RegistrationService.LanguageChoices
                        .Contains(ss.Subject.Name))?.Subject.Name;

                ViewBag.ExistingMaths = existing
                    .FirstOrDefault(ss => RegistrationService.MathsOptions
                        .Contains(ss.Subject.Name))?.Subject.Name;

                ViewBag.ExistingStream = existingStream?.Stream ?? AcademicStream.None;

                ViewBag.ExistingStreamSubjects = existing
                    .Where(ss => ss.Stream != AcademicStream.None && !ss.IsCompulsory)
                    .Select(ss => ss.Subject.Name)
                    .ToList();
            }

            return View();
        }

        // Fix: Accept stream as int then cast — avoids MVC enum binding issue
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SelectSubjects(
            int appId,
            int registrationId,
            string languageChoice,
            string mathsChoice,
            int stream,                  // ← int not enum (MVC binds int reliably)
            List<string> streamSubjects)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            // Cast int to enum
            var academicStream = (AcademicStream)stream;

            var (success, error) = _regService.SaveSubjectSelections(
                reg.StudentId,
                registrationId,
                reg.GradeEnrolling,
                languageChoice,
                mathsChoice,
                academicStream,
                streamSubjects ?? new List<string>());

            if (!success)
            {
                TempData["Error"] = error;
                return RedirectToAction("SelectSubjects", new { appId });
            }

            TempData["Success"] = "Subjects saved successfully!";
            return RedirectToAction("Confirm", new { appId });
        }

        // ─── Confirm ─────────────────────────────────────────────────────────────

        public ActionResult Confirm(int appId)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            using (var db = new DBContextClass())
            {
                var student = db.Students.Include("Parent")
                    .FirstOrDefault(s => s.StudentId == reg.StudentId);
                var subjects = db.StudentSubjects.Include("Subject")
                    .Where(ss => ss.StudentId == reg.StudentId).ToList();
                var streamEnrolment = db.StreamEnrolments
                    .FirstOrDefault(se => se.StudentId == reg.StudentId &&
                                          se.RegistrationId == reg.RegistrationId);

                // Auto-assign Grade 8 & 9 subjects on first visit to confirm
                if (reg.GradeEnrolling <= 9 && !subjects.Any())
                {
                    _regService.AssignGrade8And9Subjects(reg.StudentId);
                    subjects = db.StudentSubjects.Include("Subject")
                        .Where(ss => ss.StudentId == reg.StudentId).ToList();
                }

                bool needsSubs = reg.GradeEnrolling >= 10 &&
                                 reg.Status != RegistrationStatus.SubjectsSelected &&
                                 reg.Status != RegistrationStatus.Completed;

                ViewBag.AppId = appId;
                ViewBag.Reg = reg;
                ViewBag.Student = student;
                ViewBag.Subjects = subjects;
                ViewBag.NeedsSubjects = needsSubs;
                ViewBag.StreamEnrolment = streamEnrolment;
            }

            return View();
        }

        // ─── Complete Registration ────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteRegistration(int appId, int registrationId)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            if (!_invoices.IsRegistrationFeePaid(reg.RegistrationId))
            {
                TempData["Error"] = "Registration fee must be paid first.";
                return RedirectToAction("Index", "Payment",
                    new { registrationId = reg.RegistrationId });
            }

            if (reg.GradeEnrolling >= 10 &&
                reg.Status != RegistrationStatus.SubjectsSelected)
            {
                TempData["Error"] = "Please select your subjects first.";
                return RedirectToAction("SelectSubjects", new { appId });
            }

            _regService.CompleteRegistration(registrationId);

            try
            {
                var (user, tempPassword) = _studentAccounts.CreateStudentAccount(reg.StudentId);

                if (tempPassword != null)
                {
                    using (var db = new DBContextClass())
                    {
                        var parent = db.Parents.Find(GetCurrentParentId());
                        if (parent != null)
                        {
                            _email.SendStudentAccountCreated(
                                parent.Contact,
                                parent.Name,
                                reg.Student?.Name ?? "Student",
                                user.Email,
                                tempPassword,
                                reg.GradeEnrolling);
                        }
                    }
                }

                // Allocate boarding house space and generate the permanent student QR identity.
                var aiService = new AIResidenceAllocationService();
                var assignment = aiService.AllocateStudent(reg.StudentId);
                var qr = new StudentQRCodeService().GetActiveQRCode(reg.StudentId);

                // Send registration success email including QR, allocation details, and house master
                using (var db = new DBContextClass())
                {
                    var student = db.Students.Find(reg.StudentId);
                    var residence = db.Residences.Find(assignment.ResidenceId);
                    var room = db.Rooms.Find(assignment.RoomId);
                    var bed = db.Beds.Find(assignment.BedId);

                    // Compose email body (simple) and embed QR as base64
                    string qrBase64 = qr?.QRImage != null ? Convert.ToBase64String(qr.QRImage) : null;
                    string qrImgTag = qrBase64 != null ? $"<img src=\"data:image/png;base64,{qrBase64}\" alt=\"QR\" style=\"width:150px;\"/>" : "";

                    string houseMasterName = residence.HouseMaster?.FullName ?? "To be confirmed";
                    string body = $@"<p>Dear parent,</p>
<p>Student {student.FirstName} {student.LastName} has been allocated to <strong>{residence.Name}</strong>, Room <strong>{room.RoomNumber}</strong>, Bed <strong>{bed.BedNumber}</strong>.</p>
<p>House Master: {houseMasterName}</p>
{qrImgTag}
";

                    var parent = db.Parents.Find(GetCurrentParentId());
                    if (parent != null)
                    {
                        var emailSvc = new EmailService();
                        emailSvc.SendPlain(parent.Contact, "Registration Successful - Residence Allocation", body);
                    }
                }

                _invoices.CreateAnnualFeeInvoices(
                    registrationId, reg.StudentId, GetCurrentParentId());

                TempData["Success"] =
                    "Registration complete! Student account, residence allocation, and permanent QR identity are ready.";
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Post-registration error: {ex.Message}");
                TempData["Success"] = "Registration completed successfully!";
            }

            return RedirectToAction("Complete", new { appId });
        }

        // ─── Complete Page ────────────────────────────────────────────────────────

        public ActionResult Complete(int appId)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            using (var db = new DBContextClass())
            {
                ViewBag.Reg = reg;
                ViewBag.Student = db.Students.Find(reg.StudentId);
                ViewBag.Subjects = db.StudentSubjects.Include("Subject")
                    .Where(ss => ss.StudentId == reg.StudentId).ToList();
                ViewBag.StreamEnrolment = db.StreamEnrolments
                    .FirstOrDefault(se => se.StudentId == reg.StudentId &&
                                          se.RegistrationId == reg.RegistrationId);
            }

            return View();
        }
    }
}
