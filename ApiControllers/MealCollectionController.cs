using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // UC16 Mobile — Face Meal Collection
    //
    //   POST /api/mealcollection/verify  → match face, return student
    //   POST /api/mealcollection/record  → write collection row
    // ============================================================
    [RoutePrefix("api/mealcollection")]
    public class MealCollectionApiController : Controller
    {
        private readonly DBContextClass _db;
        private readonly IFaceRecognitionService _faceService;

        public MealCollectionApiController()
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
        // POST /api/mealcollection/verify
        // Body: { imageBase64, mealSlot }
        // ============================================================

        [HttpPost]
        [Route("verify")]
        public JsonResult Verify()
        {
            try
            {
                string body;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    body = reader.ReadToEnd();
                }

                var data = JsonConvert.DeserializeObject<VerifyRequestModel>(body);
                if (data == null || string.IsNullOrWhiteSpace(data.ImageBase64))
                {
                    return Json(new { ok = false, error = "No image received." });
                }

                // Strip data URL prefix if present
                string b64 = data.ImageBase64;
                int commaIdx = b64.IndexOf(',');
                if (commaIdx > 0) b64 = b64.Substring(commaIdx + 1);

                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(b64);
                }
                catch
                {
                    return Json(new { ok = false, error = "Invalid image data." });
                }

                if (imageBytes.Length < 500)
                {
                    return Json(new { ok = false, error = "Image too small." });
                }

                // Parse slot
                MealSlot slot;
                if (!Enum.TryParse(data.MealSlot, out slot))
                {
                    return Json(new { ok = false, error = "Invalid meal slot." });
                }

                // Generate signature + match
                string candidate = _faceService.GenerateEncoding(imageBytes);
                var enrolled = _db.StudentFaceSignatures.Where(x => x.IsActive).ToList();

                if (enrolled.Count == 0)
                {
                    return Json(new { ok = false, error = "No students are enrolled yet." });
                }

                var match = _faceService.FindBestMatch(candidate, enrolled);

                if (match == null)
                {
                    return Json(new { ok = false, error = "No match found." });
                }

                if (!match.IsAutoVerified)
                {
                    return Json(new
                    {
                        ok = false,
                        error = "Match confidence too low — please verify manually.",
                        confidence = match.Confidence
                    });
                }

                // Get student
                var student = _db.Students
                    .Include("StudentProfile")
                    .FirstOrDefault(s => s.StudentId == match.StudentId);

                if (student == null)
                {
                    return Json(new { ok = false, error = "Student not found." });
                }

                // Already collected?
                var existing = _db.MealCollections
                    .FirstOrDefault(c => c.StudentId == student.StudentId
                                       && c.Date == DateTime.Today
                                       && c.MealSlot == slot);

                if (existing != null)
                {
                    return Json(new
                    {
                        ok = false,
                        alreadyCollected = true,
                        studentId = student.StudentId,
                        studentName = student.FirstName + " " + student.LastName,
                        collectedAt = existing.CollectedAt.ToString("HH:mm"),
                        error = "Already collected this meal."
                    });
                }

                // Look up approved plan
                var plan = _db.MealPlans
                    .Where(p => p.StudentId == student.StudentId
                                && p.Status == MealPlanStatus.Approved)
                    .OrderByDescending(p => p.ReviewedAt)
                    .ThenByDescending(p => p.Id)
                    .FirstOrDefault();

                string mealName = "(no approved meal plan)";
                int? mealPlanItemId = null;

                if (plan != null)
                {
                    var planItem = _db.MealPlanItems
                        .Include("MenuItem")
                        .FirstOrDefault(i => i.MealPlanId == plan.Id
                                          && i.Date == DateTime.Today
                                          && i.MealSlot == slot);

                    if (planItem != null && planItem.MenuItem != null)
                    {
                        mealName = planItem.MenuItem.Name;
                        mealPlanItemId = planItem.Id;
                    }
                }

                return Json(new
                {
                    ok = true,
                    studentId = student.StudentId,
                    studentName = student.FirstName + " " + student.LastName,
                    studentNumber = student.StudentNumber,
                    mealName = mealName,
                    mealSlot = slot.ToString(),
                    mealPlanItemId = mealPlanItemId,
                    confidence = match.Confidence
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = "Server error: " + ex.Message });
            }
        }

        // ============================================================
        // POST /api/mealcollection/record
        // Body: { studentId, mealSlot, method, confidence, mealPlanItemId }
        // ============================================================

        [HttpPost]
        [Route("record")]
        public JsonResult Record()
        {
            try
            {
                string body;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    body = reader.ReadToEnd();
                }

                var data = JsonConvert.DeserializeObject<RecordRequestModel>(body);
                if (data == null || data.StudentId <= 0)
                {
                    return Json(new { ok = false, error = "Missing studentId." });
                }

                MealSlot slot;
                if (!Enum.TryParse(data.MealSlot, out slot))
                {
                    return Json(new { ok = false, error = "Invalid meal slot." });
                }

                // Verify the plan item belongs to this student
                int? verifiedPlanItemId = null;
                if (data.MealPlanItemId.HasValue)
                {
                    var candidate = _db.MealPlanItems
                        .Include("MealPlan")
                        .FirstOrDefault(i => i.Id == data.MealPlanItemId.Value);

                    if (candidate != null
                        && candidate.MealPlan != null
                        && candidate.MealPlan.StudentId == data.StudentId
                        && candidate.Date.Date == DateTime.Today
                        && candidate.MealSlot == slot)
                    {
                        verifiedPlanItemId = candidate.Id;
                    }
                }

                var collection = new MealCollection
                {
                    StudentId = data.StudentId,
                    Date = DateTime.Today,
                    MealSlot = slot,
                    MealPlanItemId = verifiedPlanItemId,
                    CollectedAt = DateTime.UtcNow,
                    VerifiedByMethod = string.Equals(data.Method, "ManualOverride", StringComparison.OrdinalIgnoreCase)
                        ? CollectionMethod.ManualOverride
                        : CollectionMethod.FaceMatch,
                    MatchConfidence = data.Confidence
                };

                _db.MealCollections.Add(collection);
                _db.SaveChanges();

                return Json(new
                {
                    ok = true,
                    collectionId = collection.Id,
                    collectedAt = collection.CollectedAt.ToString("HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = "Server error: " + ex.Message });
            }
        }
    }

    public class VerifyRequestModel
    {
        public string ImageBase64 { get; set; }
        public string MealSlot { get; set; }
    }

    public class RecordRequestModel
    {
        public int StudentId { get; set; }
        public string MealSlot { get; set; }
        public string Method { get; set; }
        public decimal? Confidence { get; set; }
        public int? MealPlanItemId { get; set; }
    }
}