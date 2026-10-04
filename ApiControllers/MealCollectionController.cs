using Michaelhouse.Models;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // UC16 Mobile — Face Meal Collection
    //
    //   GET  /api/mealcollection/status
    //        → the meal being served now (same banner as the terminal)
    //   POST /api/mealcollection/verify  { imageBase64 }
    //        → identify the student and check they may collect
    //          the meal being served now; returns a one-time token
    //   POST /api/mealcollection/record  { token }
    //        → re-check everything and write the collection row
    //
    // Same rules as the web terminal (MealCollectionService). The
    // student, meal plan item, meal and slot are all worked out on
    // the server — nothing else from the request is trusted.
    // ============================================================
    [RoutePrefix("api/mealcollection")]
    [Authorize(Roles = "Chef, CafeteriaManager, Admin")]
    public class MealCollectionApiController : Controller
    {
        private readonly DBContextClass _db;
        private readonly MealCollectionService _collections;

        public MealCollectionApiController()
        {
            _db = new DBContextClass();
            _collections = new MealCollectionService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET /api/mealcollection/status
        // The meal being served now — the same banner as the web
        // terminal (MealCollectionController.Index)
        // ============================================================

        [HttpGet]
        [Route("status")]
        public JsonResult Status()
        {
            var now = SchoolClock.Now;
            var today = now.Date;
            var opening = CollectionOpening.Current(now);
            var slot = CollectionOpening.ServingSlot(now);

            return Json(new
            {
                ok = true,
                currentSlot = slot.HasValue ? slot.Value.ToString() : null,
                currentSlotHours = opening != null
                    ? string.Format("Opened manually by {0} until {1:HH:mm}", opening.OpenedBy, opening.OpenUntil)
                    : slot.HasValue
                        ? string.Format("Collection {0:hh\\:mm}–{1:hh\\:mm}", MealTimes.CollectionOpens(slot.Value), MealTimes.CollectionCloses(slot.Value))
                        : null,
                isManualOpening = opening != null,
                collectionHours = MealTimes.CollectionHours(),
                todayCount = _db.MealCollections.Count(c => c.Date == today)
            }, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // POST /api/mealcollection/verify
        // Body: { imageBase64 }
        // ============================================================

        [HttpPost]
        [Route("verify")]
        public JsonResult Verify()
        {
            var data = ReadBody<VerifyRequestModel>();
            byte[] photo = DecodePhoto(data == null ? null : data.ImageBase64);

            return Json(ToJson(_collections.Verify(photo, ResolveUserId())));
        }

        // ============================================================
        // POST /api/mealcollection/record
        // Body: { token }
        // ============================================================

        [HttpPost]
        [Route("record")]
        public JsonResult Record()
        {
            var data = ReadBody<RecordRequestModel>();

            return Json(ToJson(_collections.Record(data == null ? null : data.Token, ResolveUserId())));
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // No face-matching scores or student ids leave the server
        private static object ToJson(CollectionResult r)
        {
            return new
            {
                ok = r.IsSuccess,
                error = r.IsSuccess ? null : r.Title + " — " + r.Message,
                outcome = r.Outcome.ToString(),
                title = r.Title,
                message = r.Message,
                studentName = r.StudentName,
                studentNumber = r.StudentNumber,
                mealName = r.MealName,
                mealSlot = r.MealSlot,
                mealDate = r.MealDateLabel,
                collectedAt = r.CollectedAt,
                token = r.Token,
                allergens = r.MealAllergens,
                studentAllergies = r.StudentAllergies,
                dietaryPreference = r.DietaryPreference,
                medicalDietaryRestrictions = r.MedicalDietaryRestrictions,
                dietaryNotes = r.DietaryNotes,
                hasDietaryConflict = r.DietaryWarnings.Count > 0,
                dietaryConflicts = r.DietaryWarnings
            };
        }

        private T ReadBody<T>() where T : class
        {
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static byte[] DecodePhoto(string imageBase64)
        {
            if (string.IsNullOrWhiteSpace(imageBase64)) return null;

            int commaIdx = imageBase64.IndexOf(',');
            if (commaIdx > 0) imageBase64 = imageBase64.Substring(commaIdx + 1);

            try
            {
                return Convert.FromBase64String(imageBase64);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private int ResolveUserId()
        {
            if (Session["UserId"] == null) return 0;
            int id;
            int.TryParse(Session["UserId"].ToString(), out id);
            return id;
        }
    }

    public class VerifyRequestModel
    {
        public string ImageBase64 { get; set; }
    }

    public class RecordRequestModel
    {
        public string Token { get; set; }
    }
}
