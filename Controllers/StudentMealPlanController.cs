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
        public ActionResult Index(string week)
        {
            int studentId = ResolveStudentId();

            if (studentId <= 0)
            {
                return new HttpStatusCodeResult(403, "No student profile linked to this account.");
            }

            try
            {
                var vm = _service.GetOrCreateDraft(studentId, ParseWeek(week));
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
        // The service checks ownership, the published menu, the
        // student's dietary profile and the selection deadline.
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
                _service.SavePicks(mealPlanId, studentId, picks);

                TempData["Success"] = "Your picks have been saved.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return BackToWeek(mealPlanId, studentId);
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
                _service.SavePicks(mealPlanId, studentId, picks);
                _service.Submit(mealPlanId, studentId);

                TempData["Success"] =
                    "Your meal plan has been submitted. Your choices go straight to the kitchen.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return BackToWeek(mealPlanId, studentId);
        }

        // Back to the week the student was working on
        private ActionResult BackToWeek(int mealPlanId, int studentId)
        {
            var weekStart = _db.MealPlans
                .Where(p => p.Id == mealPlanId && p.StudentId == studentId)
                .Select(p => (DateTime?)p.WeekStartDate)
                .FirstOrDefault();

            return weekStart.HasValue
                ? RedirectToAction("Index", new { week = weekStart.Value.ToString("yyyy-MM-dd") })
                : RedirectToAction("Index");
        }

        private static DateTime? ParseWeek(string week)
        {
            DateTime parsed;
            return DateTime.TryParseExact(week, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out parsed) ? parsed : (DateTime?)null;
        }

        // ============================================================
        // GET: StudentMealPlan/MyCollections
        // What the student has collected this week.
        //
        // BUG FIX (UC12 follow-up):
        //   Previously ordered the plan by WeekStartDate descending.
        //   When two approved plans overlap (e.g. 23–29 Sept and
        //   22–28 Sept), the later-starting one won — even if it was
        //   the one the dietitian approved days ago. Now we order by
        //   ReviewedAt (the approval timestamp). Same pattern as the
        //   fix in MealPlanService.GetOrCreateDraft.
        // ============================================================

        [HttpGet]
        public ActionResult MyCollections()
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return new HttpStatusCodeResult(403);

            // The most recent SUBMITTED meal plan (plans go straight to the
            // kitchen on submit; older Dietitian-approved plans count too).
            var plan = _db.MealPlans
                .Where(p => p.StudentId == studentId
                            && (p.Status == MealPlanStatus.Submitted
                                || p.Status == MealPlanStatus.SubmittedToDietitian
                                || p.Status == MealPlanStatus.Approved))
                .OrderByDescending(p => p.WeekStartDate)
                .ThenByDescending(p => p.Id)
                .FirstOrDefault();

            if (plan == null)
            {
                ViewBag.Message = "You haven't submitted a meal plan yet.";
                ViewBag.Collections = new List<MealCollection>();
                ViewBag.WeekStart = DateTime.Today;
                ViewBag.WeekEnd = DateTime.Today;
                return View();
            }

            // Pull collections for that plan's week.
            // We eager-load both the MealPlanItem and its MenuItem.
            var collections = _db.MealCollections
                .Include("MealPlanItem")
                .Include("MealPlanItem.MenuItem")
                .Where(c => c.StudentId == studentId
                            && c.Date >= plan.WeekStartDate
                            && c.Date <= plan.WeekEndDate)
                .OrderByDescending(c => c.Date)
                .ThenBy(c => c.MealSlot)
                .ToList();

            ViewBag.Collections = collections;
            ViewBag.WeekStart = plan.WeekStartDate;
            ViewBag.WeekEnd = plan.WeekEndDate;

            return View();
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