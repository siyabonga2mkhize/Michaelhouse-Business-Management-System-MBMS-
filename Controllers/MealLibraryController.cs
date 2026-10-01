using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // Dietitian Meal Library — view, create, edit, deactivate meals.
    //
    // Class allows the Chef and Cafeteria Manager through so they can
    // view meals. Create / edit / (de)activate re-restrict to the
    // Dietitian individually below.
    // ============================================================

    [Authorize(Roles = "Dietitian, CafeteriaManager, Chef, Admin")]
    public class MealLibraryController : Controller
    {
        private readonly DBContextClass _db;
        private readonly MealLibraryService _service;

        public MealLibraryController()
        {
            _db = new DBContextClass();
            _service = new MealLibraryService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: MealLibrary
        // ============================================================

        [HttpGet]
        public ActionResult Index(bool showInactive = false)
        {
            ViewBag.ShowInactive = showInactive;
            ViewBag.CanEdit = CanEdit();
            return View(_service.GetMeals(showInactive));
        }

        // ============================================================
        // GET: MealLibrary/Details/5
        // ============================================================

        [HttpGet]
        public ActionResult Details(int id)
        {
            var vm = _service.GetDetails(id);
            if (vm == null) return HttpNotFound();

            ViewBag.CanEdit = CanEdit();
            return View(vm);
        }

        // ============================================================
        // GET / POST: MealLibrary/Create  (Dietitian only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Dietitian, Admin")]
        public ActionResult Create()
        {
            var vm = _service.NewForm();
            PopulateForm(vm);
            return View("Form", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Dietitian, Admin")]
        public ActionResult Create(MealFormViewModel model)
        {
            model.Id = null;

            if (!AddErrors(_service.Validate(model)))
            {
                PopulateForm(model);
                return View("Form", model);
            }

            try
            {
                int id = _service.Create(model);
                SetSavedMessage(id, "Meal created.");
                return RedirectToAction("Details", new { id = id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                PopulateForm(model);
                return View("Form", model);
            }
        }

        // ============================================================
        // GET / POST: MealLibrary/Edit/5  (Dietitian only)
        // ============================================================

        [HttpGet]
        [Authorize(Roles = "Dietitian, Admin")]
        public ActionResult Edit(int id)
        {
            var vm = _service.GetForm(id);
            if (vm == null) return HttpNotFound();

            PopulateForm(vm);
            return View("Form", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Dietitian, Admin")]
        public ActionResult Edit(MealFormViewModel model)
        {
            if (!model.Id.HasValue) return HttpNotFound();

            if (!AddErrors(_service.Validate(model)))
            {
                PopulateForm(model);
                return View("Form", model);
            }

            try
            {
                _service.Update(model);
                SetSavedMessage(model.Id.Value, "Meal updated.");
                return RedirectToAction("Details", new { id = model.Id.Value });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                PopulateForm(model);
                return View("Form", model);
            }
        }

        // ============================================================
        // POST: MealLibrary/Deactivate/5 and Reactivate/5
        // Meals are never deleted — they may be referenced by
        // historical menus and meal plans.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Dietitian, Admin")]
        public ActionResult Deactivate(int id)
        {
            return SetActive(id, false, "Meal deactivated. It will no longer be offered in new menus or meal plans.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Dietitian, Admin")]
        public ActionResult Reactivate(int id)
        {
            return SetActive(id, true, "Meal reactivated.");
        }

        // ============================================================
        // POST: MealLibrary/CreateIngredient  (JSON, Dietitian only)
        // Called from the Create Meal page without leaving it.
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Dietitian, Admin")]
        public JsonResult CreateIngredient(NewIngredientViewModel model)
        {
            try
            {
                var errors = new List<string>();
                var ingredient = _service.CreateIngredient(model, errors);

                if (ingredient == null)
                {
                    return Json(new { success = false, message = string.Join(" ", errors) });
                }

                return Json(new
                {
                    success = true,
                    message = "Ingredient added.",
                    ingredient = MealLibraryService.ToOption(ingredient)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private ActionResult SetActive(int id, bool active, string message)
        {
            try
            {
                _service.SetActive(id, active);
                TempData["Success"] = message;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = id });
        }

        private void SetSavedMessage(int id, string message)
        {
            TempData["Success"] = message;

            var details = _service.GetDetails(id);
            if (details != null && !string.IsNullOrEmpty(details.NutritionCategoryWarning))
            {
                TempData["Warning"] = details.NutritionCategoryWarning;
            }
        }

        private bool AddErrors(IEnumerable<string> errors)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error);
            }

            return ModelState.IsValid;
        }

        private void PopulateForm(MealFormViewModel vm)
        {
            var usedIds = (vm.Ingredients ?? new List<MealIngredientLineViewModel>())
                .Select(i => i.IngredientId);

            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeHtml
            };

            ViewBag.IngredientOptionsJson = JsonConvert.SerializeObject(_service.GetIngredientOptions(usedIds), settings);
            ViewBag.Stations = _service.GetStationSuggestions();
        }

        private bool CanEdit()
        {
            return User != null && (User.IsInRole("Dietitian") || User.IsInRole("Admin"));
        }
    }
}
