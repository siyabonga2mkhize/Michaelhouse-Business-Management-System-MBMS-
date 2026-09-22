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
    [Authorize(Roles = "CafeteriaManager, Admin")]
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
        // GET: Cafeteria/Schedule
        // ============================================================

        [HttpGet]
        public ActionResult Schedule()
        {
            var model = new ScheduleMenuInputViewModel
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(6),
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

            return View(model);
        }

        // ============================================================
        // GET: Cafeteria/KitchenRequirements/5
        // ============================================================

        [HttpGet]
        public ActionResult KitchenRequirements(int id)
        {
            MealMenu menu = _menuService.GetMenu(id);

            if (menu == null)
            {
                return HttpNotFound();
            }

            List<MenuSchedulingService.IngredientRequirement> requirements;

            try
            {
                requirements = _menuService.CalculateIngredientRequirements(id);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;

                return RedirectToAction("Review", new { id = id });
            }

            ViewBag.MealMenuId = menu.Id;
            ViewBag.StartDate = menu.StartDate;
            ViewBag.EndDate = menu.EndDate;
            ViewBag.MenuStatus = menu.MenuStatus;

            return View(requirements);
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
                    "The schedule item was modified and its status recalculated.";

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
        // BOARDING HOUSE / RESIDENCE DATA
        // ============================================================

        private void PopulateBoardingHouses(ScheduleMenuInputViewModel model)
        {
            model.BoardingHouses = _db.Residences
                .OrderBy(x => x.Name)
                .ToList()
                .Select(x => new BoardingHouseScheduleOption
                {
                    Id = x.ResidenceId,
                    Name = x.Name,
                    StudentCount = x.Capacity,
                    IsInSeason = false,
                    ActiveSport = ""
                })
                .ToList();
        }

        private void PopulateBoardingHousesFromPostedValues(ScheduleMenuInputViewModel input)
        {
            if (input.BoardingHouses == null)
            {
                input.BoardingHouses = new List<BoardingHouseScheduleOption>();
                return;
            }

            var ids = input.BoardingHouses
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var houses = _db.Residences
                .Where(x => ids.Contains(x.ResidenceId))
                .ToDictionary(x => x.ResidenceId);

            foreach (var posted in input.BoardingHouses)
            {
                if (!houses.ContainsKey(posted.Id))
                {
                    posted.StudentCount = 0;
                    posted.Name = "Unknown boarding house";
                    continue;
                }

                var house = houses[posted.Id];

                posted.Name = house.Name;
                posted.StudentCount = house.Capacity;
            }
        }
    }
}