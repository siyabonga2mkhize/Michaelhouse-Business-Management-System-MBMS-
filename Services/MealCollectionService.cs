using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Caching;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC16 — Meal collection (verify, then record)
    //
    // Used by the collection terminal (MealCollectionController) and
    // the mobile API. Nothing the client sends is trusted except the
    // photo and the one-time token this service issued:
    //
    //   Verify(photo)
    //     1. the date and meal are worked out here from school time
    //        (SchoolClock + MealTimes, or a manual opening by the
    //        Cafeteria Manager — CollectionOpening) — never sent by
    //        the client
    //     2. the face is matched against every enrolled student
    //     3. the student must have a SUBMITTED meal plan with a meal
    //        chosen for today's meal, on the published menu, not yet
    //        collected
    //     4. if all is well, a short-lived single-use token is issued,
    //        held on the server with the student / plan item it covers
    //
    //   Record(token)
    //     re-checks everything for the student the token names, then
    //     writes one MealCollection linked to their MealPlanItem.
    //     A face match alone never creates a collection.
    // ============================================================

    public enum CollectionOutcome
    {
        ReadyForCollection = 1,
        Collected = 2,
        FaceNotRecognised = 3,
        InvalidPhoto = 4,
        NoMealBeingServed = 5,
        MealPlanNotFound = 6,
        NoMealSelected = 7,
        AlreadyCollected = 8,
        InvalidMeal = 9,
        VerificationExpired = 10
    }

    public class CollectionResult
    {
        public CollectionResult()
        {
            DietaryWarnings = new List<string>();
        }

        public CollectionOutcome Outcome { get; set; }

        public bool IsSuccess
        {
            get { return Outcome == CollectionOutcome.ReadyForCollection || Outcome == CollectionOutcome.Collected; }
        }

        // True once the face has been matched to a student
        public bool IdentityVerified { get; set; }

        // Short heading, e.g. "Identity verified" / "Meal already collected"
        public string Title { get; set; }

        // Plain-language explanation for the terminal
        public string Message { get; set; }

        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string MealName { get; set; }
        public string MealSlot { get; set; }
        public string MealDateLabel { get; set; }

        // School time of collection (HH:mm), when collected
        public string CollectedAt { get; set; }

        // One-time token to confirm the collection (ReadyForCollection only)
        public string Token { get; set; }

        // Kitchen information about the student and dish
        public string MealAllergens { get; set; }
        public string StudentAllergies { get; set; }
        public string DietaryPreference { get; set; }
        public string MedicalDietaryRestrictions { get; set; }
        public string DietaryNotes { get; set; }
        public List<string> DietaryWarnings { get; set; }
    }

    public class MealCollectionService
    {
        // How long a verification can be confirmed for
        public static readonly TimeSpan VerificationLifetime = TimeSpan.FromMinutes(2);

        private const string CachePrefix = "MealCollection.Pending.";

        private readonly DBContextClass _db;
        private readonly IFaceRecognitionService _face;
        private readonly Func<DateTime> _now;

        public MealCollectionService(DBContextClass db)
            : this(db, null, null)
        {
        }

        // face / now can be replaced in tests; default to the app's
        // face service and school time
        public MealCollectionService(DBContextClass db, IFaceRecognitionService face, Func<DateTime> now)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _face = face ?? new SimulatedFaceRecognitionService();
            _now = now ?? (() => SchoolClock.Now);
        }

        // The verified details a token stands for — server side only
        private class PendingCollection
        {
            public int StudentId { get; set; }
            public int MealPlanItemId { get; set; }
            public int MenuItemId { get; set; }
            public DateTime Date { get; set; }
            public MealSlot Slot { get; set; }
            public decimal Confidence { get; set; }
            public int StaffUserId { get; set; }
        }

        // ============================================================
        // VERIFY
        // ============================================================

        public CollectionResult Verify(byte[] photo, int staffUserId)
        {
            var now = _now();
            var slot = CollectionOpening.ServingSlot(now);   // timetable, or a manual opening

            if (!slot.HasValue)
            {
                return new CollectionResult
                {
                    Outcome = CollectionOutcome.NoMealBeingServed,
                    Title = "No meal is being served",
                    Message = "Meals can be collected during: " + MealTimes.CollectionHours() + "."
                };
            }

            var encoding = _face.GenerateEncoding(photo);
            if (encoding == null)
            {
                return new CollectionResult
                {
                    Outcome = CollectionOutcome.InvalidPhoto,
                    Title = "Photo could not be read",
                    Message = "Please try again, with the student's face in the frame."
                };
            }

            var enrolled = _db.StudentFaceSignatures.Where(x => x.IsActive).ToList();
            var match = _face.FindBestMatch(encoding, enrolled);

            if (match == null || !match.IsAutoVerified)
            {
                return new CollectionResult
                {
                    Outcome = CollectionOutcome.FaceNotRecognised,
                    Title = "Identity could not be verified",
                    Message = "Please try again or contact the cafeteria."
                };
            }

            var result = CheckEntitlement(match.StudentId, now.Date, slot.Value);
            result.IdentityVerified = true;

            if (result.Outcome != CollectionOutcome.ReadyForCollection)
            {
                return result;
            }

            // Issue the one-time token; the details stay on the server
            var token = Guid.NewGuid().ToString("N");
            HttpRuntime.Cache.Insert(
                CachePrefix + token,
                new PendingCollection
                {
                    StudentId = match.StudentId,
                    MealPlanItemId = result.PlanItem.Id,
                    MenuItemId = result.PlanItem.MenuItemId,
                    Date = now.Date,
                    Slot = slot.Value,
                    Confidence = match.Confidence,
                    StaffUserId = staffUserId
                },
                null,
                DateTime.UtcNow.Add(VerificationLifetime),
                Cache.NoSlidingExpiration);

            result.Token = token;
            return result;
        }

        // ============================================================
        // RECORD
        // ============================================================

        public CollectionResult Record(string token, int staffUserId)
        {
            var pending = string.IsNullOrWhiteSpace(token)
                ? null
                : HttpRuntime.Cache.Remove(CachePrefix + token) as PendingCollection;   // single use

            if (pending == null || pending.StaffUserId != staffUserId)
            {
                return Expired();
            }

            // The meal being served must still be the one verified
            var now = _now();
            if (now.Date != pending.Date || CollectionOpening.ServingSlot(now) != pending.Slot)
            {
                return Expired();
            }

            using (var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    var result = CheckEntitlement(pending.StudentId, pending.Date, pending.Slot);
                    result.IdentityVerified = true;

                    if (result.Outcome != CollectionOutcome.ReadyForCollection)
                    {
                        tx.Rollback();
                        return result;
                    }

                    // The selection must still be the one that was verified
                    if (result.PlanItem.Id != pending.MealPlanItemId || result.PlanItem.MenuItemId != pending.MenuItemId)
                    {
                        tx.Rollback();
                        result.Outcome = CollectionOutcome.InvalidMeal;
                        result.Title = "Meal doesn't match";
                        result.Message = "The student's meal plan changed after verification. Please scan again.";
                        return result;
                    }

                    var collection = new MealCollection
                    {
                        StudentId = pending.StudentId,
                        MealPlanItemId = pending.MealPlanItemId,
                        Date = pending.Date,
                        MealSlot = pending.Slot,
                        CollectedAt = DateTime.UtcNow,
                        VerifiedByMethod = CollectionMethod.FaceMatch,
                        MatchConfidence = pending.Confidence,
                        CollectedByUserId = staffUserId > 0 ? (int?)staffUserId : null
                    };

                    // Outside the normal hours: note who opened collection
                    var opening = CollectionOpening.Current(now);
                    if (opening != null && MealTimes.CurrentSlot(now) != pending.Slot)
                    {
                        collection.Notes = "Collected during manual opening by " + opening.OpenedBy;
                    }

                    _db.MealCollections.Add(collection);
                    _db.SaveChanges();
                    tx.Commit();

                    result.Outcome = CollectionOutcome.Collected;
                    result.Title = "Meal collected";
                    result.Message = string.Format("{0} collected {1} for {2}.", result.StudentName, result.MealName, SlotLabel(pending.Date, pending.Slot));
                    result.CollectedAt = SchoolClock.FromUtc(collection.CollectedAt).ToString("HH:mm");
                    return result;
                }
                catch (Exception)
                {
                    // Another terminal recorded it at the same moment
                    tx.Rollback();

                    var again = CheckEntitlement(pending.StudentId, pending.Date, pending.Slot);
                    again.IdentityVerified = true;
                    return again.Outcome == CollectionOutcome.AlreadyCollected ? again : Expired();
                }
            }
        }

        private static CollectionResult Expired()
        {
            return new CollectionResult
            {
                Outcome = CollectionOutcome.VerificationExpired,
                Title = "Verification expired",
                Message = "Please scan the student's face again."
            };
        }

        // ============================================================
        // ENTITLEMENT — may this student collect this meal now?
        // ============================================================

        private class EntitlementResult : CollectionResult
        {
            public MealPlanItem PlanItem { get; set; }
        }

        private EntitlementResult CheckEntitlement(int studentId, DateTime date, MealSlot slot)
        {
            var slotLabel = SlotLabel(date, slot);

            var student = _db.Students
                .AsNoTracking()
                .Include(s => s.StudentProfile)
                .FirstOrDefault(s => s.StudentId == studentId);

            var result = new EntitlementResult
            {
                StudentName = student != null ? student.FirstName + " " + student.LastName : "Unknown student",
                StudentNumber = student != null ? student.StudentNumber : null,
                MealSlot = slot.ToString(),
                MealDateLabel = date.ToString("dddd dd MMM")
            };

            // Kitchen info about the student's dietary needs
            var dietary = DietaryProfileService.FromStudentProfile(student != null ? student.StudentProfile : null);
            result.StudentAllergies = dietary.AllergySummary;
            result.DietaryPreference = dietary.PreferenceSummary;
            result.MedicalDietaryRestrictions = dietary.MedicalRestrictionSummary;
            result.DietaryNotes = dietary.Notes ?? "";

            // 1. A submitted meal plan covering today
            var plan = new MealPlanService(_db).FindSubmittedPlanFor(studentId, date);
            if (plan == null)
            {
                result.Outcome = CollectionOutcome.MealPlanNotFound;
                result.Title = "No submitted meal plan";
                result.Message = string.Format("{0} does not have a submitted meal plan for {1}.", result.StudentName, slotLabel);
                return result;
            }

            // 2. A meal chosen for this meal
            // AsNoTracking: Record must see the current row, not one cached at Verify
            var item = _db.MealPlanItems
                .AsNoTracking()
                .Include(i => i.MenuItem.Recipe.RecipeIngredients.Select(ri => ri.Ingredient))
                .FirstOrDefault(i => i.MealPlanId == plan.Id && i.Date == date && i.MealSlot == slot);

            if (item == null)
            {
                result.Outcome = CollectionOutcome.NoMealSelected;
                result.Title = "No meal selected";
                result.Message = string.Format("{0} has not selected a meal for {1}.", result.StudentName, slotLabel);
                return result;
            }

            result.PlanItem = item;
            result.MealName = item.MenuItem != null ? item.MenuItem.Name : "Unknown meal";

            // 3. Not already collected
            var existing = _db.MealCollections
                .Where(c => c.StudentId == studentId && c.Date == date && c.MealSlot == slot)
                .OrderBy(c => c.CollectedAt)
                .FirstOrDefault();

            if (existing != null)
            {
                result.Outcome = CollectionOutcome.AlreadyCollected;
                result.Title = "Meal already collected";
                result.CollectedAt = SchoolClock.FromUtc(existing.CollectedAt).ToString("HH:mm");
                result.Message = string.Format("Meal already collected for {0} (at {1}).", slotLabel, result.CollectedAt);
                return result;
            }

            // 4. The chosen meal is one the kitchen is providing: on the
            //    published menu for this meal, and still active
            var menu = _db.MealMenus
                .Where(m => m.MenuStatus == MenuStatus.Accepted && m.StartDate == plan.WeekStartDate)
                .OrderByDescending(m => m.LastModifiedDate)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();

            bool onMenu = menu != null && _db.MenuScheduleItems.Any(s =>
                s.MealMenuId == menu.Id && s.Date == date && s.MealSlot == slot && s.MenuItemId == item.MenuItemId);

            if (!onMenu || item.MenuItem == null || !item.MenuItem.IsActive)
            {
                result.Outcome = CollectionOutcome.InvalidMeal;
                result.Title = "Meal doesn't match the menu";
                result.Message = string.Format(
                    "{0}'s selected meal ({1}) is not on the published menu for {2}. Please check with the kitchen.",
                    result.StudentName, result.MealName, slotLabel);
                return result;
            }

            // Dietary check of the dish against the CURRENT profile —
            // shown to staff; the meal was checked when it was chosen
            var dish = item.MenuItem;
            result.MealAllergens = string.Join(", ", DietaryProfileService.GetAllergens(
                dish.Recipe != null
                    ? dish.Recipe.RecipeIngredients.Where(ri => ri.Ingredient != null).Select(ri => ri.Ingredient)
                    : Enumerable.Empty<Ingredient>()));

            result.DietaryWarnings = new DietaryProfileService(_db)
                .GetConflicts(dietary, dish)
                .Select(c => c.Message)
                .ToList();

            result.Outcome = CollectionOutcome.ReadyForCollection;
            result.Title = "Identity verified";
            result.Message = "Ready for collection.";
            return result;
        }

        private static string SlotLabel(DateTime date, MealSlot slot)
        {
            return date.ToString("dddd") + " " + slot;
        }
    }
}
