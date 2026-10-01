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
        private readonly MealCollectionService _collections;

        public MealCollectionController()
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
        // GET: MealCollection
        // The collection terminal — camera + verify. The meal being
        // served is worked out on the server from school time.
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            var now = SchoolClock.Now;
            var today = now.Date;
            var opening = CollectionOpening.Current(now);
            var slot = CollectionOpening.ServingSlot(now);

            ViewBag.TodayCount = _db.MealCollections.Count(c => c.Date == today);
            ViewBag.CurrentSlot = slot.HasValue ? slot.Value.ToString() : null;
            ViewBag.CurrentSlotHours = opening != null
                ? string.Format("Opened manually by {0} until {1:HH:mm}", opening.OpenedBy, opening.OpenUntil)
                : slot.HasValue
                    ? string.Format("Collection {0:hh\\:mm}–{1:hh\\:mm}", MealTimes.CollectionOpens(slot.Value), MealTimes.CollectionCloses(slot.Value))
                    : null;
            ViewBag.IsManualOpening = opening != null;
            ViewBag.CollectionHours = MealTimes.CollectionHours();
            ViewBag.CanOpenCollection = User.IsInRole("CafeteriaManager") || User.IsInRole("Admin");
            ViewBag.DurationOptions = CollectionOpening.DurationOptions;

            return View();
        }

        // ============================================================
        // POST: MealCollection/Verify
        // Photo in; the server identifies the student and checks they
        // may collect the current meal. Nothing is recorded yet — staff
        // confirm with the one-time token this returns.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Verify(string imageBase64)
        {
            byte[] photo = DecodePhoto(imageBase64);
            return Json(ToJson(_collections.Verify(photo, ResolveUserId())));
        }

        // ============================================================
        // POST: MealCollection/Record
        // Token in — nothing else from the browser is used. The
        // service re-checks the student, plan, meal and duplicates
        // before writing the MealCollection.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Record(string token)
        {
            return Json(ToJson(_collections.Record(token, ResolveUserId())));
        }

        // ============================================================
        // POST: MealCollection/OpenCollection
        // Cafeteria Manager opens one of today's meals outside the
        // normal hours (testing / demonstrations). Only the time
        // window changes — every collection check still applies.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult OpenCollection(string slot, int minutes = 0)
        {
            MealSlot mealSlot;
            if (!Enum.TryParse(slot, out mealSlot) || !Enum.IsDefined(typeof(MealSlot), mealSlot)
                || !CollectionOpening.DurationOptions.Contains(minutes))
            {
                TempData["Error"] = "Choose a meal and how long to open collection for.";
                return RedirectToAction("Index");
            }

            var user = _db.Users.Find(ResolveUserId());
            var opening = CollectionOpening.Open(mealSlot, minutes, user != null ? user.Name : User.Identity.Name, SchoolClock.Now);

            TempData["Success"] = string.Format("{0} collection is open until {1:HH:mm}.", mealSlot, opening.OpenUntil);
            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: MealCollection/CloseCollection
        // Ends a manual opening; the normal hours apply again.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult CloseCollection()
        {
            CollectionOpening.Close();

            TempData["Success"] = "Manual opening closed. Normal collection hours apply.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // GET: MealCollection/ServedToday
        // Chef + Manager dashboard — how many meals served, by slot
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Chef, CafeteriaManager, Admin")]
        public ActionResult ServedToday()
        {
            var today = SchoolClock.Today;

            // Submitted plans (incl. older Dietitian-approved ones) covering today
            var approvedPlans = _db.MealPlans
                .Where(p => (p.Status == MealPlanStatus.Submitted
                             || p.Status == MealPlanStatus.SubmittedToDietitian
                             || p.Status == MealPlanStatus.Approved)
                            && p.WeekStartDate <= today && p.WeekEndDate >= today)
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
            var today = SchoolClock.Today;

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

        // What the terminal shows. No face-matching scores or student
        // ids are sent to the browser.
        private static object ToJson(CollectionResult r)
        {
            return new
            {
                success = r.IsSuccess,
                outcome = r.Outcome.ToString(),
                identityVerified = r.IdentityVerified,
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

        // Accepts a data URL or plain base64; null if unreadable
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
}
