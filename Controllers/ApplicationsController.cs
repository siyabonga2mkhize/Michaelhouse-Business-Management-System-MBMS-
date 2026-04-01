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
        private int GetCurrentParentId(DBContextClass db)
        {
            var parent = db.Parents.FirstOrDefault();

            if (parent == null)
            {
                parent = new Parent
                {
                    Name = "Dev Parent",
                    Contact = "dev@school.co.za",
                    UserId = null
                };

                db.Parents.Add(parent);
                db.SaveChanges();
            }

            return parent.ParentId;
        }

        // ─── My Applications (Parent Dashboard) ───────────────────────────────────

        public ActionResult Index()
        {
            using (var db = new DBContextClass())
            {
                int parentId = GetCurrentParentId(db);

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
                int parentId = GetCurrentParentId(db);

                db.Students.Add(new Student
                {
                    Name = vm.Name,
                    DOB = vm.DOB,
                    ParentId = parentId
                });

                db.SaveChanges();

                TempData["Success"] = $"{vm.Name} has been added successfully.";
                return RedirectToAction("Index");
            }
        }

        // ─── New Application Form ─────────────────────────────────────────────────

        public ActionResult Create()
        {
            using (var db = new DBContextClass())
            {
                int parentId = GetCurrentParentId(db);

                var students = db.Students
                    .Where(s => s.ParentId == parentId)
                    .ToList();

                if (!students.Any())
                {
                    TempData["Info"] = "Please add a student first before submitting an application.";
                    return RedirectToAction("AddStudent");
                }

                ViewBag.Students = new SelectList(students, "StudentId", "Name");
                ViewBag.CurrentYear = DateTime.Now.Year;

                return View();
            }
        }

        // ─── Submit Application ───────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ApplicationCreateViewModel vm)
        {
            using (var db = new DBContextClass())
            {
                int parentId = GetCurrentParentId(db);

                if (!ModelState.IsValid)
                {
                    ViewBag.Students = new SelectList(
                        db.Students.Where(s => s.ParentId == parentId).ToList(),
                        "StudentId", "Name");

                    ViewBag.CurrentYear = DateTime.Now.Year;
                    return View(vm);
                }

                var files = Request.Files;

                if (files == null || files.Count == 0)
                {
                    ModelState.AddModelError("", "Please upload at least one document.");

                    ViewBag.Students = new SelectList(
                        db.Students.Where(s => s.ParentId == parentId).ToList(),
                        "StudentId", "Name");

                    return View(vm);
                }

                var app = _appService.SubmitApplication(
                    parentId,
                    vm.StudentId,
                    vm.ApplicationYear,
                    files,
                    vm.DocumentTypes);

                // Fire AI review in background
                Task.Run(() => _appService.TriggerAiReviewAsync(app.AppId));

                TempData["Success"] = "Application submitted! AI review has been triggered.";
                return RedirectToAction("Status", new { id = app.AppId });
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