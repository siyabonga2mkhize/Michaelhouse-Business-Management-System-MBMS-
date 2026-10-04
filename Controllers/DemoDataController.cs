using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // Demo Data (Admin only) — getting ready for a presentation
    //
    //   GET  /DemoData                 → face enrollments + demo meal plan form
    //   POST /DemoData/RemoveFace      → remove one student's face enrollment
    //   POST /DemoData/RemoveAllFaces  → remove every face enrollment
    //   POST /DemoData/CreateMealPlan  → submitted meal plan for today
    //                                    and the next few days
    //                                    (DemoDataService)
    // ============================================================
    [Authorize(Roles = "Admin")]
    public class DemoDataController : Controller
    {
        private readonly DBContextClass _db;

        public DemoDataController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Enrollments = _db.StudentFaceSignatures
                .Include(f => f.Student)
                .OrderBy(f => f.Student.FirstName).ThenBy(f => f.Student.LastName)
                .ToList();

            var enrolledIds = _db.StudentFaceSignatures.Where(f => f.IsActive).Select(f => f.StudentId).ToList();

            // Enrolled students first — they're the ones who can collect
            ViewBag.Students = _db.Students
                .Where(s => s.IsActive)
                .ToList()
                .OrderByDescending(s => enrolledIds.Contains(s.StudentId))
                .ThenBy(s => s.FirstName).ThenBy(s => s.LastName)
                .Select(s => new SelectListItem
                {
                    Value = s.StudentId.ToString(),
                    Text = (s.FirstName + " " + s.LastName).Trim()
                           + (string.IsNullOrEmpty(s.StudentNumber) ? "" : " (" + s.StudentNumber + ")")
                           + (enrolledIds.Contains(s.StudentId) ? " — face enrolled" : "")
                })
                .ToList();

            ViewBag.FaceEngineStatus = DlibFaceRecognitionService.StatusText();

            var today = SchoolClock.Today;
            ViewBag.Today = today;
            ViewBag.TodayMenu = new DemoDataService(_db).PublishedMenuFor(today);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveFace(int studentId)
        {
            var existing = _db.StudentFaceSignatures.Where(f => f.StudentId == studentId).ToList();
            _db.StudentFaceSignatures.RemoveRange(existing);
            _db.SaveChanges();

            TempData["Success"] = "Face enrollment removed.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveAllFaces()
        {
            var all = _db.StudentFaceSignatures.ToList();
            _db.StudentFaceSignatures.RemoveRange(all);
            _db.SaveChanges();

            TempData["Success"] = all.Count + " face enrollment(s) removed. Students can enrol again on their Face Enrollment page.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateMealPlan(int studentId, int days = 4, bool clearCollections = false)
        {
            try
            {
                var result = new DemoDataService(_db).CreateSubmittedMealPlan(studentId, days, clearCollections);

                if (result.Picks.Count > 0)
                {
                    TempData["Success"] = string.Format("Submitted meal plan for {0}: {1} meal(s) chosen{2}.",
                        result.StudentName, result.Picks.Count,
                        clearCollections ? ", " + result.CollectionsRemoved + " earlier collection(s) removed" : "");
                }
                TempData["Picks"] = string.Join("\n", result.Picks);
                TempData["Problems"] = string.Join("\n", result.Problems);
            }
            catch (Exception ex)
            {
                TempData["Problems"] = "The demo meal plan could not be created: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
