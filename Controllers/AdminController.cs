using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    [AdminOnly]  // All actions require Admin role
    public class AdminController : Controller
    {
        private readonly ApplicationService _appService = new ApplicationService();

        private string GetCurrentAdminId()
        {
            return Session["UserId"]?.ToString();
        }

        // ─── Dashboard ────────────────────────────────────────────────────────────

        public ActionResult Dashboard()
        {
            using (var db = new DBContextClass())
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
            using (var db = new DBContextClass())
            {
                var app = db.Applications
                    .Include("Student")
                    .Include("Parent")
                    .Include("Documents")
                    .Include("AdminReviews")
                    .FirstOrDefault(a => a.AppId == id);

                if (app == null) return HttpNotFound();
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

            _appService.AdminConfirm(
                vm.AppId,
                GetCurrentAdminId(),
                vm.Decision,
                vm.Notes,
                vm.AgreedWithAi);

            TempData["Success"] = $"Application #{vm.AppId} marked as {vm.Decision}.";
            return RedirectToAction("Dashboard");
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

        // ─── View Document ────────────────────────────────────────────────────────

        public ActionResult ViewDocument(int id)
        {
            using (var db = new DBContextClass())
            {
                var doc = db.Documents.Find(id);
                if (doc == null) return HttpNotFound();

                var uploadRoot = System.Web.Hosting.HostingEnvironment.MapPath(
                    System.Configuration.ConfigurationManager.AppSettings["DocumentStorage:UploadRoot"]
                    ?? "~/App_Data/Uploads");

                var fullPath = System.IO.Path.Combine(uploadRoot, doc.FilePath);
                if (!System.IO.File.Exists(fullPath)) return HttpNotFound();

                return File(System.IO.File.ReadAllBytes(fullPath), doc.ContentType, doc.FileName);
            }
        }
    }
}