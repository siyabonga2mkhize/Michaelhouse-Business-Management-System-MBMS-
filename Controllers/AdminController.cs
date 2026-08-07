using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOnly]
    public class AdminController : BaseController
    {
        private readonly ApplicationService _appService = new ApplicationService();
        private readonly StudentAccountService _studentAccounts = new StudentAccountService();
        private readonly EmailService _email = new EmailService();
        private readonly InvoiceService _invoices = new InvoiceService();

        private string GetCurrentAdminId() => Session["UserId"]?.ToString();

        // ─── Dashboard ────────────────────────────────────────────────────────────

        public ActionResult Dashboard()
        {
            using (var db = DbContextFactory.Create())
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
            using (var db = DbContextFactory.Create())
            {
                var app = db.Applications
                    .Include("Student")
                    .Include("Parent")
                    .Include("Documents")
                    .Include("AdminReviews")
                    .FirstOrDefault(a => a.AppId == id);

                if (app == null) return NotFound();

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
                using (var db = DbContextFactory.Create())
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
            using (var db = DbContextFactory.Create())
            {
                var doc = db.Documents.Find(id);
                if (doc == null) return NotFound();

                var uploadRoot = PathHelper.MapPath(
                    AppConfig.AppSettings("DocumentStorage:UploadRoot") ?? "~/App_Data/Uploads");

                var fullPath = System.IO.Path.Combine(uploadRoot, doc.FilePath);
                if (!System.IO.File.Exists(fullPath)) return NotFound();

                return File(System.IO.File.ReadAllBytes(fullPath), doc.ContentType, doc.FileName);
            }
        }

        // ─── Registrations ────────────────────────────────────────────────────────

        public ActionResult Registrations()
        {
            using (var db = DbContextFactory.Create())
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
            using (var db = DbContextFactory.Create())
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

            using (var db = DbContextFactory.Create())
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
            using (var db = DbContextFactory.Create())
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

        // ─────────────────────────────────────────────────────────────────────────
        // TEACHER ATTENDANCE HISTORY (Admin)
        // ─────────────────────────────────────────────────────────────────────────

        public ActionResult TeacherAttendanceHistory(DateTime? fromDate, DateTime? toDate, int? teacherId)
        {
            using (var db = DbContextFactory.Create())
            {
                var query = db.TeacherAttendances
                    .Include(t => t.Teacher)
                    .AsQueryable();

                if (fromDate.HasValue)
                    query = query.Where(ta => ta.Date >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(ta => ta.Date <= toDate.Value);
                if (teacherId.HasValue && teacherId.Value > 0)
                    query = query.Where(ta => ta.TeacherId == teacherId.Value);

                // First, project into a simple anonymous type (no arithmetic)
                var intermediate = query
                    .OrderByDescending(ta => ta.Date)
                    .Select(ta => new
                    {
                        TeacherFirstName = ta.Teacher.FirstName,
                        TeacherLastName = ta.Teacher.LastName,
                        ta.Date,
                        ta.SignInTime,
                        ta.SignOutTime,
                        ta.IsVerified
                    })
                    .AsEnumerable(); // Switch to LINQ to Objects

                // Now compute duration in memory
                var records = intermediate
                    .Select(ta => new TeacherAttendanceRecordViewModel
                    {
                        TeacherName = ta.TeacherFirstName + " " + ta.TeacherLastName,
                        Date = ta.Date,
                        SignInTime = ta.SignInTime,
                        SignOutTime = ta.SignOutTime,
                        Duration = ta.SignInTime.HasValue && ta.SignOutTime.HasValue
                                    ? ta.SignOutTime.Value - ta.SignInTime.Value
                                    : (TimeSpan?)null,
                        IsVerified = ta.IsVerified
                    })
                    .ToList();

                ViewBag.Teachers = db.Teachers.OrderBy(t => t.LastName).ThenBy(t => t.FirstName).ToList();
                return View(records);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // STUDENT ATTENDANCE REGISTER HISTORY (Admin)
        // ─────────────────────────────────────────────────────────────────────────

        public ActionResult StudentAttendanceHistory(int? subjectId, int? studentId, DateTime? fromDate, DateTime? toDate)
        {
            using (var db = DbContextFactory.Create())
            {
                // Base query for attendances (include navigation properties)
                var query = db.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.Subject)
                    .AsQueryable();

                // Apply filters
                if (subjectId.HasValue)
                    query = query.Where(a => a.SubjectId == subjectId.Value);
                if (studentId.HasValue)
                    query = query.Where(a => a.StudentId == studentId.Value);
                if (fromDate.HasValue)
                    query = query.Where(a => a.Date >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(a => a.Date <= toDate.Value);

                // 1. SUMMARY: Group by StudentId + SubjectId, compute counts
                var summaryRaw = query
                    .GroupBy(a => new { a.StudentId, a.SubjectId })
                    .Select(g => new
                    {
                        g.Key.StudentId,
                        g.Key.SubjectId,
                        TotalClasses = g.Count(),
                        PresentCount = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
                    })
                    .ToList();  // materialize to memory

                // Now join with Student and Subject tables to get names
                var summary = (from s in summaryRaw
                               join student in db.Students on s.StudentId equals student.StudentId
                               join subject in db.Subjects on s.SubjectId equals subject.SubjectId
                               select new AttendanceSummaryViewModel
                               {
                                   StudentId = s.StudentId,
                                   StudentName = student.FirstName + " " + student.LastName,   // adjust if you have FullName property
                                   GradeLevel = student.GradeLevel,
                                   SubjectId = s.SubjectId,
                                   SubjectName = subject.Name,   // ensure Subject has a 'Name' column
                                   TotalClasses = s.TotalClasses,
                                   PresentCount = s.PresentCount
                               })
                               .OrderBy(x => x.StudentName)
                               .ThenBy(x => x.SubjectName)
                               .ToList();

                // 2. DETAILED records (original)
                var records = query
                    .OrderByDescending(a => a.Date)
                    .Select(a => new AttendanceRecordViewModel
                    {
                        Date = a.Date,
                        StudentName = a.Student.FirstName + " " + a.Student.LastName,   // adjust as needed
                        StudentGrade = a.Student.GradeLevel,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    })
                    .ToList();

                // Dropdown data
                ViewBag.Subjects = db.Subjects.OrderBy(s => s.Name).ToList();
                ViewBag.Students = db.Students.OrderBy(s => s.FirstName).ToList();   // or use FullName
                ViewBag.AttendanceSummary = summary;

                return View(records);
            }
        }
        public ActionResult GetSubjectAttendanceDetails(int studentId, int subjectId)
        {
            using (var db = DbContextFactory.Create())
            {
                var records = db.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.Subject)
                    .Where(a => a.StudentId == studentId && a.SubjectId == subjectId)
                    .OrderByDescending(a => a.Date)
                    .Select(a => new AttendanceRecordViewModel
                    {
                        Date = a.Date,
                        StudentName = a.Student.FirstName + " " + a.Student.LastName,
                        StudentGrade = a.Student.GradeLevel,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    })
                    .ToList();

                if (!records.Any())
                {
                    return Content("<p class='text-muted'>No attendance records found for this subject.</p>");
                }

                // Return a partial view (or build HTML manually)
                return PartialView("_SubjectAttendanceDetails", records);
            }
        }

        public ActionResult FixLowerGradeTeacher()
        {
            using (var db = DbContextFactory.Create())
            {
                // Configuration – change these as needed
                string teacherEmail = "teacher.lower@michaelhouse.org";
                string teacherFirstName = "Jane";
                string teacherLastName = "Lower";
                int gradeLevel = 8; // or 9
                string defaultPassword = "Teacher@123";

                // 1. Get the fixed subjects for grade 8/9 from RegistrationService
                var gradeSubjects = RegistrationService.Grade8And9Subjects;

                // 2. Ensure each subject exists in the Subjects table (create if missing)
                foreach (string subjectName in gradeSubjects)
                {
                    var existingSubject = db.Subjects.FirstOrDefault(s => s.Name == subjectName && s.GradeLevel == gradeLevel);
                    if (existingSubject == null)
                    {
                        db.Subjects.Add(new Subject
                        {
                            Name = subjectName,
                            Code = subjectName.Replace(" ", "").Substring(0, Math.Min(3, subjectName.Length)) + gradeLevel,
                            GradeLevel = gradeLevel,
                            Stream = AcademicStream.None,
                            IsCompulsory = true, // All grade 8/9 subjects are compulsory
                            ApplicableGrades = gradeLevel.ToString()
                        });
                    }
                }
                db.SaveChanges();

                // 3. Find or create the teacher
                var teacher = db.Teachers.FirstOrDefault(t => t.Email == teacherEmail);
                if (teacher == null)
                {
                    teacher = new Teacher
                    {
                        FirstName = teacherFirstName,
                        LastName = teacherLastName,
                        Email = teacherEmail,
                        HireDate = DateTime.Now
                    };
                    db.Teachers.Add(teacher);
                    db.SaveChanges();
                }

                // 4. Create AppUser account if missing
                if (teacher.UserId == null)
                {
                    var user = new AppUser
                    {
                        Name = teacher.Name,
                        Email = teacher.Email,
                        PasswordHash = AccountController.HashPassword(defaultPassword),
                        Role = "Teacher"
                    };
                    db.Users.Add(user);
                    db.SaveChanges();
                    teacher.UserId = user.UserId;
                    db.Entry(teacher).State = EntityState.Modified;
                    db.SaveChanges();
                }

                // 5. Assign all grade 8/9 subjects to the teacher (no streams)
                var subjectsToAssign = db.Subjects.Where(s => s.GradeLevel == gradeLevel).ToList();
                foreach (var subject in subjectsToAssign)
                {
                    bool alreadyAssigned = db.TeacherSubjectGrades
                        .Any(tsg => tsg.TeacherId == teacher.TeacherId && tsg.SubjectId == subject.SubjectId);
                    if (!alreadyAssigned)
                    {
                        db.TeacherSubjectGrades.Add(new TeacherSubjectGrade
                        {
                            TeacherId = teacher.TeacherId,
                            SubjectId = subject.SubjectId,
                            Grade = gradeLevel,
                            Stream = AcademicStream.None
                        });
                    }
                }
                db.SaveChanges();

                return Content($@"
            <h3>Success!</h3>
            <p>Teacher <strong>{teacher.Name}</strong> (Email: {teacherEmail}) has been activated for Grade {gradeLevel}.</p>
            <p>Assigned {subjectsToAssign.Count} subjects (all from the fixed grade {gradeLevel} curriculum).</p>
            <p>Login credentials: <br/>Email: {teacherEmail}<br/>Password: {defaultPassword}</p>
        ");
            }
        }


    }
}