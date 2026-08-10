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

            if (!IsStudentProfileComplete(reg.StudentId))
                return RedirectToAction("Start", "StudentProfile", new { appId });

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

                if (!IsStudentProfileComplete(reg.StudentId, db))
                {
                    TempData["Info"] = "Please complete the student profile before registration is finalized.";
                    return RedirectToAction("Start", "StudentProfile", new { appId });
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

            if (reg == null)
                return HttpNotFound();

            // ---------------------------------------------------------
            // 1. Registration fee must be paid
            // ---------------------------------------------------------
            if (!_invoices.IsRegistrationFeePaid(reg.RegistrationId))
            {
                TempData["Error"] = "Registration fee must be paid first.";

                return RedirectToAction(
                    "Index",
                    "Payment",
                    new { registrationId = reg.RegistrationId });
            }

            // ---------------------------------------------------------
            // 2. Grade 10-12 students must select subjects
            // ---------------------------------------------------------
            if (reg.GradeEnrolling >= 10 &&
                reg.Status != RegistrationStatus.SubjectsSelected)
            {
                TempData["Error"] = "Please select your subjects first.";

                return RedirectToAction(
                    "SelectSubjects",
                    new { appId });
            }

            // ---------------------------------------------------------
            // 3. Student profile must be complete
            // ---------------------------------------------------------
            if (!IsStudentProfileComplete(reg.StudentId))
            {
                TempData["Error"] =
                    "Please complete the student profile before registration is finalized.";

                return RedirectToAction(
                    "Start",
                    "StudentProfile",
                    new { appId });
            }

            // ---------------------------------------------------------
            // 4. Complete the registration
            // ---------------------------------------------------------
            _regService.CompleteRegistration(registrationId);

            try
            {
                // =====================================================
                // 5. Create student account
                // =====================================================
                var (user, tempPassword) =
                    _studentAccounts.CreateStudentAccount(reg.StudentId);

                // =====================================================
                // 6. Allocate boarding house
                // =====================================================
                var aiService = new AIResidenceAllocationService();

                var assignment =
                    aiService.AllocateStudent(reg.StudentId);

                if (assignment == null)
                {
                    throw new Exception(
                        "Registration completed, but no residence allocation was created.");
                }

                // =====================================================
                // 7. Generate / retrieve permanent QR identity
                // =====================================================
                var qrService = new StudentQRCodeService();

                var qr =
                    qrService.GetActiveQRCode(reg.StudentId);

                if (qr == null)
                {
                    qr = qrService.GenerateQRCode(reg.StudentId);
                }

                // =====================================================
                // 8. Load all information needed for the email
                // =====================================================
                using (var db = new DBContextClass())
                {
                    var student = db.Students
                        .FirstOrDefault(s => s.StudentId == reg.StudentId);

                    if (student == null)
                        throw new Exception("Student record could not be found.");

                    var parent = db.Parents
                        .Find(GetCurrentParentId());

                    // -------------------------------------------------
                    // Load residence + House Master
                    // -------------------------------------------------
                    var residence = db.Residences
                        .Include("HouseMaster")
                        .FirstOrDefault(r =>
                            r.ResidenceId == assignment.ResidenceId);

                    // -------------------------------------------------
                    // Load room
                    // -------------------------------------------------
                    var room = db.Rooms
                        .FirstOrDefault(r =>
                            r.RoomId == assignment.RoomId);

                    // -------------------------------------------------
                    // Load bed
                    // -------------------------------------------------
                    var bed = db.Beds
                        .FirstOrDefault(b =>
                            b.BedId == assignment.BedId);

                    // =================================================
                    // 9. Basic student information
                    // =================================================
                    string studentName =
                        student.Name ?? "Student";

                    string studentNumber =
                        !string.IsNullOrWhiteSpace(student.StudentNumber)
                            ? student.StudentNumber
                            : "To be confirmed";

                    string loginEmail =
                        user?.Email ?? "To be confirmed";

                    // =================================================
                    // 10. Temporary password
                    // =================================================
                    string passwordLine;

                    if (!string.IsNullOrWhiteSpace(tempPassword))
                    {
                        passwordLine =
                            $"<p style='margin:0; font-size:14px; color:#555;'>" +
                            $"<strong>Temporary Password:</strong> " +
                            $"<span style='color:#C21E2E; font-weight:bold;'>" +
                            $"{HttpUtility.HtmlEncode(tempPassword)}" +
                            $"</span></p>";
                    }
                    else
                    {
                        passwordLine =
                            "<p style='margin:0; font-size:14px; color:#555;'>" +
                            "<strong>Password:</strong> Please use the student's " +
                            "existing password or the password reset option on the login page." +
                            "</p>";
                    }

                    // =================================================
                    // 11. Residence information
                    // =================================================
                    string residenceName =
                        residence?.Name ?? "To be confirmed";

                    string roomNumber =
                        room?.RoomNumber ?? "To be confirmed";

                    string bedNumber =
                        bed?.BedNumber ?? "To be confirmed";

                    string houseMasterName =
                        residence?.HouseMaster?.FullName ?? "To be confirmed";

                    string moveInDate =
                        assignment.MoveInDate != default(DateTime)
                            ? assignment.MoveInDate.ToString("dd MMMM yyyy")
                            : "To be confirmed";

                    // =================================================
                    // 12. QR image
                    // =================================================
                    string qrBase64 =
                        qr?.QRImage != null
                            ? Convert.ToBase64String(qr.QRImage)
                            : null;

                    string qrImgTag = "";

                    if (!string.IsNullOrWhiteSpace(qrBase64))
                    {
                        qrImgTag =
                            $"<div style='text-align:center; margin-top:25px;'>" +
                            $"<img src='data:image/png;base64,{qrBase64}' " +
                            $"alt='Student QR Identity' " +
                            $"style='width:180px;height:180px;border:1px solid #eeeeee;padding:10px;' />" +
                            $"</div>";
                    }

                    // =================================================
                    // 13. Login URL
                    // =================================================
                    string loginUrl =
                        Url.Action(
                            "Login",
                            "Account",
                            null,
                            Request.Url.Scheme);

                    // =================================================
                    // 14. Email
                    // =================================================
                    string parentName =
                        parent?.Name ?? "Parent/Guardian";

                    string subject =
                        $"OFFICIAL NOTICE: Registration Complete — {studentName}";

                    string logoUrl =
                        "https://i.postimg.cc/Ss8DWBVf/Content/logo.svg.png";

                    string body = $@"
<div style='background-color:#f5f5f5; padding:30px 10px; font-family:Arial,Helvetica,sans-serif;'>

    <div style='max-width:700px; margin:0 auto; background:#ffffff;'>

        <!-- HEADER -->
        <div style='background-color:#1a1a1a;
                    padding:50px 40px;
                    border-bottom:5px solid #C21E2E;
                    text-align:center;'>

            <img src='{logoUrl}'
                 alt='Michaelhouse'
                 style='height:80px;
                        width:auto;
                        margin-bottom:20px;
                        display:inline-block;' />

            <h1 style='color:#ffffff;
                       margin:0;
                       letter-spacing:5px;
                       text-transform:uppercase;
                       font-size:22px;'>
                Michaelhouse
            </h1>

            <div style='height:1px;
                        width:40px;
                        background-color:#C21E2E;
                        margin:15px auto;'>
            </div>

            <p style='color:#999999;
                      margin:0;
                      letter-spacing:3px;
                      text-transform:uppercase;
                      font-size:9px;
                      font-weight:bold;'>
                Enrollment Management Division
            </p>

        </div>


        <!-- BODY -->
        <div style='padding:50px 45px;
                    color:#333333;
                    line-height:1.7;'>

            <h2 style='font-size:28px;
                       color:#1a1a1a;
                       margin-top:0;'>
                Dear {HttpUtility.HtmlEncode(parentName)},
            </h2>

            <p style='font-size:16px;'>
                We are pleased to confirm that the registration of
                <strong>{HttpUtility.HtmlEncode(studentName)}</strong>
                has been successfully completed.
            </p>

            <p style='font-size:14px; color:#666;'>
                The student's institutional identity has now been provisioned,
                including their student account, boarding allocation and
                permanent QR identity.
            </p>


            <!-- STUDENT INFORMATION -->
            <div style='margin:35px 0;
                        border:1px solid #dddddd;'>

                <div style='background:#1a1a1a;
                            color:#ffffff;
                            padding:12px 20px;
                            font-size:11px;
                            font-weight:bold;
                            letter-spacing:2px;
                            text-transform:uppercase;'>
                    Student Information
                </div>

                <div style='padding:25px;'>

                    <table style='width:100%;
                                  border-collapse:collapse;'>

                        <tr>
                            <td style='padding:9px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;'>
                                Student Name
                            </td>

                            <td style='padding:9px 0;
                                       color:#1a1a1a;
                                       font-size:14px;
                                       font-weight:bold;'>
                                {HttpUtility.HtmlEncode(studentName)}
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:9px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;'>
                                Student Number
                            </td>

                            <td style='padding:9px 0;
                                       color:#1a1a1a;
                                       font-size:14px;
                                       font-weight:bold;'>
                                {HttpUtility.HtmlEncode(studentNumber)}
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:9px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;'>
                                Grade
                            </td>

                            <td style='padding:9px 0;
                                       color:#1a1a1a;
                                       font-size:14px;
                                       font-weight:bold;'>
                                Grade {reg.GradeEnrolling}
                            </td>
                        </tr>

                    </table>

                </div>
            </div>


            <!-- RESIDENCE ALLOCATION -->
            <div style='margin:35px 0;
                        border:2px solid #C21E2E;'>

                <div style='background:#C21E2E;
                            color:#ffffff;
                            padding:14px 20px;
                            font-size:11px;
                            font-weight:bold;
                            letter-spacing:2px;
                            text-transform:uppercase;'>
                    Boarding Residence Allocation
                </div>

                <div style='padding:30px;
                            background:#fff;'>

                    <p style='font-size:14px;
                              color:#555;
                              margin-top:0;
                              margin-bottom:25px;'>
                        The following boarding accommodation has been
                        allocated to the student:
                    </p>

                    <table style='width:100%;
                                  border-collapse:collapse;'>

                        <tr style='border-bottom:1px solid #eeeeee;'>
                            <td style='padding:12px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;
                                       width:42%;'>
                                Residence / House
                            </td>

                            <td style='padding:12px 0;
                                       color:#1a1a1a;
                                       font-size:15px;
                                       font-weight:bold;'>
                                {HttpUtility.HtmlEncode(residenceName)}
                            </td>
                        </tr>

                        <tr style='border-bottom:1px solid #eeeeee;'>
                            <td style='padding:12px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;'>
                                Room
                            </td>

                            <td style='padding:12px 0;
                                       color:#1a1a1a;
                                       font-size:15px;
                                       font-weight:bold;'>
                                {HttpUtility.HtmlEncode(roomNumber)}
                            </td>
                        </tr>

                        <tr style='border-bottom:1px solid #eeeeee;'>
                            <td style='padding:12px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;'>
                                Bed
                            </td>

                            <td style='padding:12px 0;
                                       color:#1a1a1a;
                                       font-size:15px;
                                       font-weight:bold;'>
                                {HttpUtility.HtmlEncode(bedNumber)}
                            </td>
                        </tr>

                        <tr style='border-bottom:1px solid #eeeeee;'>
                            <td style='padding:12px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;'>
                                House Master
                            </td>

                            <td style='padding:12px 0;
                                       color:#1a1a1a;
                                       font-size:15px;
                                       font-weight:bold;'>
                                {HttpUtility.HtmlEncode(houseMasterName)}
                            </td>
                        </tr>

                        <tr>
                            <td style='padding:12px 0;
                                       color:#888;
                                       font-size:11px;
                                       font-weight:bold;
                                       text-transform:uppercase;'>
                                Move-in Date
                            </td>

                            <td style='padding:12px 0;
                                       color:#1a1a1a;
                                       font-size:15px;
                                       font-weight:bold;'>
                                {HttpUtility.HtmlEncode(moveInDate)}
                            </td>
                        </tr>

                    </table>

                </div>
            </div>


            <!-- LOGIN CREDENTIALS -->
            <div style='margin:35px 0;
                        border:1px solid #1a1a1a;'>

                <div style='background:#1a1a1a;
                            color:#ffffff;
                            padding:12px 20px;
                            font-size:11px;
                            font-weight:bold;
                            letter-spacing:2px;
                            text-transform:uppercase;'>
                    Student Portal Access
                </div>

                <div style='padding:25px;
                            background:#fafafa;'>

                    <p style='margin:0 0 12px;
                              font-size:14px;'>
                        <strong>Login Email:</strong>
                        {HttpUtility.HtmlEncode(loginEmail)}
                    </p>

                    {passwordLine}

                </div>

            </div>


            <!-- QR CODE -->
            <div style='margin:35px 0;
                        padding:30px;
                        background:#fafafa;
                        border:1px solid #eeeeee;
                        text-align:center;'>

                <h3 style='margin-top:0;
                           color:#1a1a1a;
                           font-size:18px;'>
                    Student QR Identity
                </h3>

                <p style='font-size:13px;
                          color:#666;'>
                    This QR identity is permanently associated with
                    the student's institutional record.
                </p>

                {qrImgTag}

            </div>


            <!-- LOGIN BUTTON -->
            <div style='text-align:center;
                        margin:45px 0;'>

                <a href='{loginUrl}'
                   style='background-color:#1a1a1a;
                          color:#ffffff;
                          padding:18px 40px;
                          text-decoration:none;
                          font-size:11px;
                          font-weight:bold;
                          letter-spacing:3px;
                          text-transform:uppercase;
                          display:inline-block;'>
                    Access Student Portal
                </a>

            </div>


            <p style='font-size:12px;
                      color:#888;
                      border-top:1px solid #eeeeee;
                      padding-top:25px;'>
                Please keep this email for your records. The residence
                allocation and student identity information contained
                above should be retained for future reference.
            </p>

        </div>


        <!-- FOOTER -->
        <div style='background-color:#fafafa;
                    padding:35px;
                    text-align:center;
                    border-top:1px solid #eeeeee;'>

            <p style='margin:0;
                      font-size:10px;
                      color:#999999;
                      letter-spacing:1px;
                      text-transform:uppercase;'>
                Michaelhouse &nbsp;·&nbsp; Balgowan &nbsp;·&nbsp; KwaZulu-Natal
            </p>

            <p style='margin:15px 0 0;
                      font-size:8px;
                      color:#cccccc;
                      text-transform:uppercase;
                      letter-spacing:1px;'>
                Registration Confirmation
                &nbsp;·&nbsp;
                Ref: MHS-REG-{DateTime.Now.Year}-{studentNumber}
            </p>

        </div>

    </div>

</div>";

                    // =================================================
                    // 15. Send registration email
                    // =================================================
                    if (parent != null &&
                        !string.IsNullOrWhiteSpace(parent.Contact))
                    {
                        _email.SendPlain(
                            parent.Contact,
                            subject,
                            body);
                    }
                }

                // =====================================================
                // 16. Create annual invoices
                // =====================================================
                _invoices.CreateAnnualFeeInvoices(
                    registrationId,
                    reg.StudentId,
                    GetCurrentParentId());

                TempData["Success"] =
                    "Registration complete! Student account, residence allocation, and permanent QR identity are ready.";
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Post-registration error: {ex}");

                TempData["Success"] =
                    "Registration completed successfully!";
            }

            return RedirectToAction(
                "Complete",
                new { appId });
        }

        private bool IsStudentProfileComplete(int studentId, DBContextClass existingContext = null)
        {
            var profileService = new BoardingProfileService();
            if (existingContext != null)
                return profileService.IsComplete(existingContext.StudentProfiles.Find(studentId));

            using (var db = new DBContextClass())
            {
                return profileService.IsComplete(db.StudentProfiles.Find(studentId));
            }
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
