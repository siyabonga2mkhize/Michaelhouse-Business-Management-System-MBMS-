using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [Require
        
        ]
    public class ApplicationsController : Controller
    {
        private readonly ApplicationService _appService = new ApplicationService();

        private int GetParentId()
        {
            return Session["ParentId"] == null ? 0 : (int)Session["ParentId"];
        }

        private ActionResult RedirectIfNoParent()
        {
            if (Session["ParentId"] == null)
                return RedirectToAction("Login", "Account");
            return null;
        }

        // ─── My Applications (Parent Dashboard) ───────────────────────────────────

        public ActionResult Index()
        {
            var redirect = RedirectIfNoParent();
            if (redirect != null) return redirect;

            using (var db = new DBContextClass())
            {
                var apps = db.Applications
                    .Include("Student")
                    .Include("Documents")
                    .Include("AdminReviews")
                    .Where(a => a.ParentId == GetParentId())
                    .OrderByDescending(a => a.Date)
                    .ToList();

                return View(apps);
            }
        }

        // ─── Add Student ──────────────────────────────────────────────────────────

        public ActionResult AddStudent() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddStudent(AddStudentViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var redirect = RedirectIfNoParent();
            if (redirect != null) return redirect;

            using (var db = new DBContextClass())
            {
                db.Students.Add(new Student
                {
                    FirstName = vm.FirstName,
                    LastName = vm.LastName,
                    DOB = vm.DOB,
                    HomeLanguage = vm.HomeLanguage,
                    IdNumber = vm.IdNumber,
                    PreviousSchool = vm.PreviousSchool,
                    CurrentGrade = vm.CurrentGrade,
                    MedicalConditions = vm.MedicalConditions,
                    ParentId = GetParentId()
                });
                db.SaveChanges();

                TempData["Success"] = $"{vm.FirstName} {vm.LastName} has been added.";
                return RedirectToAction("Create");
            }
        }

        // ─── New Application Form ─────────────────────────────────────────────────

        public ActionResult Create()
        {
            var redirect = RedirectIfNoParent();
            if (redirect != null) return redirect;

            using (var db = new DBContextClass())
            {
                var students = db.Students
                    .Where(s => s.ParentId == GetParentId())
                    .ToList();

                if (!students.Any())
                {
                    TempData["Info"] = "Please add a student first.";
                    return RedirectToAction("AddStudent");
                }

                ViewBag.Students = new SelectList(students, "StudentId", "Name");
                ViewBag.CurrentYear = DateTime.Now.Year;

                // Grades 8–12 for Michaelhouse
                ViewBag.Grades = new SelectList(new[]
                {
                    new { Value = 8,  Text = "Grade 8"  },
                    new { Value = 9,  Text = "Grade 9"  },
                    new { Value = 10, Text = "Grade 10" },
                    new { Value = 11, Text = "Grade 11" },
                    new { Value = 12, Text = "Grade 12" }
                }, "Value", "Text");

                return View();
            }
        }

        // ─── Submit Application ───────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ApplicationCreateViewModel vm)
        {
            var redirect = RedirectIfNoParent();
            if (redirect != null) return redirect;

            if (!ModelState.IsValid)
            {
                using (var db = new DBContextClass())
                {
                    var students = db.Students.Where(s => s.ParentId == GetParentId()).ToList();
                    ViewBag.Students = new SelectList(students, "StudentId", "Name");
                    ViewBag.CurrentYear = DateTime.Now.Year;
                    ViewBag.Grades = new SelectList(new[]
                    {
                        new { Value = 8,  Text = "Grade 8"  },
                        new { Value = 9,  Text = "Grade 9"  },
                        new { Value = 10, Text = "Grade 10" },
                        new { Value = 11, Text = "Grade 11" },
                        new { Value = 12, Text = "Grade 12" }
                    }, "Value", "Text");
                }
                return View(vm);
            }

            var files = Request.Files;
            if (files == null || files.Count == 0)
            {
                ModelState.AddModelError("", "Please upload at least one document.");
                return View(vm);
            }

            var app = _appService.SubmitApplication(
                GetParentId(),
                vm.StudentId,
                vm.ApplicationYear,
                vm.GradeApplying,
                vm.AdditionalNotes,
                files,
                vm.DocumentTypes);

            Task.Run(() => _appService.TriggerAiReviewAsync(app.AppId));

            TempData["Success"] = "Application submitted! AI review has been triggered.";
            return RedirectToAction("Status", new { id = app.AppId });
        }

        public ActionResult SyncTeacher(int teacherId)
        {
            using (var db = new DBContextClass())
            {
                var teacher = db.Teachers.Find(teacherId);

                // If the teacher exists but has no UserId linked
                if (teacher != null && teacher.UserId == null)
                {
                    var newUser = new AppUser
                    {
                        Name = $"{teacher.FirstName} {teacher.LastName}",
                        Email = teacher.Email,
                        PasswordHash = AccountController.HashPassword("Teacher@123"), // Default pass
                        Role = "Teacher"
                    };

                    db.Users.Add(newUser);
                    db.SaveChanges(); // Generates the UserId

                    teacher.UserId = newUser.UserId;
                    db.SaveChanges(); // Links the Teacher to the User

                    return Content("Teacher successfully linked to AppUser account.");
                }
                return Content("Teacher already linked or not found.");
            }
        }

        // ─── Application Status Page ──────────────────────────────────────────────

        public ActionResult Status(int id)
        {
            using (var db = new DBContextClass())
            {
                var app = db.Applications
                    .Include("Student")
                    .Include("Documents")
                    .Include("AdminReviews")
                    .FirstOrDefault(a => a.AppId == id);

                if (app == null) return HttpNotFound();

                return View(app);
            }
        }
    }
}