using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // UC13 API — Student Meal Plan
    //
    // The mobile app calls this for the logged-in student's weekly
    // meal plan (with ranked options per slot).
    //
    // Returns JSON — the same data that the web page uses, but
    // serialised instead of rendered.
    // ============================================================
    [RoutePrefix("api/mealplan")]
    public class MealPlanApiController : Controller
    {
        private readonly DBContextClass _db;

        public MealPlanApiController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // LOGGED-IN STUDENT — same rules as the web page
        // (StudentMealPlanController). The student is taken from the
        // login session, never from the request.
        //
        //   GET  /api/mealplan/mine          → this week's plan
        //   POST /api/mealplan/mine/pick     { mealPlanId, date, mealSlot, menuItemId }
        //        → MealPlanService.SavePicks (deadline, menu and
        //          dietary checks), like the web's Save
        //   POST /api/mealplan/mine/submit   { mealPlanId }
        //        → MealPlanService.Submit, like the web's Submit
        // ============================================================

        [HttpGet]
        [Route("mine")]
        [Authorize(Roles = "Student")]
        public JsonResult Mine()
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0)
            {
                return Json(new { ok = false, error = "No student profile linked to this account." },
                    JsonRequestBehavior.AllowGet);
            }

            try
            {
                var vm = new MealPlanService(_db).GetOrCreateDraft(studentId);
                return Json(ToMobileJson(vm), JsonRequestBehavior.AllowGet);
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [Route("mine/pick")]
        [Authorize(Roles = "Student")]
        public JsonResult Pick()
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return Json(new { ok = false, error = "No student profile linked to this account." });

            var data = ReadBody<PickRequestModel>();
            if (data == null || string.IsNullOrWhiteSpace(data.Date) || string.IsNullOrWhiteSpace(data.MealSlot))
            {
                return Json(new { ok = false, error = "Please choose a meal." });
            }

            try
            {
                // Same key format the web form posts: "yyyy-MM-dd|Lunch"
                var picks = new Dictionary<string, int>
                {
                    { data.Date + "|" + data.MealSlot, data.MenuItemId }
                };

                new MealPlanService(_db).SavePicks(data.MealPlanId, studentId, picks);
                return Json(new { ok = true, message = "Your pick has been saved." });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        [HttpPost]
        [Route("mine/submit")]
        [Authorize(Roles = "Student")]
        public JsonResult SubmitMine()
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return Json(new { ok = false, error = "No student profile linked to this account." });

            var data = ReadBody<SubmitRequestModel>();
            if (data == null) return Json(new { ok = false, error = "Meal plan not found." });

            try
            {
                new MealPlanService(_db).Submit(data.MealPlanId, studentId);
                return Json(new
                {
                    ok = true,
                    message = "Your meal plan has been submitted. Your choices go straight to the kitchen."
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // Everything the web page shows, including options the student
        // can't choose (with the reason)
        private static object ToMobileJson(MealPlanBuildViewModel vm)
        {
            return new
            {
                ok = true,
                mealPlanId = vm.MealPlanId,
                studentName = vm.StudentName,
                weekStart = vm.WeekStartDate.ToString("yyyy-MM-dd"),
                weekEnd = vm.WeekEndDate.ToString("yyyy-MM-dd"),
                weekLabel = vm.WeekStartDate.ToString("dd MMM") + " – " + vm.WeekEndDate.ToString("dd MMM yyyy"),
                status = vm.Status.ToString(),
                statusText = StatusText(vm.Status),
                isSubmitted = MealPlanService.IsSubmitted(vm.Status),
                submittedAt = vm.SubmittedAt.HasValue
                    ? SchoolClock.FromUtc(vm.SubmittedAt.Value).ToString("dd MMM yyyy HH:mm")
                    : null,
                dietitianComment = vm.DietitianComment,
                isEditable = vm.IsEditable,
                canSubmit = vm.CanSubmit,
                openSlotCount = vm.OpenSlotCount,
                openSlotsChosen = vm.OpenSlotsChosen,
                lockedSlotCount = vm.Days.Sum(d => d.Slots.Count(s => s.IsLocked)),
                allergies = vm.Allergies,
                dietaryPreference = vm.DietaryPreference,
                medicalDietaryRestrictions = vm.MedicalDietaryRestrictions,
                medicalConditions = vm.MedicalConditions,
                sports = vm.Sports,
                days = vm.Days.Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    dayLabel = d.Date.ToString("dddd"),
                    dateLabel = d.Date.ToString("dd MMMM"),
                    shortLabel = d.Date.ToString("ddd dd MMM"),
                    slots = d.Slots.Select(s => new
                    {
                        mealSlot = s.MealSlot.ToString(),
                        isLocked = s.IsLocked,
                        // Web: "Choose by end of ddd dd MMM"
                        chooseByLabel = s.SelectionCutoff.AddDays(-1).ToString("ddd dd MMM"),
                        currentPickMenuItemId = s.CurrentPickMenuItemId,
                        currentPickName = s.CurrentPickName,
                        currentPickNoLongerSuitable = s.CurrentPickNoLongerSuitable,
                        hasNoSuitableOption = s.HasNoSuitableOption,
                        sportsNote = s.SportsNote,
                        isMatchDay = s.IsMatchDay,
                        isDayBeforeMatch = s.IsDayBeforeMatch,
                        isTrainingDay = s.IsTrainingDay,
                        matchDescription = s.MatchDescription,
                        options = s.Options.Select(o => new
                        {
                            menuItemId = o.MenuItemId,
                            name = o.Name,
                            isAvailable = o.IsAvailable,
                            unavailableReason = o.UnavailableReason,
                            dietaryClassification = o.DietaryClassification,
                            calories = o.CaloriesPerPortion,
                            protein = o.ProteinGramsPerPortion,
                            carbohydrate = o.CarbohydrateGramsPerPortion,
                            fat = o.FatGramsPerPortion,
                            isDefault = o.IsDefault,
                            tags = o.Tags,
                            recommendation = o.RecommendationLabel
                        }).ToList()
                    }).ToList()
                }).ToList()
            };
        }

        // Same wording as Views/StudentMealPlan/Index.cshtml
        private static string StatusText(MealPlanStatus status)
        {
            switch (status)
            {
                case MealPlanStatus.Draft: return "Draft";
                case MealPlanStatus.Submitted:
                case MealPlanStatus.SubmittedToDietitian: return "Submitted";
                case MealPlanStatus.Approved: return "Approved";
                case MealPlanStatus.SentBack: return "Sent Back — Action Needed";
                default: return status.ToString();
            }
        }

        // Same lookup as StudentMealPlanController.ResolveStudentId
        private int ResolveStudentId()
        {
            int id;
            if (Session["StudentId"] != null && int.TryParse(Session["StudentId"].ToString(), out id))
            {
                return id;
            }

            int userId;
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out userId))
            {
                var student = _db.Students.FirstOrDefault(s => s.UserId == userId);
                if (student != null)
                {
                    Session["StudentId"] = student.StudentId;
                    return student.StudentId;
                }
            }

            return 0;
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
    }

    public class PickRequestModel
    {
        public int MealPlanId { get; set; }
        public string Date { get; set; }       // yyyy-MM-dd
        public string MealSlot { get; set; }   // Breakfast / Lunch / Dinner
        public int MenuItemId { get; set; }
    }

    public class SubmitRequestModel
    {
        public int MealPlanId { get; set; }
    }
}