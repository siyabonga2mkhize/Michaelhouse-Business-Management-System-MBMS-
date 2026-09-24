using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Student, Admin")]
    public class StudentMealPlanController : Controller
    {
        private readonly DBContextClass _db;
        private readonly MealPlanService _service;

        public StudentMealPlanController()
        {
            _db = new DBContextClass();
            _service = new MealPlanService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: StudentMealPlan
        // The student's personal meal plan builder.
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
                var vm = _service.GetOrCreateDraft(studentId);
                return View(vm);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return View("NoMenu");
            }
        }

        // ============================================================
        // POST: StudentMealPlan/Save
        // Saves the student's current picks. Does not submit.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Save(int mealPlanId)
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return new HttpStatusCodeResult(403);

            try
            {
                var picks = ExtractPicks(Request.Form);
                _service.SavePicks(mealPlanId, picks);

                TempData["Success"] = "Your picks have been saved.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: StudentMealPlan/Submit
        // Saves the current picks AND submits to the Dietitian.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(int mealPlanId)
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return new HttpStatusCodeResult(403);

            try
            {
                var picks = ExtractPicks(Request.Form);

                // Save first, then submit
                _service.SavePicks(mealPlanId, picks);
                _service.Submit(mealPlanId);

                TempData["Success"] =
                    "Your meal plan has been submitted to the Dietitian for review.";
            }
            catch (Exception ex)
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

            // Fall back to lookup by userId
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

        // Reads radio button values from the form.
        // Each pick is named "pick[yyyy-MM-dd|Slot]"
        private Dictionary<string, int> ExtractPicks(System.Collections.Specialized.NameValueCollection form)
        {
            var picks = new Dictionary<string, int>();

            foreach (string key in form.AllKeys)
            {
                if (key == null) continue;
                if (!key.StartsWith("pick[", StringComparison.OrdinalIgnoreCase)) continue;

                // Extract the slot key from "pick[2026-09-22|Lunch]"
                int openBracket = key.IndexOf('[');
                int closeBracket = key.IndexOf(']');
                if (openBracket < 0 || closeBracket < 0) continue;

                string slotKey = key.Substring(openBracket + 1, closeBracket - openBracket - 1);

                int menuItemId;
                if (int.TryParse(form[key], out menuItemId))
                {
                    picks[slotKey] = menuItemId;
                }
            }

            return picks;
        }
    }
}