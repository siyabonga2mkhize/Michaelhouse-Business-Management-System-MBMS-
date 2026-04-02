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
    [ParentOnly]  // Parent completes registration — not the student
    public class RegistrationController : Controller
    {
        private readonly RegistrationService _regService = new RegistrationService();
        private readonly StudentAccountService _studentAccounts = new StudentAccountService();
        private readonly EmailService _email = new EmailService();

        private int GetCurrentParentId() => (int)Session["ParentId"];

        // ─── Registration Home ────────────────────────────────────────────────────
        // Parent lands here after application is approved

        public ActionResult Index(int appId)
        {
            var reg = _regService.GetRegistrationForApp(appId);

            if (reg == null)
            {
                TempData["Error"] = "No registration found for this application.";
                return RedirectToAction("Index", "Applications");
            }

            // Already completed
            if (reg.Status == RegistrationStatus.Completed)
                return RedirectToAction("Complete", new { appId });

            // Grade 10-12 needs subject selection first
            if (reg.GradeEnrolling >= 10 &&
                reg.Status != RegistrationStatus.SubjectsSelected)
                return RedirectToAction("SelectSubjects", new { appId });

            // Grade 8 & 9 or subjects already selected — go to confirm
            return RedirectToAction("Confirm", new { appId });
        }

        // ─── Select Subjects (Grade 10-12 only) ───────────────────────────────────

        public ActionResult SelectSubjects(int appId)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            // Grade 8 & 9 don't need subject selection
            if (reg.GradeEnrolling <= 9)
                return RedirectToAction("Confirm", new { appId });

            using (var db = new DBContextClass())
            {
                // Get already selected elective subjects if any
                var selected = db.StudentSubjects
                    .Include("Subject")
                    .Where(ss => ss.StudentId == reg.StudentId && ss.IsElective)
                    .Select(ss => ss.Subject.SubjectName)
                    .ToList();

                ViewBag.AppId = appId;
                ViewBag.RegistrationId = reg.RegistrationId;
                ViewBag.Grade = reg.GradeEnrolling;
                ViewBag.StudentName = reg.Student?.Name;
                ViewBag.ElectiveGroupA = RegistrationService.ElectiveGroupA;
                ViewBag.ElectiveGroupB = RegistrationService.ElectiveGroupB;
                ViewBag.CompulsorySubjects = RegistrationService.CompulsorySubjects;
                ViewBag.SelectedSubjects = selected;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SelectSubjects(
            int appId, int registrationId,
            string groupASubject, List<string> groupBSubjects)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            var (success, error) = _regService.SaveSubjectSelections(
                reg.StudentId, registrationId, groupASubject, groupBSubjects);

            if (!success)
            {
                TempData["Error"] = error;
                return RedirectToAction("SelectSubjects", new { appId });
            }

            TempData["Success"] = "Subjects saved successfully!";
            return RedirectToAction("Confirm", new { appId });
        }

        // ─── Confirm Registration ─────────────────────────────────────────────────

        public ActionResult Confirm(int appId)
        {
            var reg = _regService.GetRegistrationForApp(appId);
            if (reg == null) return HttpNotFound();

            using (var db = new DBContextClass())
            {
                var student = db.Students
                    .Include("Parent")
                    .FirstOrDefault(s => s.StudentId == reg.StudentId);

                var subjects = db.StudentSubjects
                    .Include("Subject")
                    .Where(ss => ss.StudentId == reg.StudentId)
                    .ToList();

                // Grade 8 & 9 — auto-assign subjects on first visit to confirm
                if (reg.GradeEnrolling <= 9 && !subjects.Any())
                {
                    _regService.AssignGrade8And9Subjects(reg.StudentId);
                    subjects = db.StudentSubjects
                        .Include("Subject")
                        .Where(ss => ss.StudentId == reg.StudentId)
                        .ToList();
                }

                bool needsSubjects = reg.GradeEnrolling >= 10 &&
                                     reg.Status != RegistrationStatus.SubjectsSelected &&
                                     reg.Status != RegistrationStatus.Completed;

                ViewBag.AppId = appId;
                ViewBag.Reg = reg;
                ViewBag.Student = student;
                ViewBag.Subjects = subjects;
                ViewBag.NeedsSubjects = needsSubjects;
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

            // Grade 10-12 must have subjects selected
            if (reg.GradeEnrolling >= 10 &&
                reg.Status != RegistrationStatus.SubjectsSelected)
            {
                TempData["Error"] = "Please select subjects before completing registration.";
                return RedirectToAction("SelectSubjects", new { appId });
            }

            // Mark registration as complete
            _regService.CompleteRegistration(registrationId);

            // NOW create the student account
            try
            {
                var (user, tempPassword) = _studentAccounts.CreateStudentAccount(reg.StudentId);

                if (tempPassword != null)
                {
                    // Email credentials to parent
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

                    TempData["Success"] =
                        $"Registration complete! A student account has been created. " +
                        $"Login details have been sent to your email address.";
                }
                else
                {
                    TempData["Success"] = "Registration completed successfully!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Student account creation failed: {ex.Message}");
                TempData["Success"] = "Registration completed! Please contact the school if you don't receive login credentials.";
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
                var student = db.Students.Find(reg.StudentId);
                var subjects = db.StudentSubjects
                    .Include("Subject")
                    .Where(ss => ss.StudentId == reg.StudentId)
                    .ToList();

                ViewBag.Reg = reg;
                ViewBag.Student = student;
                ViewBag.Subjects = subjects;
            }

            return View();
        }
    }
}