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
    // UC19 Mobile — Feast Plan
    //
    // Endpoint: GET /api/feastplan/current
    // Returns the feast plan for the most recent event that has one.
    // ============================================================
    [RoutePrefix("api/feastplan")]
    public class FeastPlanApiController : Controller
    {
        private readonly DBContextClass _db;

        public FeastPlanApiController()
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
                // Most recently generated feast plan
                var evt = _db.CafeteriaEvents
                    .Include("Venue")
                    .Include("MenuTemplate")
                    .Where(e => e.Status == EventStatus.FeastPlanGenerated
                             || e.Status == EventStatus.RsvpClosed)
                    .OrderByDescending(e => e.RsvpClosedAt)
                    .ThenByDescending(e => e.Id)
                    .FirstOrDefault();

                if (evt == null)
                {
                    return Json(new
                    {
                        ok = false,
                        error = "No event has a feast plan generated yet."
                    }, JsonRequestBehavior.AllowGet);
                }

                // Same builder as the web page — from the RSVPs
                var vm = new EventBuffetService(_db).BuildFeastPlan(evt);
                var req = vm.Requirements;

                var dto = new
                {
                    ok = true,
                    eventId = vm.EventId,
                    eventName = vm.EventName,
                    eventDate = vm.EventDate.ToString("yyyy-MM-dd"),
                    eventDateLabel = vm.EventDate.ToString("dddd, dd MMMM yyyy"),
                    startTime = vm.StartTime.ToString(@"hh\:mm"),
                    endTime = vm.EndTime.ToString(@"hh\:mm"),
                    venueName = vm.VenueName,
                    menuTemplateName = vm.MenuTemplateName,
                    totalGuests = vm.TotalGuests,
                    standardGuests = vm.StandardGuests,
                    vegetarianGuests = vm.VegetarianGuests,
                    otherGuests = vm.OtherGuests,
                    shortageCount = vm.ShortageCount,
                    dietaryNotes = vm.DietaryNotes,
                    attendance = new
                    {
                        students = req.Attendance.Students,
                        parents = req.Attendance.Parents,
                        staff = req.Attendance.Staff,
                        guests = req.Attendance.Guests,
                        total = req.Attendance.Total
                    },
                    dietaryCounts = req.PreferenceCounts.Select(kv => new { group = kv.Key, attendees = kv.Value }),
                    restrictionCounts = req.RestrictionCounts.Select(kv => new { requirement = kv.Key, attendees = kv.Value }),
                    meals = req.Meals.Select(m => new { name = m.MenuItem.Name, chosen = m.Selected, byType = m.TypeSummary, allocated = m.Allocated, portions = m.Portions }),
                    buffetMeals = vm.Coverage.Meals.Select(m => new { name = m.Name, suitableFor = m.SuitableFor, allergens = m.Allergens }),
                    dietaryGroups = vm.Coverage.Groups.Select(g => new { label = g.Label, students = g.StudentCount, covered = g.IsCovered, meals = g.SuitableMeals }),
                    dietaryWarnings = vm.Coverage.Warnings,
                    timeline = vm.Timeline.Select(t => new
                    {
                        phase = t.Phase.ToString(),
                        dayLabel = t.DayLabel,
                        calendarDate = t.CalendarDate.ToString("yyyy-MM-dd"),
                        dishes = t.Dishes.Select(d => new
                        {
                            dishName = d.DishName,
                            station = d.Station,
                            portions = d.Portions,
                            prepMinutes = d.PrepMinutes,
                            cookMinutes = d.CookMinutes
                        }).ToList()
                    }).ToList(),
                    dishes = vm.Dishes.Select(d => new
                    {
                        section = d.Section,
                        dishName = d.DishName,
                        classification = d.Classification,
                        portions = d.Portions,
                        station = d.Station,
                        startTime = d.StartTime.ToString(@"hh\:mm"),
                        readyTime = d.ReadyTime.ToString(@"hh\:mm"),
                        prepMinutes = d.PrepMinutes,
                        cookMinutes = d.CookMinutes,
                        ingredients = d.Ingredients.Select(i => new
                        {
                            name = i.Name,
                            unit = i.Unit,
                            quantityPerPortion = i.QuantityPerPortion,
                            totalRequired = i.TotalRequired,
                            stockAvailable = i.StockAvailable,
                            shortfall = i.Shortfall,
                            isCovered = i.IsCovered
                        }).ToList()
                    }).ToList(),
                    totalIngredients = vm.TotalIngredients.Select(i => new
                    {
                        name = i.Name,
                        unit = i.Unit,
                        totalRequired = i.TotalRequired,
                        stockAvailable = i.StockAvailable,
                        shortfall = i.Shortfall,
                        isCovered = i.IsCovered
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