using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // Class allows Chef through so they can reach Queue + FeastPlan.
    // Coordinator-only actions re-restrict themselves individually below.
    [Authorize(Roles = "CafeteriaManager, Admin, Chef")]
    public class EventController : Controller
    {
        private readonly DBContextClass _db;

        // All non-parent, non-student roles that count as "staff"
        private static readonly string[] StaffRoles = new[]
        {
            "Admin", "Teacher", "HouseMaster", "Housemaster",
            "CafeteriaManager", "Chef", "Dietitian", "Coach",
            "InventoryManager", "TransportManager",
            "MaintenanceManager", "MaintenanceWorker", "Driver"
        };

        public EventController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: Event/Index  (Coordinator only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Index()
        {
            var today = DateTime.Today;

            ViewBag.Upcoming = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .Where(e => e.EventDate >= today && e.Status != EventStatus.Cancelled)
                .OrderBy(e => e.EventDate)
                .ToList();

            ViewBag.Past = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .Where(e => e.EventDate < today || e.Status == EventStatus.Cancelled)
                .OrderByDescending(e => e.EventDate)
                .Take(20)
                .ToList();

            return View();
        }

        // ============================================================
        // GET: Event/Create  (Coordinator only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Create()
        {
            PopulateDropdowns();
            return View(new CafeteriaEvent
            {
                EventDate = DateTime.Today.AddDays(7),
                StartTime = new TimeSpan(12, 30, 0),
                EndTime = new TimeSpan(15, 0, 0),
                ExpectedHeadcount = 50,
                Status = EventStatus.Draft,
                InviteParents = true,
                InviteStaff = false,
                InviteStudents = false
            });
        }

        // ============================================================
        // POST: Event/Create  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Create(CafeteriaEvent model)
        {
            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("EndTime", "End time must be after start time.");
            }

            if (model.ExpectedHeadcount <= 0)
            {
                ModelState.AddModelError("ExpectedHeadcount", "Expected headcount must be greater than zero.");
            }

            var selectedHouses = Request.Form.GetValues("InviteHouses");
            if (selectedHouses != null && selectedHouses.Length > 0)
            {
                model.InviteParentResidenceIds = string.Join(",", selectedHouses);
            }
            else
            {
                model.InviteParentResidenceIds = null;
            }

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return View(model);
            }

            bool clash = _db.CafeteriaEvents.Any(e =>
                e.VenueId == model.VenueId
                && e.EventDate == model.EventDate.Date
                && e.Status != EventStatus.Cancelled
                && e.StartTime < model.EndTime
                && e.EndTime > model.StartTime);

            if (clash)
            {
                var clashEvent = _db.CafeteriaEvents
                    .Include("Venue")
                    .FirstOrDefault(e =>
                        e.VenueId == model.VenueId
                        && e.EventDate == model.EventDate.Date
                        && e.Status != EventStatus.Cancelled
                        && e.StartTime < model.EndTime
                        && e.EndTime > model.StartTime);

                ModelState.AddModelError("",
                    string.Format("Venue conflict — {0} is already booked for \"{1}\" from {2:hh\\:mm} to {3:hh\\:mm}.",
                        clashEvent.Venue.Name,
                        clashEvent.EventName,
                        clashEvent.StartTime,
                        clashEvent.EndTime));

                PopulateDropdowns();
                return View(model);
            }

            model.EventDate = model.EventDate.Date;
            model.Status = EventStatus.Confirmed;
            model.CreatedByUserId = ResolveUserId();
            model.CreatedAt = DateTime.UtcNow;

            _db.CafeteriaEvents.Add(model);
            _db.SaveChanges();

            TempData["Success"] = "Event \"" + model.EventName + "\" scheduled for "
                + model.EventDate.ToString("dd MMM yyyy") + ".";
            return RedirectToAction("Index");
        }

        // ============================================================
        // GET: Event/Details/5  (Coordinator only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Details(int id)
        {
            var evt = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate.Items.MenuItem")
                .Include("StaffAssignments")
                .Include("Rsvps")
                .FirstOrDefault(e => e.Id == id);

            if (evt == null) return HttpNotFound();

            return View(evt);
        }

        // ============================================================
        // POST: Event/Cancel/5  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult Cancel(int id, string reason)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            evt.Status = EventStatus.Cancelled;
            evt.InternalNotes = (evt.InternalNotes ?? "")
                + "\n[CANCELLED " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")
                + "] " + (reason ?? "No reason given");
            evt.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            TempData["Success"] = "Event cancelled.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // POST: Event/OpenRsvp/5  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult OpenRsvp(int id, DateTime rsvpDeadline)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            if (evt.Status != EventStatus.Confirmed)
            {
                TempData["Error"] = "Only confirmed events can open RSVPs.";
                return RedirectToAction("Details", new { id = id });
            }

            if (rsvpDeadline.Date < DateTime.Today)
            {
                TempData["Error"] = "RSVP deadline cannot be in the past.";
                return RedirectToAction("Details", new { id = id });
            }

            evt.Status = EventStatus.RsvpOpen;
            evt.RsvpDeadline = rsvpDeadline.Date;
            evt.RsvpOpenedAt = DateTime.UtcNow;
            evt.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            int notified = NotifyAudience(evt);

            TempData["Success"] = string.Format(
                "RSVPs are now open. {0} users notified. Share the link with anyone else.",
                notified);

            return RedirectToAction("Details", new { id = id });
        }

        // ============================================================
        // POST: Event/CloseRsvp/5  (Coordinator only)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CafeteriaManager, Admin")]
        public ActionResult CloseRsvp(int id)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == id);
            if (evt == null) return HttpNotFound();

            if (evt.Status != EventStatus.RsvpOpen)
            {
                TempData["Error"] = "Only events with open RSVPs can be closed.";
                return RedirectToAction("Details", new { id = id });
            }

            var total = _db.EventRsvps
                .Where(r => r.EventId == id && r.ResponseStatus == "Attending")
                .Select(r => (int?)r.TotalAttendees)
                .Sum() ?? 0;

            evt.GuaranteedHeadcount = total;
            evt.Status = EventStatus.RsvpClosed;
            evt.RsvpClosedAt = DateTime.UtcNow;
            evt.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();

            TempData["Success"] = string.Format(
                "RSVPs closed. {0} guests confirmed.", total);

            return RedirectToAction("Details", new { id = id });
        }

        // ============================================================
        // GET: Event/FeastPlan/5  (Coordinator + Chef)
        // ============================================================

        [HttpGet]
        public ActionResult FeastPlan(int id)
        {
            var evt = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .FirstOrDefault(e => e.Id == id);

            if (evt == null) return HttpNotFound();

            if (evt.Status != EventStatus.RsvpClosed
                && evt.Status != EventStatus.FeastPlanGenerated)
            {
                TempData["Error"] = "Close RSVPs first, then generate the feast plan.";
                return RedirectToAction("Details", new { id = id });
            }

            var vm = BuildFeastPlan(evt);

            if (evt.Status == EventStatus.RsvpClosed)
            {
                evt.Status = EventStatus.FeastPlanGenerated;
                evt.UpdatedAt = DateTime.UtcNow;
                _db.SaveChanges();

                NotifyChefsFeastPlan(evt);
            }

            return View(vm);
        }

        // ============================================================
        // GET: Event/Queue  (Coordinator + Chef)
        // ============================================================

        [HttpGet]
        public ActionResult Queue()
        {
            var today = DateTime.Today;

            var events = _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .Where(e => e.EventDate >= today)
                .Where(e => e.Status == EventStatus.FeastPlanGenerated
                         || e.Status == EventStatus.InProgress)
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.StartTime)
                .ToList();

            return View(events);
        }

        // ============================================================
        // FEAST PLAN BUILDER
        // ============================================================

        private Michaelhouse.Models.ViewModels.FeastPlanViewModel BuildFeastPlan(CafeteriaEvent evt)
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

                var combinedTotals = new Dictionary<int, Michaelhouse.Models.ViewModels.FeastPlanIngredient>();

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
                    {
                        dish.PrepPhase = Michaelhouse.Models.ViewModels.FeastPrepPhase.TwoDaysBefore;
                    }
                    else if (totalMinutes >= 90)
                    {
                        dish.PrepPhase = Michaelhouse.Models.ViewModels.FeastPrepPhase.DayBefore;
                    }
                    else
                    {
                        dish.PrepPhase = Michaelhouse.Models.ViewModels.FeastPrepPhase.DayOf;
                    }

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

        // ============================================================
        // NOTIFY AUDIENCE (RSVP invitations)
        // ============================================================

        private int NotifyAudience(CafeteriaEvent evt)
        {
            var recipients = new HashSet<int>();

            if (evt.InviteParents)
            {
                var parentQuery = _db.Parents.Where(p => p.UserId != null);

                if (!string.IsNullOrWhiteSpace(evt.InviteParentResidenceIds))
                {
                    var houseIds = evt.InviteParentResidenceIds
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s =>
                        {
                            int i;
                            return int.TryParse(s.Trim(), out i) ? (int?)i : null;
                        })
                        .Where(i => i.HasValue)
                        .Select(i => i.Value)
                        .ToList();

                    if (houseIds.Count > 0)
                    {
                        var parentIdsInHouses = _db.Students
                            .Where(s => s.ResidenceId.HasValue && houseIds.Contains(s.ResidenceId.Value))
                            .Select(s => s.ParentId)
                            .Distinct()
                            .ToList();

                        parentQuery = parentQuery.Where(p => parentIdsInHouses.Contains(p.ParentId));
                    }
                }

                foreach (var uid in parentQuery.Select(p => p.UserId.Value).ToList())
                {
                    recipients.Add(uid);
                }
            }

            if (evt.InviteStudents)
            {
                foreach (var uid in _db.Students
                                        .Where(s => s.UserId != null)
                                        .Select(s => s.UserId.Value)
                                        .ToList())
                {
                    recipients.Add(uid);
                }
            }

            if (evt.InviteStaff)
            {
                foreach (var uid in _db.Users
                                        .Where(u => StaffRoles.Contains(u.Role))
                                        .Select(u => u.UserId)
                                        .ToList())
                {
                    recipients.Add(uid);
                }
            }

            if (recipients.Count == 0) return 0;

            var scheme = Request != null ? Request.Url.Scheme : "https";
            var link = Url.Action("Event", "Rsvp", new { id = evt.Id }, scheme);

            var deadlineText = evt.RsvpDeadline.HasValue
                ? evt.RsvpDeadline.Value.ToString("ddd dd MMM")
                : "the deadline";

            var message = string.Format(
                "You're invited to {0} on {1}. RSVP by {2} — {3}",
                evt.EventName,
                evt.EventDate.ToString("ddd dd MMM"),
                deadlineText,
                link);

            var now = DateTime.Now;
            foreach (var uid in recipients)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Message = message,
                    RelatedEntityType = "CafeteriaEvent",
                    RelatedEntityId = evt.Id,
                    IsRead = false,
                    CreatedAt = now
                });
            }

            _db.SaveChanges();
            return recipients.Count;
        }

        // ============================================================
        // NOTIFY KITCHEN — feast plan ready
        // ============================================================

        private void NotifyChefsFeastPlan(CafeteriaEvent evt)
        {
            var chefUserIds = _db.Users
                .Where(u => u.Role == "Chef")
                .Select(u => u.UserId)
                .ToList();

            if (chefUserIds.Count == 0) return;

            var scheme = Request != null ? Request.Url.Scheme : "https";
            var link = Url.Action("FeastPlan", "Event", new { id = evt.Id }, scheme);

            var message = string.Format(
                "Feast plan ready — {0} on {1}. {2} guests. Open: {3}",
                evt.EventName,
                evt.EventDate.ToString("ddd dd MMM"),
                evt.GuaranteedHeadcount ?? 0,
                link);

            var now = DateTime.Now;
            foreach (var uid in chefUserIds)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Message = message,
                    RelatedEntityType = "FeastPlan",
                    RelatedEntityId = evt.Id,
                    IsRead = false,
                    CreatedAt = now
                });
            }
            _db.SaveChanges();
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void PopulateDropdowns()
        {
            ViewBag.Venues = _db.EventVenues
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .ToList();

            ViewBag.Templates = _db.EventMenuTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .ToList();

            ViewBag.Houses = _db.Residences
                .OrderBy(r => r.Name)
                .ToList();

            ViewBag.EventTypes = Enum.GetValues(typeof(EventType))
                .Cast<EventType>()
                .Select(e => new SelectListItem
                {
                    Value = ((int)e).ToString(),
                    Text = System.Text.RegularExpressions.Regex.Replace(e.ToString(), "([a-z])([A-Z])", "$1 $2")
                })
                .ToList();
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