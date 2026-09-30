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

                // Reuse the same builder used by the web page
                var svc = new MenuSchedulingService(_db);
                var vm = BuildFeastPlanFromEvent(evt);

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

        // ── Local builder (mirrors EventController.BuildFeastPlan) ──
        private Michaelhouse.Models.ViewModels.FeastPlanViewModel BuildFeastPlanFromEvent(CafeteriaEvent evt)
        {
            var vm = new Michaelhouse.Models.ViewModels.FeastPlanViewModel
            {
                EventId = evt.Id,
                EventName = evt.EventName,
                EventDate = evt.EventDate,
                StartTime = evt.StartTime,
                EndTime = evt.EndTime,
                VenueName = evt.Venue != null ? evt.Venue.Name : "",
                MenuTemplateName = evt.MenuTemplate != null ? evt.MenuTemplate.Name : "",
                TotalGuests = evt.GuaranteedHeadcount ?? 0
            };

            var attending = _db.EventRsvps
                .Where(r => r.EventId == evt.Id && r.ResponseStatus == "Attending")
                .ToList();

            vm.StandardGuests = attending.Sum(r => r.StandardCount);
            vm.VegetarianGuests = attending.Sum(r => r.VegetarianCount);
            vm.OtherGuests = attending.Sum(r => r.OtherDietCount);

            vm.DietaryNotes = attending
                .Where(r => !string.IsNullOrWhiteSpace(r.DietaryNotes))
                .Select(r => r.ResponderName + ": " + r.DietaryNotes)
                .ToList();

            var ingredientStock = _db.Ingredients
                .Where(i => i.IsActive)
                .ToDictionary(
                    i => i.Id,
                    i => i.FarmAvailableQuantity + i.ExternalAvailableQuantity);

            var serveTime = evt.StartTime;

            if (evt.MenuTemplateId.HasValue)
            {
                var templateItems = _db.EventMenuTemplateItems
                    .Include("MenuItem.Recipe.RecipeIngredients.Ingredient")
                    .Where(t => t.TemplateId == evt.MenuTemplateId.Value)
                    .OrderBy(t => t.SortOrder)
                    .ToList();

                var combinedTotals = new System.Collections.Generic.Dictionary<int, Michaelhouse.Models.ViewModels.FeastPlanIngredient>();

                foreach (var item in templateItems)
                {
                    if (item.MenuItem == null) continue;

                    int portions = (int)Math.Ceiling(vm.TotalGuests * item.QuantityPerGuest);
                    if (portions < 1) portions = 1;

                    var recipe = item.MenuItem.Recipe;

                    var dish = new Michaelhouse.Models.ViewModels.FeastPlanDish
                    {
                        Section = item.Section,
                        DishName = item.MenuItem.Name,
                        Classification = item.MenuItem.DietaryClassification,
                        Portions = portions,
                        QuantityPerGuest = item.QuantityPerGuest,
                        Station = recipe != null && !string.IsNullOrWhiteSpace(recipe.Station)
                                    ? recipe.Station
                                    : "Line",
                        PrepMinutes = recipe != null ? recipe.PrepTimeMinutes : 0,
                        CookMinutes = recipe != null ? recipe.CookTimeMinutes : 0
                    };

                    int totalMinutes = dish.PrepMinutes + dish.CookMinutes;

                    if (totalMinutes >= 240)
                        dish.PrepPhase = Michaelhouse.Models.ViewModels.FeastPrepPhase.TwoDaysBefore;
                    else if (totalMinutes >= 90)
                        dish.PrepPhase = Michaelhouse.Models.ViewModels.FeastPrepPhase.DayBefore;
                    else
                        dish.PrepPhase = Michaelhouse.Models.ViewModels.FeastPrepPhase.DayOf;

                    var cookSpan = TimeSpan.FromMinutes(dish.CookMinutes);
                    var startSpan = serveTime.Subtract(cookSpan);
                    dish.StartTime = startSpan < TimeSpan.Zero ? startSpan.Add(TimeSpan.FromHours(24)) : startSpan;
                    dish.ReadyTime = serveTime.Subtract(TimeSpan.FromMinutes(5));

                    if (recipe != null && recipe.RecipeIngredients != null)
                    {
                        foreach (var ri in recipe.RecipeIngredients)
                        {
                            if (ri.Ingredient == null) continue;

                            decimal required = ri.QuantityPerStandardPortion * portions;
                            decimal available = ingredientStock.ContainsKey(ri.IngredientId)
                                ? ingredientStock[ri.IngredientId]
                                : 0m;

                            var line = new Michaelhouse.Models.ViewModels.FeastPlanIngredient
                            {
                                IngredientId = ri.IngredientId,
                                Name = ri.Ingredient.Name,
                                Unit = ri.Ingredient.Unit,
                                QuantityPerPortion = ri.QuantityPerStandardPortion,
                                TotalRequired = required,
                                StockAvailable = available,
                                Shortfall = required > available ? required - available : 0m
                            };

                            dish.Ingredients.Add(line);

                            if (combinedTotals.ContainsKey(line.IngredientId))
                            {
                                var existing = combinedTotals[line.IngredientId];
                                existing.TotalRequired += line.TotalRequired;
                                existing.Shortfall = existing.TotalRequired > existing.StockAvailable
                                    ? existing.TotalRequired - existing.StockAvailable
                                    : 0m;
                            }
                            else
                            {
                                combinedTotals[line.IngredientId] = new Michaelhouse.Models.ViewModels.FeastPlanIngredient
                                {
                                    IngredientId = line.IngredientId,
                                    Name = line.Name,
                                    Unit = line.Unit,
                                    QuantityPerPortion = line.QuantityPerPortion,
                                    TotalRequired = line.TotalRequired,
                                    StockAvailable = line.StockAvailable,
                                    Shortfall = line.Shortfall
                                };
                            }
                        }
                    }

                    vm.Dishes.Add(dish);
                }

                vm.TotalIngredients = combinedTotals.Values
                    .OrderBy(x => x.Name)
                    .ToList();
            }

            // Timeline
            var eventDate = evt.EventDate.Date;

            var d2 = new Michaelhouse.Models.ViewModels.FeastPlanTimelineDay
            {
                Phase = Michaelhouse.Models.ViewModels.FeastPrepPhase.TwoDaysBefore,
                CalendarDate = eventDate.AddDays(-2),
                DayLabel = "Two days before",
                Dishes = vm.Dishes.Where(d => d.PrepPhase == Michaelhouse.Models.ViewModels.FeastPrepPhase.TwoDaysBefore).ToList()
            };
            var d1 = new Michaelhouse.Models.ViewModels.FeastPlanTimelineDay
            {
                Phase = Michaelhouse.Models.ViewModels.FeastPrepPhase.DayBefore,
                CalendarDate = eventDate.AddDays(-1),
                DayLabel = "Day before",
                Dishes = vm.Dishes.Where(d => d.PrepPhase == Michaelhouse.Models.ViewModels.FeastPrepPhase.DayBefore).ToList()
            };
            var d0 = new Michaelhouse.Models.ViewModels.FeastPlanTimelineDay
            {
                Phase = Michaelhouse.Models.ViewModels.FeastPrepPhase.DayOf,
                CalendarDate = eventDate,
                DayLabel = "Day of event",
                Dishes = vm.Dishes.Where(d => d.PrepPhase == Michaelhouse.Models.ViewModels.FeastPrepPhase.DayOf).ToList()
            };

            if (d2.Dishes.Count > 0) vm.Timeline.Add(d2);
            if (d1.Dishes.Count > 0) vm.Timeline.Add(d1);
            if (d0.Dishes.Count > 0) vm.Timeline.Add(d0);

            return vm;
        }
    }
}