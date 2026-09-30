using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // UC13 API — Student Meal Plan
    //
    // The mobile app calls this to fetch a student's weekly
    // meal plan (with ranked options per slot).
    //
    // Endpoint: GET /api/mealplan/student/{studentId}
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
        // GET: /api/mealplan/student/1
        // ============================================================

        [HttpGet]
        [Route("student/{studentId:int}")]
        public JsonResult GetStudentPlan(int studentId)
        {
            try
            {
                var svc = new MealPlanService(_db);
                var vm = svc.GetOrCreateDraft(studentId);

                // ── Map the view model into a DTO the mobile app expects ──
                var dto = new
                {
                    ok = true,
                    mealPlanId = vm.MealPlanId,
                    studentName = vm.StudentName,
                    weekStart = vm.WeekStartDate.ToString("yyyy-MM-dd"),
                    weekEnd = vm.WeekEndDate.ToString("yyyy-MM-dd"),
                    status = vm.Status.ToString(),
                    isEditable = vm.IsEditable,
                    allergies = vm.Allergies,
                    medicalConditions = vm.MedicalConditions,
                    sports = vm.Sports,
                    days = vm.Days.Select(d => new
                    {
                        date = d.Date.ToString("yyyy-MM-dd"),
                        dayLabel = d.Date.ToString("dddd"),
                        slots = d.Slots.Select(s => new
                        {
                            mealSlot = s.MealSlot.ToString(),
                            isMatchDay = s.IsMatchDay,
                            isDayBeforeMatch = s.IsDayBeforeMatch,
                            matchDescription = s.MatchDescription,
                            defaultItemName = s.DefaultItemName,
                            currentPickMenuItemId = s.CurrentPickMenuItemId,
                            currentPickMealPlanItemId = s.CurrentPickMealPlanItemId,
                            options = s.Options.Select(o => new
                            {
                                menuItemId = o.MenuItemId,
                                name = o.Name,
                                dietaryClassification = o.DietaryClassification,
                                calories = o.CaloriesPerPortion,
                                protein = o.ProteinGramsPerPortion,
                                nutritionCategory = o.NutritionCategory.ToString(),
                                isDefault = o.IsDefault,
                                tags = o.Tags
                            }).ToList()
                        }).ToList()
                    }).ToList()
                };

                return Json(dto, JsonRequestBehavior.AllowGet);
            }
            catch (InvalidOperationException ex)
            {
                // Known service-level error (e.g. no published menu)
                return Json(new
                {
                    ok = false,
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    error = "Server error: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}