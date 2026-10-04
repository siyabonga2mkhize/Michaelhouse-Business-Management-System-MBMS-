using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Dietitian, Admin")]
    public class DietitianController : Controller
    {
        private readonly DBContextClass _db;
        private readonly MealPlanService _service;

        public DietitianController()
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
        // GET: Dietitian
        // Landing page — queue of plans awaiting review
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            var submitted = _service.GetSubmittedPlans();
            var recent = _service.GetRecentlyReviewedPlans();

            ViewBag.SubmittedPlans = submitted;
            ViewBag.RecentPlans = recent;

            return View();
        }

        // ============================================================
        // GET: Dietitian/Review/5
        // Full view of one plan
        // ============================================================

        [HttpGet]
        public ActionResult Review(int id)
        {
            var plan = _service.GetPlan(id);

            if (plan == null)
            {
                return HttpNotFound();
            }

            return View(plan);
        }

        // ============================================================
        // POST: Dietitian/Approve
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int id, string comment)
        {
            try
            {
                int userId = ResolveUserId();
                _service.Approve(id, userId, comment);

                TempData["Success"] = "Meal plan approved.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: Dietitian/SendBack
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendBack(int id, string comment)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(comment))
                {
                    TempData["Error"] = "Please explain what needs to change.";
                    return RedirectToAction("Review", new { id = id });
                }

                int userId = ResolveUserId();
                _service.SendBack(id, userId, comment);

                TempData["Success"] = "Meal plan sent back to the student.";
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

        private int ResolveUserId()
        {
            if (Session["UserId"] == null)
            {
                return 0;
            }

            int id;
            int.TryParse(Session["UserId"].ToString(), out id);
            return id;
        }
    }
}