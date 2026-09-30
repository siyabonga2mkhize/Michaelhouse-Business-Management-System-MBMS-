using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Chef, CafeteriaManager, Admin")]
    public class MealCollectionController : Controller
    {
        private readonly DBContextClass _db;
        private readonly IFaceRecognitionService _faceService;

        public MealCollectionController()
        {
            _db = new DBContextClass();
            _faceService = new SimulatedFaceRecognitionService();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: MealCollection
        // The collection terminal — camera + verify
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.TodayCount = _db.MealCollections
                .Count(c => c.Date == DateTime.Today);

            return View();
        }

        // ============================================================
        // POST: MealCollection/Verify
        // Takes a captured face image, matches it, looks up today's
        // meal for that student, and returns verification info.
        // Does NOT write a collection yet — staff confirms first.
        //
        // UC12 follow-up fixes:
        //   - Plan lookup now orders by ReviewedAt desc (same bug
        //     pattern that was causing the wrong plan to be picked
        //     when two approved plans overlapped).
        //   - Returns MealPlanItemId so Record can link it.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Verify(string imageBase64, string mealSlot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageBase64))
                {
                    return Json(new { success = false, message = "No image received." });
                }

                int commaIdx = imageBase64.IndexOf(',');
                if (commaIdx > 0) imageBase64 = imageBase64.Substring(commaIdx + 1);

                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(imageBase64);
                }
                catch
                {
                    return Json(new { success = false, message = "Invalid image data." });
                }

                if (imageBytes.Length < 500)
                {
                    return Json(new { success = false, message = "Image too small." });
                }

                // Generate signature for the captured image
                string candidate = _faceService.GenerateEncoding(imageBytes);

                // Load all enrolled signatures
                var enrolled = _db.StudentFaceSignatures
                    .Where(x => x.IsActive)
                    .ToList();

                if (enrolled.Count == 0)
                {
                    return Json(new { success = false, message = "No students are enrolled yet." });
                }

                // Match
                var match = _faceService.FindBestMatch(candidate, enrolled);

                if (match == null)
                {
                    return Json(new { success = false, message = "No match found." });
                }

                if (!match.IsAutoVerified)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Match confidence too low — please verify manually.",
                        confidence = match.Confidence
                    });
                }

                // Parse requested meal slot
                MealSlot slot;
                if (!Enum.TryParse(mealSlot, out slot))
                {
                    return Json(new { success = false, message = "Invalid meal slot." });
                }

                // Look up student
                var student = _db.Students
                    .Include("StudentProfile")
                    .FirstOrDefault(s => s.StudentId == match.StudentId);

                if (student == null)
                {
                    return Json(new { success = false, message = "Student not found." });
                }

                // Anti-fraud: already collected?
                var existing = _db.MealCollections
                    .FirstOrDefault(c => c.StudentId == student.StudentId
                                       && c.Date == DateTime.Today
                                       && c.MealSlot == slot);

                if (existing != null)
                {
                    return Json(new
                    {
                        success = false,
                        alreadyCollected = true,
                        studentName = student.FirstName + " " + student.LastName,
                        collectedAt = existing.CollectedAt.ToString("HH:mm"),
                        message = "Already collected this meal."
                    });
                }

                // Look up what they ordered via UC13.
                // BUG FIX: order by ReviewedAt desc — same pattern as
                // the fix in MealPlanService and StudentMealPlanController.
                var plan = _db.MealPlans
                    .Where(p => p.StudentId == student.StudentId
                                && p.Status == MealPlanStatus.Approved)
                    .OrderByDescending(p => p.ReviewedAt)
                    .ThenByDescending(p => p.Id)
                    .FirstOrDefault();

                string mealName = "(no approved meal plan)";
                int? mealPlanItemId = null;
                List<string> allergens = new List<string>();

                if (plan != null)
                {
                    var planItem = _db.MealPlanItems
                        .Include("MenuItem.Recipe.RecipeIngredients.Ingredient")
                        .FirstOrDefault(i => i.MealPlanId == plan.Id
                                          && i.Date == DateTime.Today
                                          && i.MealSlot == slot);

                    if (planItem != null && planItem.MenuItem != null)
                    {
                        mealName = planItem.MenuItem.Name;
                        mealPlanItemId = planItem.Id;

                        if (planItem.MenuItem.Recipe != null
                            && planItem.MenuItem.Recipe.RecipeIngredients != null)
                        {
                            foreach (var ri in planItem.MenuItem.Recipe.RecipeIngredients)
                            {
                                if (ri.Ingredient == null) continue;
                                if (string.IsNullOrWhiteSpace(ri.Ingredient.Allergens)) continue;
                                allergens.Add(ri.Ingredient.Allergens);
                            }
                        }
                    }
                }

                // Check student's own allergy list
                bool hasAllergenConflict = false;
                string studentAllergies = student.StudentProfile != null
                    ? student.StudentProfile.Allergies
                    : "";

                if (!string.IsNullOrWhiteSpace(studentAllergies)
                    && allergens.Count > 0)
                {
                    var studentSet = studentAllergies
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim().ToLower())
                        .ToHashSet();

                    foreach (var a in allergens)
                    {
                        foreach (var piece in a.Split(','))
                        {
                            if (studentSet.Contains(piece.Trim().ToLower()))
                            {
                                hasAllergenConflict = true;
                            }
                        }
                    }
                }

                return Json(new
                {
                    success = true,
                    studentId = student.StudentId,
                    studentName = student.FirstName + " " + student.LastName,
                    mealName = mealName,
                    mealSlot = slot.ToString(),
                    mealPlanItemId = mealPlanItemId,
                    confidence = match.Confidence,
                    matchDistance = Math.Round(match.Distance, 2),
                    allergens = string.Join(", ", allergens),
                    studentAllergies = studentAllergies,
                    hasAllergenConflict = hasAllergenConflict
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // POST: MealCollection/Record
        // Staff confirmed — write the collection row.
        //
        // BUG FIX: now accepts mealPlanItemId and links the row to
        // the student's UC13 pick. Previously this was left null,
        // which broke My Collections ("-" meal names) and any
        // reporting that walks MealCollection → MealPlanItem.
        //
        // If mealPlanItemId is null (student had no plan / no pick
        // for this slot), the collection is still recorded — the
        // student still ate — but it won't have a dish link.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Record(
            int studentId,
            string mealSlot,
            string method,
            decimal? confidence,
            int? mealPlanItemId)
        {
            try
            {
                MealSlot slot;
                if (!Enum.TryParse(mealSlot, out slot))
                {
                    return Json(new { success = false, message = "Invalid meal slot." });
                }

                int userId = ResolveUserId();

                // Defensive: if we were passed an id, confirm it belongs
                // to this student and slot. If not, silently drop it
                // rather than write a wrong link.
                int? verifiedPlanItemId = null;

                if (mealPlanItemId.HasValue)
                {
                    var candidate = _db.MealPlanItems
                        .Include("MealPlan")
                        .FirstOrDefault(i => i.Id == mealPlanItemId.Value);

                    if (candidate != null
                        && candidate.MealPlan != null
                        && candidate.MealPlan.StudentId == studentId
                        && candidate.Date.Date == DateTime.Today
                        && candidate.MealSlot == slot)
                    {
                        verifiedPlanItemId = candidate.Id;
                    }
                }

                // If the caller didn't pass an id (or it failed validation),
                // try to find one ourselves. Same ordering as Verify.
                if (!verifiedPlanItemId.HasValue)
                {
                    var fallbackPlan = _db.MealPlans
                        .Where(p => p.StudentId == studentId
                                    && p.Status == MealPlanStatus.Approved)
                        .OrderByDescending(p => p.ReviewedAt)
                        .ThenByDescending(p => p.Id)
                        .FirstOrDefault();

                    if (fallbackPlan != null)
                    {
                        var fallbackItem = _db.MealPlanItems
                            .FirstOrDefault(i => i.MealPlanId == fallbackPlan.Id
                                              && i.Date == DateTime.Today
                                              && i.MealSlot == slot);

                        if (fallbackItem != null)
                        {
                            verifiedPlanItemId = fallbackItem.Id;
                        }
                    }
                }

                var collection = new MealCollection
                {
                    StudentId = studentId,
                    Date = DateTime.Today,
                    MealSlot = slot,
                    MealPlanItemId = verifiedPlanItemId,
                    CollectedAt = DateTime.UtcNow,
                    VerifiedByMethod = string.Equals(method, "ManualOverride", StringComparison.OrdinalIgnoreCase)
                        ? CollectionMethod.ManualOverride
                        : CollectionMethod.FaceMatch,
                    MatchConfidence = confidence,
                    CollectedByUserId = userId > 0 ? (int?)userId : null
                };

                _db.MealCollections.Add(collection);
                _db.SaveChanges();

                return Json(new
                {
                    success = true,
                    collectionId = collection.Id,
                    collectedAt = collection.CollectedAt.ToString("HH:mm:ss"),
                    mealPlanItemId = verifiedPlanItemId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET: MealCollection/ServedToday
        // Chef + Manager dashboard — how many meals served, by slot
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Chef, CafeteriaManager, Admin")]
        public ActionResult ServedToday()
        {
            var today = DateTime.Today;

            var approvedPlans = _db.MealPlans
                .Where(p => p.Status == MealPlanStatus.Approved)
                .Select(p => p.Id)
                .ToList();

            var expectedToday = _db.MealPlanItems
                .Where(i => approvedPlans.Contains(i.MealPlanId)
                            && i.Date == today)
                .ToList();

            var collectedToday = _db.MealCollections
                .Include("Student")
                .Where(c => c.Date == today)
                .OrderByDescending(c => c.CollectedAt)
                .ToList();

            ViewBag.BreakfastExpected = expectedToday.Count(i => i.MealSlot == MealSlot.Breakfast);
            ViewBag.BreakfastServed = collectedToday.Count(c => c.MealSlot == MealSlot.Breakfast);

            ViewBag.LunchExpected = expectedToday.Count(i => i.MealSlot == MealSlot.Lunch);
            ViewBag.LunchServed = collectedToday.Count(c => c.MealSlot == MealSlot.Lunch);

            ViewBag.DinnerExpected = expectedToday.Count(i => i.MealSlot == MealSlot.Dinner);
            ViewBag.DinnerServed = collectedToday.Count(c => c.MealSlot == MealSlot.Dinner);

            ViewBag.TotalExpected = expectedToday.Count;
            ViewBag.TotalServed = collectedToday.Count;

            ViewBag.RecentCollections = collectedToday.Take(20).ToList();

            return View();
        }

        // ============================================================
        // GET: MealCollection/Log
        // Simple list of today's collections
        // ============================================================

        [HttpGet]
        public ActionResult Log()
        {
            var today = DateTime.Today;

            var collections = _db.MealCollections
                .Include("Student")
                .Where(c => c.Date == today)
                .OrderByDescending(c => c.CollectedAt)
                .ToList();

            return View(collections);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private int ResolveUserId()
        {
            if (Session["UserId"] == null) return 0;
            int id;
            int.TryParse(Session["UserId"].ToString(), out id);
            return id;
        }
    }
}