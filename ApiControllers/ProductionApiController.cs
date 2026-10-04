using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // UC15 Mobile — Kitchen Production Plan
    //
    // Endpoint: GET /api/production/current
    // Returns the production plan for the most recent accepted menu.
    // ============================================================
    [RoutePrefix("api/production")]
    public class ProductionApiController : Controller
    {
        private readonly DBContextClass _db;

        public ProductionApiController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        [Route("current")]
        public JsonResult Current()
        {
            try
            {
                var menu = _db.MealMenus
                    .Where(m => m.MenuStatus == MenuStatus.Accepted)
                    .OrderByDescending(m => m.LastModifiedDate)
                    .FirstOrDefault();

                if (menu == null)
                {
                    return Json(new
                    {
                        ok = false,
                        error = "No accepted menu is available yet."
                    }, JsonRequestBehavior.AllowGet);
                }

                var svc = new MenuSchedulingService(_db);
                var plan = svc.BuildProductionPlan(menu.Id);

                var dto = new
                {
                    ok = true,
                    menuId = plan.MenuId,
                    weekStart = plan.WeekStart.ToString("yyyy-MM-dd"),
                    weekEnd = plan.WeekEnd.ToString("yyyy-MM-dd"),
                    weekLabel = plan.WeekStart.ToString("dd MMM") + " - " + plan.WeekEnd.ToString("dd MMM yyyy"),
                    isProductionConfirmed = plan.IsProductionConfirmed,
                    totalMeals = plan.TotalMeals,
                    submittedPlanCount = plan.SubmittedPlanCount,
                    hasIngredientProblems = plan.HasIngredientProblems,
                    warnings = plan.Warnings.Select(w => new
                    {
                        when = w.When,
                        severity = w.Severity.ToString(),
                        message = w.Message,
                        affectedMeals = w.AffectedMeals
                    }).ToList(),
                    days = plan.Days.Select(d => new
                    {
                        date = d.Date.ToString("yyyy-MM-dd"),
                        dayLabel = d.Date.ToString("dddd, dd MMM"),
                        slots = d.Slots.Select(s => new
                        {
                            mealSlot = s.MealSlot,
                            serveTime = s.ServeTime.ToString(@"hh\:mm"),
                            portions = s.Portions,
                            tasks = s.Tasks.Select(t => new
                            {
                                dishName = t.DishName,
                                station = t.Station,
                                portions = t.Portions,
                                prepMinutes = t.PrepMinutes,
                                cookMinutes = t.CookMinutes,
                                startTime = t.StartTime.ToString(@"hh\:mm"),
                                readyTime = t.ReadyTime.ToString(@"hh\:mm")
                            }).ToList()
                        }).ToList()
                    }).ToList(),
                    weekIngredients = plan.WeekTotalIngredients.Select(i => new
                    {
                        ingredientId = i.IngredientId,
                        name = i.IngredientName,
                        unit = i.Unit,
                        totalRequired = i.TotalRequired,
                        stockAvailable = i.StockAvailable,
                        shortfall = i.Shortfall,
                        isCovered = i.IsCovered,
                        status = i.Status.ToString()
                    }).ToList()
                };

                return Json(dto, JsonRequestBehavior.AllowGet);
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