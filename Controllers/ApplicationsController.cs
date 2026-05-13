using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class ApplicationsController : Controller
    {
        private readonly ApplicationService _appService = new ApplicationService();

        // ─── DEV HELPER (FIXED) ───────────────────────────────────────────────────
        // Gets an existing parent OR creates one if none exists
        private int GetCurrentParentId()
        {
            if (Session["ParentId"] == null)
            {
                // Parent record missing — send them to login
                Response.Redirect("~/Account/Login");
                return 0;
            }
            return (int)Session["ParentId"];
        }

        // ─── My Applications (Parent Dashboard) ───────────────────────────────────

        public ActionResult Index()
        {
            using (var db = new DBContextClass())
            {
                int parentId = GetCurrentParentId();

                var apps = db.Applications
                    .Include("Student")
                    .Include("Documents")
                    .Include("AdminReviews")
                    .Where(a => a.ParentId == parentId)
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

            using (var db = new DBContextClass())
            {
                int parentId = GetCurrentParentId();

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
                    ParentId = parentId
                });
                db.SaveChanges();

                TempData["Success"] = $"{vm.FirstName} {vm.LastName} has been added.";
                return RedirectToAction("Create");
            }
        }

        // ─── New Application Form ─────────────────────────────────────────────────

        public ActionResult Create()
        {
            using (var db = new DBContextClass())
            {
                int parentId = GetCurrentParentId();

                var students = db.Students
                    .Where(s => s.ParentId == parentId)
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
            if (!ModelState.IsValid)
            {
                using (var db = new DBContextClass())
                {
                    int parentId = GetCurrentParentId();
                    var students = db.Students.Where(s => s.ParentId == parentId).ToList();
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

            int currentParentId = GetCurrentParentId();

            var app = _appService.SubmitApplication(
                currentParentId,
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