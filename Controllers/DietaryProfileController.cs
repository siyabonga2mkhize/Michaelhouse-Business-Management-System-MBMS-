using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // Student Dietary Profile
    //
    // The student views and edits their dietary preference, food
    // allergies, medical dietary restrictions and notes. Stored on
    // StudentProfile; rules live in DietaryProfileService.
    // ============================================================

    [Authorize(Roles = "Student, Admin")]
    public class DietaryProfileController : Controller
    {
        private readonly DBContextClass _db;
        private readonly DietaryProfileService _service;

        public DietaryProfileController()
        {
            _db = new DBContextClass();
            _service = new DietaryProfileService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: DietaryProfile
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0)
            {
                return new HttpStatusCodeResult(403, "No student profile linked to this account.");
            }

            try
            {
                return View(_service.GetForStudent(studentId));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return View(new DietaryProfileViewModel());
            }
        }

        // ============================================================
        // POST: DietaryProfile/Save
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(DietaryProfileViewModel model)
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return new HttpStatusCodeResult(403);

            foreach (var error in _service.Validate(model))
            {
                ModelState.AddModelError("", error);
            }

            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            try
            {
                _service.Save(studentId, model);
                TempData["Success"] = "Your dietary profile has been saved.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private int ResolveStudentId()
        {
            if (Session["StudentId"] != null)
            {
                int id;
                if (int.TryParse(Session["StudentId"].ToString(), out id))
                {
                    return id;
                }
            }

            if (Session["UserId"] != null)
            {
                int userId;
                if (int.TryParse(Session["UserId"].ToString(), out userId))
                {
                    var student = _db.Students.FirstOrDefault(s => s.UserId == userId);
                    if (student != null)
                    {
                        Session["StudentId"] = student.StudentId;
                        return student.StudentId;
                    }
                }
            }

            return 0;
        }
    }
}
