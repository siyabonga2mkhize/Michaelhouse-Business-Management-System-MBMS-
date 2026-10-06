using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "CafeteriaManager, Chef, Admin")]
    public class CafeteriaController : Controller
    {
        private readonly DBContextClass _db;
        private readonly MenuSchedulingService _menuService;

        public CafeteriaController()
        {
            _db = new DBContextClass();
            _menuService = new MenuSchedulingService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }

            base.Dispose(disposing);
        }

        // ============================================================
        // GET: Cafeteria/Dashboard
        // ============================================================

        [HttpGet]
        public ActionResult Dashboard()
        {
            var pendingMenus = _db.MealMenus
                .Where(x => x.MenuStatus == MenuStatus.PendingReview)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            var acceptedMenus = _db.MealMenus
                .Where(x => x.MenuStatus == MenuStatus.Accepted)
                .OrderByDescending(x => x.LastModifiedDate)
                .Take(5)
                .ToList();

            ViewBag.PendingMenus = pendingMenus;
            ViewBag.AcceptedMenus = acceptedMenus;

            return View();
        }

        // ============================================================
        // GET: Cafeteria/Schedule
        // ============================================================

        [HttpGet]
        public ActionResult Schedule(DateTime? startDate, DateTime? endDate)
        {
            // Dates can be passed in so the Coaches' fixtures shown match
            // the range being planned
            var start = (startDate ?? SchoolClock.Today).Date;
            var end = endDate.HasValue && endDate.Value.Date >= start ? endDate.Value.Date : start.AddDays(6);

            var model = new ScheduleMenuInputViewModel
            {
                StartDate = start,
                EndDate = end,
                StaffMeals = 0
            };

            PopulateBoardingHouses(model);

            return View(model);
        }

        // ============================================================
        // POST: Cafeteria/Generate
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Generate(ScheduleMenuInputViewModel input)
        {
            PopulateBoardingHousesFromPostedValues(input);

            if (!ModelState.IsValid)
            {
                return View("Schedule", input);
            }

            try
            {
                MealMenu menu = _menuService.GenerateMenu(input);

                TempData["SuccessMessage"] =
                    "The menu was generated successfully and is ready for review.";

                return RedirectToAction("Review", new { id = menu.Id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            return View("Schedule", input);
        }

        // ============================================================
        // GET: Cafeteria/Review/5
        // ============================================================

        [HttpGet]
        public ActionResult Review(int id)
        {
            MealMenu menu = _menuService.GetMenu(id);

            if (menu == null)
            {
                return HttpNotFound();
            }

            ProposedMenuViewModel model = BuildProposedMenuViewModel(menu);

            ViewBag.MenuItems = _db.MenuItems
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();

            // Do the options in each slot give every dietary group of
            // students a suitable choice? Recalculated on every view so
            // it reflects manual changes.
            ViewBag.Coverage = new MenuCoverageService(_db).AnalyseMenu(id);

            return View(model);
        }

        // ============================================================
        // GET: Cafeteria/KitchenRequirements/5
        // Ingredient requirements for the week — calculated only from
        // students' submitted meal plans (+ staff meals), the same
        // figures as the Kitchen Plan. Nothing is shown before
        // students have chosen.
        // ============================================================

        [HttpGet]
        public ActionResult KitchenRequirements(int id)
        {
            if (!_db.MealMenus.Any(m => m.Id == id))
            {
                return HttpNotFound();
            }

            return View(_menuService.BuildProductionPlan(id));
        }

        // ============================================================
        // POST: Cafeteria/Accept/5
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Accept(int id)
        {
            try
            {
                // The Chef no longer signs off before acceptance: the menu
                // is a set of options for students to choose from, and
                // the Chef / stock / production steps follow once students'
                // meal plans give actual demand.
                var menu = _db.MealMenus.FirstOrDefault(x => x.Id == id);

                if (menu == null)
                {
                    return HttpNotFound();
                }

                _menuService.AcceptMenu(id);

                TempData["SuccessMessage"] = "The menu has been accepted.";

                return RedirectToAction("Review", new { id = id });
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return RedirectToAction("Review", new { id = id });
            }
        }

        // ============================================================
        // POST: Cafeteria/Reject/5
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["ErrorMessage"] = "A rejection reason is required.";

                    return RedirectToAction("Review", new { id = id });
                }

                /*
                 * Preserve the original rejected menu.
                 * Then immediately generate a replacement menu.
                 */
                MealMenu rejected = _menuService.RejectMenu(id, reason);

                MealMenu regenerated = _menuService.RegenerateRejectedMenu(rejected.Id, reason);

                TempData["SuccessMessage"] =
                    "The menu was rejected and a regenerated proposal has been created.";

                return RedirectToAction("Review", new { id = regenerated.Id });
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return RedirectToAction("Review", new { id = id });
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return RedirectToAction("Review", new { id = id });
            }
        }

        // ============================================================
        // POST: Cafeteria/ModifyItem
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ModifyItem(int scheduleItemId, int newMenuItemId, int newPortions)
        {
            try
            {
                MenuScheduleItem item = _menuService.ModifyItem(scheduleItemId, newMenuItemId, newPortions);

                TempData["SuccessMessage"] =
                    "The option was changed. Dietary coverage has been rechecked.";

                return RedirectToAction("Review", new { id = item.MealMenuId });
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction("Schedule");
        }

        // ============================================================
        // GET: Cafeteria/ChefIndex
        // Chef landing page — lists menus awaiting kitchen review
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult ChefIndex()
        {
            var pendingMenus = _db.MealMenus
                .Where(x => x.MenuStatus == MenuStatus.PendingReview)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            var acceptedMenus = _db.MealMenus
                .Where(x => x.MenuStatus == MenuStatus.Accepted)
                .OrderByDescending(x => x.LastModifiedDate)
                .Take(5)
                .ToList();

            ViewBag.PendingMenus = pendingMenus;
            ViewBag.AcceptedMenus = acceptedMenus;

            return View();
        }
        // ============================================================
        // GET: Cafeteria/ChefReview/5
        // Chef's view of a proposed menu: flagged items + BOM tab
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult ChefReview(int id)
        {
            MealMenu menu = _menuService.GetMenu(id);

            if (menu == null)
            {
                return HttpNotFound();
            }

            var model = BuildProposedMenuViewModel(menu);

            // Load substitute options for every flagged item
            var flaggedIds = menu.ScheduleItems
                .Where(x => x.ItemTagStatus == ItemTagStatus.NeedsSubstitution)
                .Select(x => x.Id)
                .ToList();

            var substitutesByItem = new Dictionary<int, List<SubstituteOption>>();

            foreach (var itemId in flaggedIds)
            {
                try
                {
                    substitutesByItem[itemId] =
                        _menuService.GetValidSubstitutes(itemId);
                }
                catch
                {
                    substitutesByItem[itemId] = new List<SubstituteOption>();
                }
            }

            ViewBag.SubstitutesByItem = substitutesByItem;

            // Ingredient requirements are only calculated from students'
            // submitted meal plans (Kitchen Plan / Kitchen Requirements),
            // never from a proposed menu.

            return View(model);
        }
        // ============================================================
        // GET: Cafeteria/ProductionPlan/5
        // Chef + kitchen staff view: what to cook, when, where.
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Chef, CafeteriaManager, Admin")]
        public ActionResult ProductionPlan(int id)
        {
            var menu = _db.MealMenus.FirstOrDefault(m => m.Id == id);

            if (menu == null)
            {
                return HttpNotFound();
            }

            try
            {
                var plan = _menuService.BuildProductionPlan(id);
                return View(plan);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Review", new { id = id });
            }
        }

        // ============================================================
        // POST: Cafeteria/ConfirmProduction/5
        // Chef confirms the production sheet — human sign-off.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult ConfirmProduction(int id)
        {
            var menu = _db.MealMenus.FirstOrDefault(m => m.Id == id);

            if (menu == null)
            {
                return HttpNotFound();
            }

            menu.IsProductionConfirmed = true;
            menu.ProductionConfirmedAt = DateTime.UtcNow;
            menu.ProductionConfirmedByUserId = (int)(Session["UserId"] ?? 0);

            _db.SaveChanges();

            // Don't let a plan with missing ingredients look fully ready
            int problems = _menuService.BuildProductionPlan(id).Warnings
                .Count(w => w.Severity != KitchenWarningSeverity.Info);

            TempData["SuccessMessage"] = problems == 0
                ? "Kitchen plan confirmed. All ingredients for the chosen meals are available."
                : string.Format("Kitchen plan confirmed, but {0} ingredient problem(s) are still open.", problems);

            return RedirectToAction("ProductionPlan", new { id = id });
        }


        // ============================================================
        // GET: Cafeteria/IssueIngredients/5?date=2026-10-07
        // The day's ingredients from the kitchen plan, against stock.
        // POST: the Chef confirms what was used — stock goes down.
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Chef, CafeteriaManager, Admin")]
        public ActionResult IssueIngredients(int id, DateTime date)
        {
            try
            {
                var vm = new KitchenIssueService(_db).ForMenuDay(id, date);
                ViewBag.PostUrl = Url.Action("IssueIngredients", "Cafeteria", new { id, date = date.ToString("yyyy-MM-dd") });
                ViewBag.BackUrl = Url.Action("ProductionPlan", "Cafeteria", new { id });
                ViewBag.BackLabel = "Back to Kitchen Plan";
                ViewBag.CanSubmit = User.IsInRole("Chef") || User.IsInRole("Admin");
                return View("~/Views/Shared/KitchenIssue.cshtml", vm);
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("ProductionPlan", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult IssueIngredients(int id, DateTime date, string notes)
        {
            var service = new KitchenIssueService(_db);

            try
            {
                var vm = service.ForMenuDay(id, date);
                var quantities = KitchenIssueService.ReadQuantities(k => Request.Form[k], vm);
                service.IssueMenuDay(id, date, quantities, notes, (int)(Session["UserId"] ?? 0));

                TempData["Success"] = "Ingredients for " + date.ToString("dddd dd MMM") + " issued — stock updated.";
                return RedirectToAction("ProductionPlan", new { id });
            }
            catch (InventoryException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("IssueIngredients", new { id, date = date.ToString("yyyy-MM-dd") });
            }
        }

        // ============================================================
        // POST: Cafeteria/ConfirmSubstitution
        // Chef swaps one flagged item for a validated alternative
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult ConfirmSubstitution(int scheduleItemId, int newMenuItemId)
        {
            try
            {
                MenuScheduleItem item = _menuService.ModifyItem(
                    scheduleItemId,
                    newMenuItemId,
                    _db.MenuScheduleItems
                        .Where(x => x.Id == scheduleItemId)
                        .Select(x => x.CalculatedPortions)
                        .FirstOrDefault());

                TempData["SuccessMessage"] =
                    "Substitution saved. Ingredient requirements recalculated.";

                return RedirectToAction("ChefReview", new { id = item.MealMenuId });
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            var fallbackId = _db.MenuScheduleItems
                .Where(x => x.Id == scheduleItemId)
                .Select(x => x.MealMenuId)
                .FirstOrDefault();

            return RedirectToAction("ChefReview", new { id = fallbackId });
        }

        // ============================================================
        // POST: Cafeteria/MarkKitchenReady/5
        // Chef signs off — unlocks Accept for the coordinator
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult MarkKitchenReady(int id)
        {
            MealMenu menu = _menuService.GetMenu(id);

            if (menu == null)
            {
                return HttpNotFound();
            }

            int unresolved = menu.ScheduleItems.Count(x =>
                x.ItemTagStatus != ItemTagStatus.Confirmed);

            if (unresolved > 0)
            {
                TempData["ErrorMessage"] = string.Format(
                    "{0} item(s) still need substitution before the kitchen can sign off.",
                    unresolved);

                return RedirectToAction("ChefReview", new { id = id });
            }

            menu.IsKitchenReady = true;
            menu.KitchenReadyDate = DateTime.UtcNow;

            _db.SaveChanges();

            TempData["SuccessMessage"] =
                "Kitchen Ready. The Meal Coordinator can now accept the menu.";

            return RedirectToAction("ChefReview", new { id = id });
        }
        // ============================================================
        // POST: Cafeteria/RequestChanges
        // Chef sends the menu back to the Coordinator with a reason.
        // This is a soft push-back, not a rejection — the menu stays
        // PendingReview so the Coordinator can respond.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Admin")]
        public ActionResult RequestChanges(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "Please explain what needs to change.";
                return RedirectToAction("ChefReview", new { id = id });
            }

            MealMenu menu = _db.MealMenus.FirstOrDefault(x => x.Id == id);

            if (menu == null)
            {
                return HttpNotFound();
            }

            menu.ChefChallengeRequested = true;
            menu.ChefChallengeReason = reason.Trim();
            menu.ChefChallengeDate = DateTime.UtcNow;

            _db.SaveChanges();

            TempData["SuccessMessage"] =
                "Change request sent. The Meal Coordinator will review.";

            return RedirectToAction("ChefReview", new { id = id });
        }


        // ============================================================
        // VIEWMODEL BUILDERS
        // ============================================================

        private ProposedMenuViewModel BuildProposedMenuViewModel(MealMenu menu)
        {
            var model = new ProposedMenuViewModel
            {
                Id = menu.Id,
                StartDate = menu.StartDate,
                EndDate = menu.EndDate,
                StaffMeals = menu.StaffMeals,
                SpecialEventNotes = menu.SpecialEventNotes,
                MenuStatus = menu.MenuStatus,
                IsKitchenReady = menu.IsKitchenReady,
                KitchenReadyDate = menu.KitchenReadyDate,
                ChefChallengeRequested = menu.ChefChallengeRequested,
                ChefChallengeReason = menu.ChefChallengeReason,
                ChefChallengeDate = menu.ChefChallengeDate,
                RejectionReason = menu.RejectionReason
            };


            model.ScheduleItems = menu.ScheduleItems
                .OrderBy(x => x.Date)
                .ThenBy(x => x.MealSlot)
                .Select(BuildScheduleItemDisplay)
                .ToList();

            model.ConfirmedCount = model.ScheduleItems.Count(x => x.ItemTagStatus == ItemTagStatus.Confirmed);

            model.NeedsSubstitutionCount = model.ScheduleItems.Count(x => x.ItemTagStatus == ItemTagStatus.NeedsSubstitution);

            model.NeedsReviewCount = model.ScheduleItems.Count(x => x.ItemTagStatus == ItemTagStatus.NeedsReview);

            return model;
        }

        private MenuScheduleItemDisplay BuildScheduleItemDisplay(MenuScheduleItem item)
        {
            string statusText;
            string cssClass;

            switch (item.ItemTagStatus)
            {
                case ItemTagStatus.Confirmed:
                    statusText = "Confirmed";
                    cssClass = "bg-success";
                    break;

                case ItemTagStatus.NeedsSubstitution:
                    statusText = "Needs Substitution";
                    cssClass = "bg-warning text-dark";
                    break;

                default:
                    statusText = "Needs Review";
                    cssClass = "bg-danger";
                    break;
            }

            return new MenuScheduleItemDisplay
            {
                Id = item.Id,
                MenuItemId = item.MenuItemId,
                NutritionCategory = item.MenuItem != null ? item.MenuItem.NutritionCategory : NutritionCategory.Standard,
                Date = item.Date,
                MealSlot = item.MealSlot.ToString(),
                MenuItemName = item.MenuItem != null ? item.MenuItem.Name : "Unknown item",
                DietaryClassification = item.MenuItem != null ? item.MenuItem.DietaryClassification : string.Empty,
                CalculatedPortions = item.CalculatedPortions,
                ItemTagStatus = item.ItemTagStatus,
                StatusText = statusText,
                StatusCssClass = cssClass,
                TagReason = item.TagReason,
                IsFarmGrownProduce = item.MenuItem != null && item.MenuItem.IsFarmGrownProduce,
                CaloriesPerPortion = item.MenuItem != null ? item.MenuItem.CaloriesPerPortion : 0,
                ProteinGramsPerPortion = item.MenuItem != null ? item.MenuItem.ProteinGramsPerPortion : 0,
                SubstitutionMenuItemName = item.SubstitutionMenuItem != null ? item.SubstitutionMenuItem.Name : null
            };
        }

        // ============================================================
        // BOARDING HOUSES AND SPORT FIXTURES (Schedule form)
        //
        // Houses show how many students live in them (residence
        // allocation). Sport comes only from the Coaches' fixtures —
        // shown read-only here; the generator works out who is
        // affected (StudentSportService).
        // ============================================================

        private void PopulateBoardingHouses(ScheduleMenuInputViewModel model)
        {
            var counts = HouseStudentCounts();

            model.BoardingHouses = _db.Residences
                .Where(x => !x.IsArchived)
                .OrderBy(x => x.Name)
                .ToList()
                .Select(house => new BoardingHouseScheduleOption
                {
                    Id = house.ResidenceId,
                    Name = house.Name,
                    StudentCount = counts.ContainsKey(house.ResidenceId) ? counts[house.ResidenceId] : 0
                })
                .ToList();

            PopulateScheduledSports(model);
        }

        private void PopulateBoardingHousesFromPostedValues(ScheduleMenuInputViewModel input)
        {
            if (input.BoardingHouses == null)
            {
                input.BoardingHouses = new List<BoardingHouseScheduleOption>();
            }

            var ids = input.BoardingHouses
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var houses = _db.Residences
                .Where(x => ids.Contains(x.ResidenceId))
                .ToDictionary(x => x.ResidenceId);

            var counts = HouseStudentCounts();

            foreach (var posted in input.BoardingHouses)
            {
                if (!houses.ContainsKey(posted.Id))
                {
                    posted.StudentCount = 0;
                    posted.Name = "Unknown boarding house";
                    continue;
                }

                posted.Name = houses[posted.Id].Name;
                posted.StudentCount = counts.ContainsKey(posted.Id) ? counts[posted.Id] : 0;
            }

            PopulateScheduledSports(input);
        }

        // ResidenceId → active students living there
        private Dictionary<int, int> HouseStudentCounts()
        {
            var activeIds = new HashSet<int>(_db.Students.Where(s => s.IsActive).Select(s => s.StudentId));

            return new StudentHouseService(_db).HouseByStudent()
                .Where(kv => activeIds.Contains(kv.Key))
                .GroupBy(kv => kv.Value)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // The Coaches' fixtures in the planning range, with the players
        // each affects and what it means for meals
        private void PopulateScheduledSports(ScheduleMenuInputViewModel model)
        {
            var start = model.StartDate.Date;
            var end = model.EndDate.Date < start ? start : model.EndDate.Date;

            var calendar = new StudentSportService(_db).LoadCalendar(start, end);
            var houseNames = _db.Residences.ToDictionary(r => r.ResidenceId, r => r.Name);

            var rows = new List<ScheduledSportViewModel>();
            int peak = 0;

            // The day after the range too: a match then makes the last
            // day a pre-match day
            for (var date = start; date <= end.AddDays(1); date = date.AddDays(1))
            {
                var events = calendar.EventsOn(date);
                var matchPlayers = new HashSet<int>();

                foreach (var e in events)
                {
                    var players = calendar.PlayersFor(e);
                    var archetype = SportCatalogue.ArchetypeOf(e.Sport);
                    bool isMatch = StudentSportService.IsMatch(e);

                    if (isMatch) matchPlayers.UnionWith(players);

                    string need;
                    if (isMatch)
                    {
                        need = Capitalise(MenuCoverageService.CategoryWords(MenuSchedulingService.MatchDayCategory(archetype)))
                            + " on match day, high-carb the day before";
                    }
                    else
                    {
                        var category = MenuSchedulingService.TrainingCategory(archetype);
                        need = category.HasValue
                            ? Capitalise(MenuCoverageService.CategoryWords(category.Value)) + " on training day"
                            : "No particular meal need";
                    }

                    rows.Add(new ScheduledSportViewModel
                    {
                        Date = e.ScheduledDate,
                        StartTime = e.StartTime,
                        EndTime = e.StartTime.Add(TimeSpan.FromMinutes(e.DurationMinutes)),
                        EventType = e.EventType,
                        Label = StudentSportService.Label(e),
                        House = e.ResidenceId.HasValue && houseNames.ContainsKey(e.ResidenceId.Value) ? houseNames[e.ResidenceId.Value] : null,
                        Players = players.Count,
                        MealNeed = need
                    });
                }

                if (date <= end) peak = Math.Max(peak, matchPlayers.Count);
            }

            model.ScheduledSports = rows;
            model.PeakMatchPlayers = peak;
        }

        private static string Capitalise(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}