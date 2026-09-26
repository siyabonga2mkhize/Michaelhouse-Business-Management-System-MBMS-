using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Services;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class CafeteriaController : BaseController
    {
        private DBContextClass db = new DBContextClass();

        // ─── Services ─────────────────────────────────────────────────
        private CafeteriaSettingsService CafeSettings { get { return new CafeteriaSettingsService(db); } }
        private DietarySafetyService Safety { get { return new DietarySafetyService(db); } }
        private MenuValidationService Validator { get { return new MenuValidationService(db); } }
        private CafeteriaAuditService Audit { get { return new CafeteriaAuditService(db); } }

        // ─── Session helpers ──────────────────────────────────────────
        private string CurrentRole
        {
            get { return Session["UserRole"] != null ? Session["UserRole"].ToString() : ""; }
        }
        private string CurrentUserName
        {
            get { return Session["UserName"] != null ? Session["UserName"].ToString() : "System"; }
        }
        private int CurrentStudentId
        {
            get
            {
                int id;
                if (Session["StudentId"] != null && int.TryParse(Session["StudentId"].ToString(), out id))
                    return id;
                return 0;
            }
        }
        private int CurrentUserId
        {
            get
            {
                int id;
                if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out id))
                    return id;
                return 0;
            }
        }
        private int CurrentCoachId
        {
            get
            {
                int id;
                if (Session["CoachId"] != null && int.TryParse(Session["CoachId"].ToString(), out id))
                    return id;
                return 0;
            }
        }

        private bool HasServingWindowEnded(string mealType)
        {
            var now = DateTime.Now; var today = now.Date;
            switch (mealType)
            {
                case "Breakfast": return now >= today.AddHours(7).AddMinutes(30);
                case "Lunch": return now >= today.AddHours(13).AddMinutes(30);
                case "Dinner": return now >= today.AddHours(18).AddMinutes(30);
                default: return false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // DASHBOARD (all cafeteria staff)
        // ══════════════════════════════════════════════════════════════
        public ActionResult Index()
        {
            ViewBag.WeekNumber = "W" + (DateTime.Now.DayOfYear / 7 + 1);
            ViewBag.TotalStudents = db.Students.Count(s => s.IsActive);
            ViewBag.ApprovedPlans = db.MealPlans.Count(m => m.Status == "Approved");
            ViewBag.PendingPlans = db.MealPlans.Count(m => m.Status == "Pending");

            var today = DateTime.Now.Date;
            ViewBag.TodaySelections = db.Selections.Count(s => s.Date == today);
            ViewBag.TodayServed = db.Selections.Count(s => s.Date == today && s.IsCollected);

            ViewBag.LowStockCount = db.InventoryItems
                .Where(i => i.IsActive && i.Category == "Food").ToList()
                .Count(i => i.CurrentStock <= i.ReorderLevel);

            // Pending dietary change requests (Dietitian sees this)
            ViewBag.PendingDietaryRequests = db.DietaryChangeRequests
                .Count(r => r.Status == DietaryRequestStatus.Pending);

            // Pending coach requirements (MealCoordinator sees this)
            ViewBag.PendingCoachRequirements = db.CoachRequirements
                .Count(r => r.Status == CoachRequirementStatus.Submitted);

            // Latest published / draft menu
            ViewBag.CurrentMenu = db.WeeklyMenus
                .OrderByDescending(w => w.StartDate).FirstOrDefault();

            ViewBag.UpcomingEvents = db.Events
                .Where(e => e.Date >= today && e.Status != "Cancelled")
                .OrderBy(e => e.Date).Take(5).ToList();

            ViewBag.Role = CurrentRole;
            ViewBag.UserName = CurrentUserName;
            return View();
        }

        // ══════════════════════════════════════════════════════════════
        // DIETITIAN — Meal Plan Review
        // ══════════════════════════════════════════════════════════════
        public ActionResult MealPlans()
        {
            var allPlans = db.MealPlans
                .Include("Items")
                .OrderByDescending(m => m.GeneratedAt).ToList();

            var studentIds = allPlans.Select(m => m.StudentId).Distinct().ToList();
            var students = db.Students.Where(s => studentIds.Contains(s.StudentId)).ToList();
            var profiles = db.StudentProfiles.Where(p => studentIds.Contains(p.StudentId)).ToList();

            var rows = new List<MealPlanReviewRow>();
            foreach (var plan in allPlans)
            {
                rows.Add(new MealPlanReviewRow
                {
                    Plan = plan,
                    Student = students.FirstOrDefault(s => s.StudentId == plan.StudentId),
                    Profile = profiles.FirstOrDefault(p => p.StudentId == plan.StudentId),
                    Flags = GetMealPlanFlags(plan)
                });
            }

            ViewBag.Role = CurrentRole;
            ViewBag.TotalCount = rows.Count;
            ViewBag.PendingCount = rows.Count(r => r.Plan.Status == "Pending");
            ViewBag.FlaggedCount = rows.Count(r => r.Plan.Status == "Pending" && r.Flags.Any());
            ViewBag.SafeCount = rows.Count(r => r.Plan.Status == "Pending" && !r.Flags.Any());
            ViewBag.ApprovedCount = rows.Count(r => r.Plan.Status == "Approved");
            ViewBag.RejectedCount = rows.Count(r => r.Plan.Status == "Rejected");
            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BulkApproveSafe()
        {
            var pending = db.MealPlans.Include("Items")
                .Where(m => m.Status == "Pending").ToList();

            int approved = 0;
            foreach (var plan in pending)
            {
                if (!GetMealPlanFlags(plan).Any())
                {
                    plan.Status = "Approved";
                    plan.ConfirmedAt = DateTime.Now;
                    approved++;
                    Audit.Log("MealPlanApproved", "MealPlan", plan.Id, "Pending", "Approved");
                }
            }
            db.SaveChanges();
            TempData["Success"] = approved + " safe meal plans auto-approved.";
            return RedirectToAction("MealPlans");
        }

        public ActionResult ViewMealPlan(int id)
        {
            var mealPlan = db.MealPlans.Include("Items").FirstOrDefault(m => m.Id == id);
            if (mealPlan == null) return HttpNotFound();

            ViewBag.Student = db.Students.FirstOrDefault(s => s.StudentId == mealPlan.StudentId);
            ViewBag.StudentProfile = db.StudentProfiles.FirstOrDefault(p => p.StudentId == mealPlan.StudentId);
            ViewBag.Role = CurrentRole;
            return View(mealPlan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveMealPlan(int id)
        {
            var plan = db.MealPlans.Find(id);
            if (plan == null) return HttpNotFound();

            plan.Status = "Approved";
            plan.ConfirmedAt = DateTime.Now;
            db.SaveChanges();
            Audit.Log("MealPlanApproved", "MealPlan", id, "Pending", "Approved");

            TempData["Success"] = "Meal plan approved.";
            return RedirectToAction("MealPlans");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectMealPlan(int id, string reason)
        {
            var plan = db.MealPlans.Find(id);
            if (plan == null) return HttpNotFound();

            plan.Status = "Rejected";
            db.SaveChanges();
            Audit.Log("MealPlanRejected", "MealPlan", id, "Pending", "Rejected", reason);

            TempData["Success"] = "Meal plan rejected.";
            return RedirectToAction("MealPlans");
        }

        private List<string> GetMealPlanFlags(MealPlan plan)
        {
            var flags = new List<string>();
            if (plan == null) return flags;
            if (plan.Items == null || plan.Items.Count == 0)
                flags.Add("Empty meal plan — no items selected");
            return flags;
        }

        // ══════════════════════════════════════════════════════════════
        // STUDENT — My Meal Plan
        // ══════════════════════════════════════════════════════════════
        public ActionResult MyMealPlan()
        {
            int studentId = CurrentStudentId;
            if (studentId == 0)
            {
                TempData["Error"] = "Only students can access this page.";
                return RedirectToAction("Index", "Home");
            }

            var student = db.Students.FirstOrDefault(s => s.StudentId == studentId);
            if (student == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Now.Date;
            var publishedMenu = db.WeeklyMenus
                .Include("Days.Meals.Components.MenuItem")
                .Include("Days.Meals.Alternatives.MenuItem")
                .Include("Days.Meals.MealSlot")
                .OrderByDescending(w => w.StartDate)
                .FirstOrDefault(w => w.Status == "Published" && w.EndDate >= today);

            ViewBag.Student = student;
            ViewBag.PublishedMenu = publishedMenu;
            ViewBag.HasPublishedMenu = publishedMenu != null;

            if (publishedMenu == null)
            {
                ViewBag.Days = new List<StudentMealPlanDay>();
                ViewBag.PlanStatus = "Pending";
                return View();
            }

            var teamIds = db.TeamMemberships
                .Where(m => m.StudentId == studentId && m.EndDate == null)
                .Select(m => m.TeamId)
                .ToList();

            var activities = db.SportsActivities
                .Where(a => teamIds.Contains(a.TeamId)
                         && a.StartDateTime >= publishedMenu.StartDate
                         && a.StartDateTime <= publishedMenu.EndDate.AddDays(1)
                         && !a.IsCancelled)
                .ToList();

            var existingPlan = db.MealPlans
                .Include("Items")
                .FirstOrDefault(m => m.StudentId == studentId && m.WeeklyMenuId == publishedMenu.WeeklyMenuID);

            var selectedByMeal = new Dictionary<int, int>();
            if (existingPlan != null)
            {
                foreach (var i in existingPlan.Items)
                {
                    if (i.SelectedMenuItemId.HasValue)
                        selectedByMeal[i.MenuMealId] = i.SelectedMenuItemId.Value;
                }
            }

            var days = new List<StudentMealPlanDay>();
            foreach (var day in publishedMenu.Days.OrderBy(d => d.Date))
            {
                var dayVM = new StudentMealPlanDay
                {
                    Date = day.Date,
                    DayOfWeek = day.Date.ToString("dddd")
                };

                foreach (var meal in day.Meals.OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99))
                {
                    var mealVM = new StudentMealPlanMeal
                    {
                        MenuMealId = meal.Id,
                        SlotName = meal.MealSlot != null ? meal.MealSlot.Name : "",
                        Title = meal.Title
                    };

                    var candidates = new List<MenuItem>();
                    if (meal.Components != null)
                        candidates.AddRange(meal.Components.Where(c => c.MenuItem != null).Select(c => c.MenuItem));
                    if (meal.Alternatives != null)
                        candidates.AddRange(meal.Alternatives.Where(a => a.MenuItem != null).Select(a => a.MenuItem));

                    candidates = candidates.GroupBy(c => c.Id).Select(g => g.First()).ToList();

                    foreach (var c in candidates)
                    {
                        var check = Safety.CheckMenuItem(studentId, c.Id);
                        if (!check.IsSafe) continue;

                        mealVM.Options.Add(new StudentMealPlanItemOption
                        {
                            MenuItemId = c.Id,
                            Name = c.Name,
                            Category = c.Category,
                            IsVegetarian = c.IsVegetarian,
                            IsVegan = c.IsVegan,
                            IsGlutenFree = c.IsGlutenFree,
                            IsLactoseFree = c.IsLactoseFree
                        });
                    }

                    if (mealVM.Options.Count > 0)
                    {
                        var activity = activities.FirstOrDefault(a => a.StartDateTime.Date == day.Date.Date);

                        if (activity != null)
                        {
                            var protein = mealVM.Options.FirstOrDefault(o =>
                                !string.IsNullOrEmpty(o.Category) &&
                                o.Category.IndexOf("Protein", StringComparison.OrdinalIgnoreCase) >= 0);
                            if (protein != null)
                            {
                                mealVM.RecommendedItemId = protein.MenuItemId;
                                mealVM.RecommendationReason = "Your team has " + activity.ActivityType +
                                    " at " + activity.StartDateTime.ToString("HH:mm") + " — protein recommended";
                            }
                        }

                        if (!mealVM.RecommendedItemId.HasValue)
                        {
                            mealVM.RecommendedItemId = mealVM.Options[0].MenuItemId;
                            mealVM.RecommendationReason = "Standard option for this meal";
                        }

                        if (selectedByMeal.ContainsKey(meal.Id))
                        {
                            var savedId = selectedByMeal[meal.Id];
                            if (mealVM.Options.Any(o => o.MenuItemId == savedId))
                                mealVM.SelectedItemId = savedId;
                        }
                    }

                    dayVM.Meals.Add(mealVM);
                }

                days.Add(dayVM);
            }

            ViewBag.Days = days;
            ViewBag.PlanStatus = existingPlan != null ? existingPlan.Status : "Pending";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveMyMealPlan(FormCollection form)
        {
            int studentId = CurrentStudentId;
            if (studentId == 0) return RedirectToAction("Index", "Home");

            var today = DateTime.Now.Date;
            var publishedMenu = db.WeeklyMenus
                .OrderByDescending(w => w.StartDate)
                .FirstOrDefault(w => w.Status == "Published" && w.EndDate >= today);

            if (publishedMenu == null)
            {
                TempData["Error"] = "Cannot save meal plan — no menu has been published yet.";
                return RedirectToAction("MyMealPlan");
            }

            var plan = db.MealPlans.Include("Items")
                .FirstOrDefault(m => m.StudentId == studentId && m.WeeklyMenuId == publishedMenu.WeeklyMenuID);

            if (plan == null)
            {
                plan = new MealPlan
                {
                    StudentId = studentId,
                    WeeklyMenuId = publishedMenu.WeeklyMenuID,
                    Status = "Pending",
                    GeneratedAt = DateTime.Now
                };
                db.MealPlans.Add(plan);
                db.SaveChanges();
            }

            // Clear old items
            if (plan.Items != null && plan.Items.Count > 0)
            {
                db.MealPlanItems.RemoveRange(plan.Items);
                db.SaveChanges();
            }

            // Parse form: "item_<menuMealId>" = menuItemId
            int saved = 0, blocked = 0;
            foreach (string key in form.AllKeys)
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith("item_")) continue;

                int menuMealId;
                if (!int.TryParse(key.Substring(5), out menuMealId)) continue;

                int menuItemId;
                if (!int.TryParse(form[key], out menuItemId)) continue;
                if (menuItemId == 0) continue;

                // Safety check — never allow unsafe choices
                var check = Safety.CheckMenuItem(studentId, menuItemId);
                if (!check.IsSafe)
                {
                    blocked++;
                    continue;
                }

                db.MealPlanItems.Add(new MealPlanItem
                {
                    MealPlanId = plan.Id,
                    MenuMealId = menuMealId,
                    SelectedMenuItemId = menuItemId,
                    Status = MealPlanItemStatus.Accepted,
                    AcceptedAt = DateTime.Now,
                    Reason = "Selected by student"
                });
                saved++;
            }

            plan.Status = "Pending";
            plan.GeneratedAt = DateTime.Now;
            db.SaveChanges();

            Audit.Log("MealPlanSaved", "MealPlan", plan.Id, null,
                saved + " items saved, " + blocked + " blocked");

            if (blocked > 0)
                TempData["Error"] = saved + " meals saved. " + blocked + " unsafe item(s) were blocked.";
            else
                TempData["Success"] = saved + " meals saved. Submitted for Dietitian review.";

            return RedirectToAction("MyMealPlan");
        }

        // ══════════════════════════════════════════════════════════════
        // STUDENT — Today's Meals
        // ══════════════════════════════════════════════════════════════
        public ActionResult TodayMeals()
        {
            int studentId = CurrentStudentId;
            if (studentId == 0)
            {
                TempData["Error"] = "Only students can access this page.";
                return RedirectToAction("Index", "Home");
            }

            var today = DateTime.Now.Date;
            var publishedMenu = db.WeeklyMenus
                .Include("Days.Meals.Components.MenuItem")
                .Include("Days.Meals.Alternatives.MenuItem")
                .Include("Days.Meals.MealSlot")
                .OrderByDescending(w => w.StartDate)
                .FirstOrDefault(w => w.Status == "Published" && w.EndDate >= today);

            ViewBag.Student = db.Students.FirstOrDefault(s => s.StudentId == studentId);
            ViewBag.HasPublishedMenu = publishedMenu != null;

            if (publishedMenu == null)
            {
                ViewBag.MealCards = new List<TodayMealCard>();
                return View();
            }

            var todayMenuDay = publishedMenu.Days.FirstOrDefault(d => d.Date.Date == today);
            if (todayMenuDay == null)
            {
                ViewBag.MealCards = new List<TodayMealCard>();
                return View();
            }

            var existingSelections = db.Selections
                .Where(s => s.StudentID == studentId && s.Date == today)
                .ToList();

            var cards = new List<TodayMealCard>();

            foreach (var meal in todayMenuDay.Meals.OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99))
            {
                var slotName = meal.MealSlot != null ? meal.MealSlot.Name : "";
                var card = new TodayMealCard
                {
                    MealSlotId = meal.MealSlotId,
                    SlotName = slotName,
                    Title = meal.Title,
                    WindowOpen = CafeSettings.IsWithinSelectionWindow(slotName),
                    CutoffLabel = CafeSettings.GetSelectionCutoffLabel(slotName)
                };

                var candidates = new List<MenuItem>();
                if (meal.Components != null)
                    candidates.AddRange(meal.Components.Where(c => c.MenuItem != null).Select(c => c.MenuItem));
                if (meal.Alternatives != null)
                    candidates.AddRange(meal.Alternatives.Where(a => a.MenuItem != null).Select(a => a.MenuItem));

                candidates = candidates.GroupBy(c => c.Id).Select(g => g.First()).ToList();
                foreach (var c in candidates)
                {
                    var check = Safety.CheckMenuItem(studentId, c.Id);
                    if (!check.IsSafe) continue;
                    card.Options.Add(new StudentMealPlanItemOption
                    {
                        MenuItemId = c.Id,
                        Name = c.Name,
                        Category = c.Category,
                        IsVegetarian = c.IsVegetarian,
                        IsVegan = c.IsVegan,
                        IsGlutenFree = c.IsGlutenFree,
                        IsLactoseFree = c.IsLactoseFree
                    });
                }

                var existing = existingSelections.FirstOrDefault(s => s.MealType == slotName);
                if (existing != null)
                {
                    card.CurrentSelectionName = existing.Meal;
                    card.IsCollected = existing.IsCollected;
                    card.CollectedOn = existing.CollectedOn;
                }

                cards.Add(card);
            }

            ViewBag.MealCards = cards;
            ViewBag.Today = today;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveTodayMeal(int mealSlotId, int menuItemId)
        {
            int studentId = CurrentStudentId;
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var slot = db.MealSlots.Find(mealSlotId);
            if (slot == null)
            {
                TempData["Error"] = "Unknown meal slot.";
                return RedirectToAction("TodayMeals");
            }

            if (!CafeSettings.IsWithinSelectionWindow(slot.Name))
            {
                TempData["Error"] = slot.Name + " selection window closed at " +
                                    CafeSettings.GetSelectionCutoffLabel(slot.Name) + ". ";
                return RedirectToAction("TodayMeals");
            }

            var item = db.MenuItems.Find(menuItemId);
            if (item == null)
            {
                TempData["Error"] = "Unknown item.";
                return RedirectToAction("TodayMeals");
            }

            var check = Safety.CheckMenuItem(studentId, menuItemId);
            if (!check.IsSafe)
            {
                TempData["Error"] = "Cannot select '" + item.Name + "' — " + check.Reason;
                return RedirectToAction("TodayMeals");
            }

            var today = DateTime.Now.Date;
            var existing = db.Selections.FirstOrDefault(s =>
                s.StudentID == studentId && s.Date == today && s.MealType == slot.Name);

            if (existing != null)
            {
                existing.Meal = item.Name;
                existing.SelectedOn = DateTime.Now;
                existing.VerificationMethod = "Manual";
            }
            else
            {
                db.Selections.Add(new Selection
                {
                    StudentID = studentId,
                    Meal = item.Name,
                    Date = today,
                    MealType = slot.Name,
                    SelectedOn = DateTime.Now,
                    QRCodeValue = string.Format("MHS_{0}_{1}_{2}", studentId, slot.Name, DateTime.Now.Ticks),
                    IsCollected = false,
                    VerificationMethod = "Manual"
                });
            }

            // Mirror into the new MealSelection table for future phases
            try
            {
                var menuMeal = db.MenuMeals
                    .Include("MenuDay")
                    .Include("MenuDay.WeeklyMenu")
                    .FirstOrDefault(m => m.MealSlotId == mealSlotId
                                       && m.MenuDay.Date == today
                                       && m.MenuDay.WeeklyMenu.Status == "Published");

                if (menuMeal != null)
                {
                    var ms = db.MealSelections.FirstOrDefault(x =>
                        x.StudentId == studentId && x.MenuMealId == menuMeal.Id);

                    if (ms != null)
                    {
                        ms.MenuItemId = menuItemId;
                        ms.SelectedAt = DateTime.Now;
                        ms.Status = MealSelectionStatus.Selected;
                    }
                    else
                    {
                        db.MealSelections.Add(new MealSelection
                        {
                            StudentId = studentId,
                            MenuMealId = menuMeal.Id,
                            MenuItemId = menuItemId,
                            SelectedAt = DateTime.Now,
                            Status = MealSelectionStatus.Selected
                        });
                    }
                }
            }
            catch { /* non-critical */ }

            db.SaveChanges();
            Audit.Log("MealSelected", "Selection", 0, null, slot.Name + ": " + item.Name);

            TempData["Success"] = "Selected for " + slot.Name + ": " + item.Name;
            return RedirectToAction("TodayMeals");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SelectMeal(string meal, string mealType)
        {
            int studentId = CurrentStudentId;
            if (studentId == 0)
                return Json(new { success = false, message = "Not authenticated" });

            if (string.IsNullOrWhiteSpace(mealType))
                return Json(new { success = false, message = "Meal type required" });

            if (!CafeSettings.IsWithinSelectionWindow(mealType))
                return Json(new
                {
                    success = false,
                    closed = true,
                    message = mealType + " window closed at " + CafeSettings.GetSelectionCutoffLabel(mealType) + "."
                });

            var today = DateTime.Now.Date;
            var existing = db.Selections
                .FirstOrDefault(s => s.StudentID == studentId && s.Date == today && s.MealType == mealType);

            if (existing != null)
            {
                existing.Meal = meal;
                existing.SelectedOn = DateTime.Now;
                existing.VerificationMethod = "Manual";
            }
            else
            {
                db.Selections.Add(new Selection
                {
                    StudentID = studentId,
                    Meal = meal,
                    Date = today,
                    MealType = mealType,
                    SelectedOn = DateTime.Now,
                    QRCodeValue = string.Format("MHS_{0}_{1}_{2}", studentId, mealType, DateTime.Now.Ticks),
                    IsCollected = false,
                    VerificationMethod = "Manual"
                });
            }
            db.SaveChanges();
            return Json(new { success = true, message = "Meal selected" });
        }

        public ActionResult MyMealQR()
        {
            int studentId = CurrentStudentId;
            if (studentId == 0) return RedirectToAction("Index", "Home");

            var today = DateTime.Now.Date;
            var selections = db.Selections
                .Where(s => s.StudentID == studentId && s.Date == today)
                .OrderBy(s => s.MealType).ToList();

            ViewBag.Role = CurrentRole;
            return View(selections);
        }

        // ══════════════════════════════════════════════════════════════
        // WEEKLY MENU
        // ══════════════════════════════════════════════════════════════
        public ActionResult WeeklyMenu()
        {
            var menus = db.WeeklyMenus
                .OrderByDescending(w => w.StartDate).Take(10).ToList();
            ViewBag.Role = CurrentRole;
            return View(menus);
        }

        public ActionResult CreateWeeklyMenu()
        {
            ViewBag.AllRecipes = db.Recipes.Where(r => r.IsActive).OrderBy(r => r.Name).ToList();
            ViewBag.Role = CurrentRole;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateWeeklyMenu(WeeklyMenu menu)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(menu.WeekNumber))
                    menu.WeekNumber = "W" + (DateTime.Now.DayOfYear / 7 + 1);

                menu.CreatedDate = DateTime.Now;
                menu.CreatedBy = CurrentUserName;
                menu.CreatedByUserId = CurrentUserId;
                menu.Status = "Draft";

                db.WeeklyMenus.Add(menu);
                db.SaveChanges();

                Audit.Log("MenuCreated", "WeeklyMenu", menu.WeeklyMenuID, null, menu.WeekNumber);

                try { NotificationHelper.NotifyChefMenuReview(db, menu, CurrentUserName); } catch { }

                TempData["Success"] = "Menu saved as Draft. Chef has been notified to review.";
                return RedirectToAction("MenuEditor", new { id = menu.WeeklyMenuID });
            }

            ViewBag.AllRecipes = db.Recipes.Where(r => r.IsActive).ToList();
            ViewBag.Role = CurrentRole;
            return View(menu);
        }

        // ─── Menu Editor (day / meal / component grid) ────────────────
        public ActionResult MenuEditor(int id, int? sel = null)
        {
            var menu = db.WeeklyMenus
                .Include("Days.Meals.Components.MenuItem")
                .Include("Days.Meals.Alternatives.MenuItem")
                .Include("Days.Meals.MealSlot")
                .FirstOrDefault(w => w.WeeklyMenuID == id);

            if (menu == null) return HttpNotFound();

            var validation = Validator.ValidateWeeklyMenu(id);

            ViewBag.Menu = menu;
            ViewBag.Validation = validation;
            ViewBag.MealSlots = db.MealSlots.Where(m => m.IsActive).OrderBy(m => m.DisplayOrder).ToList();
            ViewBag.MenuItems = db.MenuItems.Where(m => m.IsActive).OrderBy(m => m.Name).ToList();
            ViewBag.SelectedMealId = sel.HasValue ? sel.Value : 0;
            ViewBag.Role = CurrentRole;
            return View(menu);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddMenuMeal(int weeklyMenuId, DateTime date, int mealSlotId, string title, string notes)
        {
            var menu = db.WeeklyMenus.Find(weeklyMenuId);
            if (menu == null) return HttpNotFound();

            var slot = db.MealSlots.Find(mealSlotId);
            if (slot == null)
            {
                TempData["Error"] = "Meal slot not found.";
                return RedirectToAction("MenuEditor", new { id = weeklyMenuId });
            }

            // Auto-title if the caller didn't provide one
            if (string.IsNullOrWhiteSpace(title))
            {
                title = date.DayOfWeek + " " + slot.Name;
            }

            var day = db.MenuDays.FirstOrDefault(d => d.WeeklyMenuId == weeklyMenuId && d.Date == date.Date);
            if (day == null)
            {
                day = new MenuDay
                {
                    WeeklyMenuId = weeklyMenuId,
                    Date = date.Date,
                    DayOfWeek = date.DayOfWeek
                };
                db.MenuDays.Add(day);
                db.SaveChanges();
            }

            var existing = db.MenuMeals.FirstOrDefault(m => m.MenuDayId == day.Id && m.MealSlotId == mealSlotId);
            if (existing == null)
            {
                var newMeal = new MenuMeal
                {
                    MenuDayId = day.Id,
                    MealSlotId = mealSlotId,
                    Title = title,
                    Notes = notes ?? ""
                };
                db.MenuMeals.Add(newMeal);
                db.SaveChanges();

                Audit.Log("MenuMealAdded", "MenuMeal", newMeal.Id, null, title);

                // Redirect straight into the new meal so the panel opens automatically
                return RedirectToAction("MenuEditor", new { id = weeklyMenuId, sel = newMeal.Id });
            }
            else
            {
                // Update title if changed
                if (!string.IsNullOrWhiteSpace(title) && existing.Title != title)
                {
                    existing.Title = title;
                    existing.Notes = notes ?? existing.Notes;
                    db.SaveChanges();
                }
                return RedirectToAction("MenuEditor", new { id = weeklyMenuId, sel = existing.Id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddMenuComponent(int menuMealId, int menuItemId, int componentType)
        {
            var meal = db.MenuMeals.Find(menuMealId);
            if (meal == null) return HttpNotFound();

            db.MenuMealComponents.Add(new MenuMealComponent
            {
                MenuMealId = menuMealId,
                MenuItemId = menuItemId,
                ComponentType = (MenuMealComponentType)componentType,
                IsDefault = true
            });
            db.SaveChanges();

            TempData["Success"] = "Component added.";
            return RedirectToAction("MenuEditor", new { id = meal.MenuDay.WeeklyMenuId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddMenuAlternative(int menuMealId, int menuItemId, string reason)
        {
            var meal = db.MenuMeals.Find(menuMealId);
            if (meal == null) return HttpNotFound();

            db.MenuMealAlternatives.Add(new MenuMealAlternative
            {
                MenuMealId = menuMealId,
                MenuItemId = menuItemId,
                Reason = reason ?? "Alternative"
            });
            db.SaveChanges();

            TempData["Success"] = "Alternative added.";
            return RedirectToAction("MenuEditor", new { id = meal.MenuDay.WeeklyMenuId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMenuMeal(int id)
        {
            var meal = db.MenuMeals.Include("MenuDay").FirstOrDefault(m => m.Id == id);
            if (meal == null) return HttpNotFound();

            int menuId = meal.MenuDay.WeeklyMenuId;

            // Remove components + alternatives first
            var comps = db.MenuMealComponents.Where(c => c.MenuMealId == id).ToList();
            var alts = db.MenuMealAlternatives.Where(a => a.MenuMealId == id).ToList();
            db.MenuMealComponents.RemoveRange(comps);
            db.MenuMealAlternatives.RemoveRange(alts);
            db.MenuMeals.Remove(meal);
            db.SaveChanges();

            Audit.Log("MenuMealDeleted", "MenuMeal", id, null, null);
            TempData["Success"] = "Meal removed.";
            return RedirectToAction("MenuEditor", new { id = menuId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMenuComponent(int id)
        {
            var comp = db.MenuMealComponents.Include("MenuMeal.MenuDay")
                .FirstOrDefault(c => c.Id == id);
            if (comp == null) return HttpNotFound();

            int menuId = comp.MenuMeal.MenuDay.WeeklyMenuId;
            int mealId = comp.MenuMealId;

            db.MenuMealComponents.Remove(comp);
            db.SaveChanges();

            TempData["Success"] = "Component removed.";
            return RedirectToAction("MenuEditor", new { id = menuId, sel = mealId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMenuAlternative(int id)
        {
            var alt = db.MenuMealAlternatives.Include("MenuMeal.MenuDay")
                .FirstOrDefault(a => a.Id == id);
            if (alt == null) return HttpNotFound();

            int menuId = alt.MenuMeal.MenuDay.WeeklyMenuId;
            int mealId = alt.MenuMealId;

            db.MenuMealAlternatives.Remove(alt);
            db.SaveChanges();

            TempData["Success"] = "Alternative removed.";
            return RedirectToAction("MenuEditor", new { id = menuId, sel = mealId });
        }

        // ─── Publish (BLOCKS ON CRITICAL ISSUES) ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PublishWeeklyMenu(int id)
        {
            var menu = db.WeeklyMenus.Find(id);
            if (menu == null) return HttpNotFound();

            if (menu.Status != "ChefApproved")
            {
                TempData["Error"] = "Cannot publish. Menu must be approved by the Chef first. " +
                                    "Current status: " + menu.Status;
                return RedirectToAction("MenuEditor", new { id = id });
            }

            // 🔒 HARD BLOCK — Section 42, 107, 135
            var validation = Validator.ValidateWeeklyMenu(id);
            if (!validation.IsReadyToPublish)
            {
                TempData["Error"] = "Cannot publish menu. " + validation.CriticalCount +
                                    " critical issue(s) must be resolved first.";
                return RedirectToAction("MenuIssues", new { id = id });
            }

            menu.Status = "Published";
            menu.PublishedDate = DateTime.Now;
            menu.PublishedBy = CurrentUserName;
            menu.PublishedByUserId = CurrentUserId;
            db.SaveChanges();

            Audit.Log("MenuPublished", "WeeklyMenu", id, null, menu.WeekNumber);

            try { NotificationHelper.NotifyStudentsMenuPublished(db, menu); } catch { }

            TempData["Success"] = "Menu published! " + validation.WarningCount +
                                  " warning(s) were noted. All students notified.";
            return RedirectToAction("WeeklyMenu");
        }

        // ─── Validation issues view ──────────────────────────────────
        public ActionResult MenuIssues(int id)
        {
            var menu = db.WeeklyMenus.Find(id);
            if (menu == null) return HttpNotFound();

            var validation = Validator.ValidateWeeklyMenu(id);

            ViewBag.Menu = menu;
            ViewBag.Validation = validation;
            ViewBag.Role = CurrentRole;
            return View(validation);
        }

        // ══════════════════════════════════════════════════════════════
        // CHEF — Menu Review
        // ══════════════════════════════════════════════════════════════
        public ActionResult ChefReviewMenu(int id)
        {
            var menu = db.WeeklyMenus
                .Include("Days.Meals.Components.MenuItem")
                .Include("Days.Meals.Alternatives.MenuItem")
                .Include("Days.Meals.MealSlot")
                .FirstOrDefault(w => w.WeeklyMenuID == id);

            if (menu == null) return HttpNotFound();

            var validation = Validator.ValidateWeeklyMenu(id);

            // Stock warnings
            var stockWarnings = new List<string>();
            var foodItems = db.InventoryItems.Where(i => i.IsActive && i.Category == "Food").ToList();
            foreach (var item in foodItems.Where(i => i.CurrentStock <= i.ReorderLevel))
            {
                stockWarnings.Add(item.Name + ": only " + item.CurrentStock + " " + item.Unit +
                                  " in stock (min " + item.ReorderLevel + ")");
            }

            // Strongly-typed per-day meal summaries
            var mealSummaries = new List<ChefReviewDay>();
            var days = menu.Days.OrderBy(d => d.Date).ToList();

            foreach (var d in days)
            {
                var day = new ChefReviewDay
                {
                    Date = d.Date,
                    DayOfWeek = d.Date.ToString("dddd")
                };

                foreach (var m in d.Meals.OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99))
                {
                    day.Meals.Add(new ChefReviewMeal
                    {
                        MealId = m.Id,
                        SlotName = m.MealSlot != null ? m.MealSlot.Name : "",
                        Title = m.Title,
                        ComponentCount = m.Components != null ? m.Components.Count : 0,
                        AlternativeCount = m.Alternatives != null ? m.Alternatives.Count : 0,
                        Components = m.Components.Select(c => c.MenuItem != null ? c.MenuItem.Name : "").ToList(),
                        Alternatives = m.Alternatives.Select(a => a.MenuItem != null ? a.MenuItem.Name : "").ToList()
                    });
                }
                mealSummaries.Add(day);
            }

            // Demand forecast (best-effort)
            int totalForecast = 0;
            try
            {
                var forecastSvc = new DemandForecastService(db);
                foreach (var d in days)
                {
                    foreach (var m in d.Meals)
                    {
                        var f = forecastSvc.ForecastForMeal(m.Id);
                        totalForecast += f.ForecastTotal;
                    }
                }
            }
            catch { /* non-fatal */ }

            ViewBag.StockWarnings = stockWarnings;
            ViewBag.Validation = validation;
            ViewBag.MealSummaries = mealSummaries;
            ViewBag.TotalForecast = totalForecast;
            ViewBag.Role = CurrentRole;
            return View(menu);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveMenu(int id)
        {
            var menu = db.WeeklyMenus.Find(id);
            if (menu == null) return HttpNotFound();

            menu.Status = "ChefApproved";
            menu.ChefReviewedBy = CurrentUserName;
            menu.ChefReviewedAt = DateTime.Now;
            db.SaveChanges();

            Audit.Log("MenuChefApproved", "WeeklyMenu", id, null, menu.WeekNumber);

            try { NotificationHelper.NotifyMealCoordinatorChefDecision(db, menu, "Approved", ""); } catch { }

            TempData["Success"] = "Menu approved. Meal Coordinator can now publish it.";
            return RedirectToAction("WeeklyMenu");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestMenuChanges(int id, string comments)
        {
            var menu = db.WeeklyMenus.Find(id);
            if (menu == null) return HttpNotFound();

            menu.Status = "Draft";
            menu.ChefComments = comments;
            menu.ChefReviewedBy = CurrentUserName;
            menu.ChefReviewedAt = DateTime.Now;
            db.SaveChanges();

            Audit.Log("MenuChangesRequested", "WeeklyMenu", id, "ChefApproved", "Draft", comments);

            try { NotificationHelper.NotifyMealCoordinatorChefDecision(db, menu, "ChangesRequested", comments); } catch { }

            TempData["Success"] = "Changes requested.";
            return RedirectToAction("WeeklyMenu");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectMenu(int id, string reason)
        {
            var menu = db.WeeklyMenus.Find(id);
            if (menu == null) return HttpNotFound();

            menu.Status = "Rejected";
            menu.ChefComments = reason;
            menu.ChefReviewedBy = CurrentUserName;
            menu.ChefReviewedAt = DateTime.Now;
            db.SaveChanges();

            Audit.Log("MenuRejected", "WeeklyMenu", id, null, null, reason);

            try { NotificationHelper.NotifyMealCoordinatorChefDecision(db, menu, "Rejected", reason); } catch { }

            TempData["Success"] = "Menu rejected.";
            return RedirectToAction("WeeklyMenu");
        }

        // ══════════════════════════════════════════════════════════════
        // KITCHEN
        // ══════════════════════════════════════════════════════════════
        public ActionResult KitchenDashboard()
        {
            var today = DateTime.Now.Date;

            var publishedMenu = db.WeeklyMenus
                .Include("Days.Meals.MealSlot")
                .OrderByDescending(w => w.StartDate)
                .FirstOrDefault(w => w.Status == "Published" && w.EndDate >= today);

            var stats = new List<KitchenMealStats>();

            if (publishedMenu != null)
            {
                var todayDay = publishedMenu.Days.FirstOrDefault(d => d.Date.Date == today);

                if (todayDay != null)
                {
                    // Look up dietary categories once (for accurate counts)
                    var vegCategory = db.DietaryCategories
                        .FirstOrDefault(c => c.Name != null && c.Name.ToLower() == "vegetarian");
                    var veganCategory = db.DietaryCategories
                        .FirstOrDefault(c => c.Name != null && c.Name.ToLower() == "vegan");

                    int vegCount = vegCategory != null
                        ? db.StudentDietaryRecords.Count(r =>
                            r.IsActive && r.Status == DietaryRequestStatus.Approved &&
                            r.RecordType == DietaryRecordType.DietaryPreference &&
                            r.DietaryCategoryId == vegCategory.Id)
                        : 0;

                    int veganCount = veganCategory != null
                        ? db.StudentDietaryRecords.Count(r =>
                            r.IsActive && r.Status == DietaryRequestStatus.Approved &&
                            r.RecordType == DietaryRecordType.DietaryPreference &&
                            r.DietaryCategoryId == veganCategory.Id)
                        : 0;

                    int allergyCount = db.StudentDietaryRecords
                        .Where(r => r.IsActive && r.Status == DietaryRequestStatus.Approved &&
                                    r.RecordType == DietaryRecordType.Allergy)
                        .Select(r => r.StudentId).Distinct().Count();

                    int medicalCount = db.StudentDietaryRecords
                        .Where(r => r.IsActive && r.Status == DietaryRequestStatus.Approved &&
                                    r.RecordType == DietaryRecordType.MedicalRestriction)
                        .Select(r => r.StudentId).Distinct().Count();

                    var forecastSvc = new DemandForecastService(db);

                    foreach (var meal in todayDay.Meals
                        .OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99))
                    {
                        var slotName = meal.MealSlot != null ? meal.MealSlot.Name : "";

                        var selections = db.Selections
                            .Where(s => s.Date == today && s.MealType == slotName)
                            .ToList();

                        int selectedCount = selections.Count;
                        int servedCount = selections.Count(s => s.IsCollected);

                        int expectedCount = 0;
                        try
                        {
                            var f = forecastSvc.ForecastForMeal(meal.Id);
                            expectedCount = f.ForecastTotal;
                        }
                        catch { expectedCount = selectedCount; }

                        var prod = db.ProductionRecords
                            .FirstOrDefault(p => p.MenuMealId == meal.Id
                                              && p.ProductionDate == today);

                        stats.Add(new KitchenMealStats
                        {
                            MenuMealId = meal.Id,
                            SlotName = slotName,
                            Title = meal.Title,
                            Expected = expectedCount,
                            Selected = selectedCount,
                            Served = servedCount,
                            Produced = prod != null ? prod.ProducedQuantity : 0,
                            Target = prod != null ? prod.TargetQuantity : expectedCount,
                            VegetarianCount = vegCount,
                            VeganCount = veganCount,
                            AllergySafeCount = allergyCount,
                            MedicalCount = medicalCount,
                            HasProductionRecord = prod != null
                        });
                    }
                }
            }

            ViewBag.Stats = stats;
            ViewBag.Today = today;
            ViewBag.PublishedMenu = publishedMenu;
            ViewBag.TotalExpected = stats.Sum(s => s.Expected);
            ViewBag.TotalSelected = stats.Sum(s => s.Selected);
            ViewBag.TotalServed = stats.Sum(s => s.Served);
            ViewBag.Role = CurrentRole;
            return View();
        }
        // ══════════════════════════════════════════════════════════════
        // QR MEAL COLLECTION
        // ══════════════════════════════════════════════════════════════
        public ActionResult ScanMealQR()
        {
            var today = DateTime.Now.Date;

            var publishedMenu = db.WeeklyMenus
                .Include("Days.Meals.MealSlot")
                .OrderByDescending(w => w.StartDate)
                .FirstOrDefault(w => w.Status == "Published"
                                  && w.StartDate <= today
                                  && w.EndDate >= today);

            ViewBag.HasPublishedMenu = publishedMenu != null;
            ViewBag.PublishedMenu = publishedMenu;
            ViewBag.CurrentSlotName = publishedMenu != null
                ? ResolveCurrentMealSlotName(today, publishedMenu.WeeklyMenuID)
                : "";
            ViewBag.Role = CurrentRole;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyMealQR(string qrValue)
        {
            if (string.IsNullOrWhiteSpace(qrValue))
                return Json(new MealQRVerificationResult { Success = false, Message = "QR value is required." });

            var value = qrValue.Trim();
            var today = DateTime.Now.Date;

            // ─── 1. Resolve the student ──────────────────────────
            Student student = null;

            var studentQR = db.StudentQRCodes
                .Include("Student")
                .FirstOrDefault(q => q.IsActive && q.QRCodeValue == value);
            if (studentQR != null) student = studentQR.Student;

            if (student == null)
                student = db.Students.FirstOrDefault(s => s.StudentNumber == value);

            if (student == null)
                student = db.Students.Include("User")
                    .FirstOrDefault(s => s.User != null && s.User.Email == value);

            if (student == null)
            {
                return Json(new MealQRVerificationResult
                {
                    Success = false,
                    Message = "QR not recognized. Try again or use manual student number lookup."
                });
            }

            string studentDisplayName = student.FirstName + " " + student.LastName;

            // ─── 2. Published menu covering today ────────────────
            var publishedMenu = db.WeeklyMenus
                .OrderByDescending(w => w.StartDate)
                .FirstOrDefault(w => w.Status == "Published"
                                  && w.StartDate <= today
                                  && w.EndDate >= today);

            if (publishedMenu == null)
            {
                return Json(new MealQRVerificationResult
                {
                    Success = false,
                    Message = "No published menu covers today.",
                    StudentId = student.StudentId,
                    StudentName = studentDisplayName
                });
            }

            // ─── 3. Resolve today's meal slot ────────────────────
            var currentSlotName = ResolveCurrentMealSlotName(today, publishedMenu.WeeklyMenuID);
            if (string.IsNullOrEmpty(currentSlotName))
            {
                return Json(new MealQRVerificationResult
                {
                    Success = false,
                    Message = "No meal is being served right now.",
                    StudentId = student.StudentId,
                    StudentName = studentDisplayName
                });
            }

            // ─── 4. Student's selection for this slot ────────────
            var selection = db.Selections
                .FirstOrDefault(s => s.StudentID == student.StudentId
                                  && s.Date == today
                                  && s.MealType == currentSlotName);

            if (selection == null)
            {
                return Json(new MealQRVerificationResult
                {
                    Success = false,
                    Message = "No selection found for " + currentSlotName + ". Student must select a meal first.",
                    StudentId = student.StudentId,
                    StudentName = studentDisplayName,
                    StudentNumber = student.StudentNumber,
                    GradeLevel = student.GradeLevel.ToString(),
                    MealSlotName = currentSlotName
                });
            }

            // ─── 5. Duplicate check ──────────────────────────────
            if (selection.IsCollected)
            {
                return Json(new MealQRVerificationResult
                {
                    Success = false,
                    AlreadyServed = true,
                    Message = "Meal already collected.",
                    StudentId = student.StudentId,
                    StudentName = studentDisplayName,
                    StudentNumber = student.StudentNumber,
                    GradeLevel = student.GradeLevel.ToString(),
                    MealSlotName = currentSlotName,
                    SelectedItemName = selection.Meal,
                    ServedAt = selection.CollectedOn
                });
            }

            // ─── 6. Dietary safety check ─────────────────────────
            var menuItem = db.MenuItems
                .FirstOrDefault(m => m.Name == selection.Meal);

            if (menuItem == null)
            {
                menuItem = db.MenuItems
                    .Where(m => m.Name != null)
                    .ToList()
                    .FirstOrDefault(m => m.Name.Equals(selection.Meal, StringComparison.OrdinalIgnoreCase));
            }

            if (menuItem != null)
            {
                var safety = Safety.CheckMenuItem(student.StudentId, menuItem.Id);
                if (!safety.IsSafe)
                {
                    return Json(new MealQRVerificationResult
                    {
                        Success = false,
                        SafetyBlocked = true,
                        Message = "SAFETY BLOCK — do not serve. " + safety.Reason,
                        StudentId = student.StudentId,
                        StudentName = studentDisplayName,
                        StudentNumber = student.StudentNumber,
                        GradeLevel = student.GradeLevel.ToString(),
                        MealSlotName = currentSlotName,
                        SelectedItemName = selection.Meal,
                        SafetyReason = safety.Reason
                    });
                }
            }

            // ─── 7. Verification successful ──────────────────────
            var hall = db.DiningHalls
                .Where(h => h.IsActive)
                .OrderBy(h => h.Name)
                .FirstOrDefault();

            return Json(new MealQRVerificationResult
            {
                Success = true,
                Message = "Verified — ready to serve.",
                StudentId = student.StudentId,
                StudentName = studentDisplayName,
                StudentNumber = student.StudentNumber,
                GradeLevel = student.GradeLevel.ToString(),
                MealSlotName = currentSlotName,
                SelectedItemName = selection.Meal,
                SelectionId = selection.SelectionID,
                DiningHallName = hall != null ? hall.Name : "Dining Hall"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmServeMeal(int selectionId)
        {
            var sel = db.Selections.FirstOrDefault(s => s.SelectionID == selectionId);
            if (sel == null)
                return Json(new { success = false, message = "Selection not found." });

            if (sel.IsCollected)
            {
                var at = sel.CollectedOn.HasValue ? sel.CollectedOn.Value.ToString("HH:mm") : "earlier";
                return Json(new { success = false, message = "Already served at " + at + "." });
            }

            // Final safety re-check (defense in depth)
            var menuItem = db.MenuItems
                .Where(m => m.Name != null)
                .ToList()
                .FirstOrDefault(m => m.Name.Equals(sel.Meal, StringComparison.OrdinalIgnoreCase));

            if (menuItem != null)
            {
                var safety = Safety.CheckMenuItem(sel.StudentID, menuItem.Id);
                if (!safety.IsSafe)
                {
                    return Json(new { success = false, message = "SAFETY BLOCK — " + safety.Reason });
                }
            }

            // Record the serving on the legacy Selection record
            sel.IsCollected = true;
            sel.CollectedOn = DateTime.Now;
            sel.CollectedBy = CurrentUserName;
            sel.VerificationMethod = "QR Code";
            db.SaveChanges();

            // Also record into the new MealService table (best effort)
            try
            {
                var menuMeal = db.MenuMeals
                    .Include("MenuDay")
                    .Include("MealSlot")
                    .FirstOrDefault(m => m.MenuDay.Date == sel.Date
                                      && m.MealSlot.Name == sel.MealType);

                if (menuMeal != null)
                {
                    var hall = db.DiningHalls.Where(h => h.IsActive).OrderBy(h => h.Name).FirstOrDefault();
                    int hallId = hall != null ? hall.Id : 1;

                    db.MealServices.Add(new MealService
                    {
                        StudentId = sel.StudentID,
                        MenuMealId = menuMeal.Id,
                        DiningHallId = hallId,
                        Method = MealServiceMethod.QR,
                        Status = MealSelectionStatus.Served,
                        ServedAt = DateTime.Now,
                        ServedByUserId = CurrentUserId
                    });
                    db.SaveChanges();
                }
            }
            catch { /* non-critical */ }

            Audit.LogMealServed(0, sel.StudentID, 0, false, "QR verification");

            return Json(new { success = true, message = "Meal served successfully." });
        }

        // ─── helper: pick the meal slot matching current time ──
        private string ResolveCurrentMealSlotName(DateTime date, int weeklyMenuId)
        {
            var day = db.MenuDays
                .Include("Meals.MealSlot")
                .FirstOrDefault(d => d.WeeklyMenuId == weeklyMenuId && d.Date == date);

            if (day == null) return "";

            var now = DateTime.Now.TimeOfDay;
            MealSlot best = null;
            MealSlot active = null;

            foreach (var meal in day.Meals.OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99))
            {
                var slot = meal.MealSlot;
                if (slot == null) continue;

                if (slot.DefaultStartTime.HasValue && slot.DefaultEndTime.HasValue)
                {
                    if (now >= slot.DefaultStartTime.Value && now <= slot.DefaultEndTime.Value)
                    {
                        active = slot;
                        break;
                    }
                }

                if (slot.DefaultStartTime.HasValue && slot.DefaultStartTime.Value <= now)
                    best = slot;
            }

            if (active != null) return active.Name;
            if (best != null) return best.Name;

            var first = day.Meals
                .OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99)
                .FirstOrDefault();
            return first != null && first.MealSlot != null ? first.MealSlot.Name : "";
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ServeSelection(int id)
        {
            var sel = db.Selections.Find(id);
            if (sel == null) return Json(new { success = false, message = "Not found" });
            if (sel.IsCollected) return Json(new { success = false, message = "Already collected" });

            sel.IsCollected = true;
            sel.CollectedOn = DateTime.Now;
            sel.CollectedBy = CurrentUserName;
            sel.VerificationMethod = "Manual (Staff)";
            db.SaveChanges();

            Audit.Log("MealServedManual", "Selection", id, null, "StudentId=" + sel.StudentID);
            return Json(new { success = true, message = "Served" });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveProduction(int menuMealId, DateTime productionDate, int producedQty, int targetQty, string notes)
        {
            var meal = db.MenuMeals.Include("MealSlot").FirstOrDefault(m => m.Id == menuMealId);
            if (meal == null) return HttpNotFound();

            var date = productionDate.Date;
            var existing = db.ProductionRecords
                .FirstOrDefault(p => p.MenuMealId == menuMealId && p.ProductionDate == date);

            if (existing == null)
            {
                db.ProductionRecords.Add(new ProductionRecord
                {
                    MenuMealId = menuMealId,
                    ProductionDate = date,
                    TargetQuantity = targetQty,
                    ProducedQuantity = producedQty,
                    Notes = notes ?? "",
                    RecordedByUserId = CurrentUserId,
                    RecordedAt = DateTime.Now
                });
            }
            else
            {
                existing.TargetQuantity = targetQty;
                existing.ProducedQuantity = producedQty;
                existing.Notes = notes ?? existing.Notes;
                existing.RecordedByUserId = CurrentUserId;
                existing.RecordedAt = DateTime.Now;
            }

            db.SaveChanges();
            Audit.Log("ProductionRecorded", "ProductionRecord", menuMealId, null,
                "Produced=" + producedQty + " Target=" + targetQty);

            TempData["Success"] = "Production recorded for " +
                (meal.MealSlot != null ? meal.MealSlot.Name : "meal") + ".";
            return RedirectToAction("KitchenDashboard");
        }

        public ActionResult RecordWaste()
        {
            var today = DateTime.Now.Date;
            var sevenDaysAgo = today.AddDays(-7);   // ← computed OUTSIDE the query

            ViewBag.MenuMeals = db.MenuMeals
                .Include("MealSlot")
                .Include("MenuDay")
                .Where(m => m.MenuDay.Date == today)
                .ToList();

            ViewBag.AllInventoryItems = db.InventoryItems
                .Where(i => i.IsActive)
                .OrderBy(i => i.Name).ToList();

            ViewBag.RecentWaste = db.WasteRecords
                .Where(w => w.RecordedDate >= sevenDaysAgo)
                .OrderByDescending(w => w.RecordedDate)
                .Take(20).ToList();

            ViewBag.Role = CurrentRole;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RecordWaste(int? menuMealId, int? inventoryItemId, decimal quantity,
            string unit, int reason, string notes)
        {
            if (quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than zero.";
                return RedirectToAction("RecordWaste");
            }

            var record = new WasteRecord
            {
                MenuMealId = menuMealId,
                InventoryItemId = inventoryItemId,
                Quantity = quantity,
                Unit = unit,
                Reason = (WasteReason)reason,
                Notes = notes ?? "",
                RecordedDate = DateTime.Now,
                RecordedByUserId = CurrentUserId
            };

            db.WasteRecords.Add(record);

            // Optionally deduct from inventory if item was specified
            if (inventoryItemId.HasValue)
            {
                var inv = db.InventoryItems.Find(inventoryItemId.Value);
                if (inv != null)
                {
                    var oldStock = inv.CurrentStock;
                    inv.CurrentStock = Math.Max(0, inv.CurrentStock - quantity);
                    Audit.LogStockAdjustment(inv.Id, oldStock, inv.CurrentStock,
                        "Waste recorded: " + ((WasteReason)reason).ToString());
                }
            }

            db.SaveChanges();

            Audit.Log("WasteRecorded", "WasteRecord", record.Id, null,
                quantity + " " + unit + " — " + ((WasteReason)reason).ToString());

            TempData["Success"] = "Waste recorded.";
            return RedirectToAction("RecordWaste");
        }
        // ══════════════════════════════════════════════════════════════
        // RECIPES
        // ══════════════════════════════════════════════════════════════
        public ActionResult Recipes()
        {
            ViewBag.Role = CurrentRole;
            return View(db.Recipes.OrderBy(r => r.Name).ToList());
        }

        public ActionResult AddRecipe() { ViewBag.Role = CurrentRole; return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddRecipe(Recipe recipe)
        {
            if (ModelState.IsValid)
            {
                recipe.CreatedAt = DateTime.Now;
                recipe.IsActive = true;
                db.Recipes.Add(recipe);
                db.SaveChanges();
                Audit.Log("RecipeAdded", "Recipe", recipe.Id, null, recipe.Name);
                TempData["Success"] = "Recipe added.";
                return RedirectToAction("Recipes");
            }
            ViewBag.Role = CurrentRole;
            return View(recipe);
        }

        public ActionResult EditRecipe(int id)
        {
            var r = db.Recipes.Find(id);
            if (r == null) return HttpNotFound();
            ViewBag.Role = CurrentRole;
            return View(r);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditRecipe(Recipe recipe)
        {
            if (ModelState.IsValid)
            {
                db.Entry(recipe).State = EntityState.Modified;
                db.SaveChanges();
                Audit.Log("RecipeEdited", "Recipe", recipe.Id, null, recipe.Name);
                TempData["Success"] = "Recipe updated.";
                return RedirectToAction("Recipes");
            }
            ViewBag.Role = CurrentRole;
            return View(recipe);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteRecipe(int id)
        {
            var r = db.Recipes.Find(id);
            if (r != null)
            {
                r.IsActive = false;
                db.SaveChanges();
                Audit.Log("RecipeDeactivated", "Recipe", id, null, r.Name);
            }
            TempData["Success"] = "Recipe removed.";
            return RedirectToAction("Recipes");
        }

        // ══════════════════════════════════════════════════════════════
        // INVENTORY & STOCK
        // ══════════════════════════════════════════════════════════════
        public ActionResult Inventory()
        {
            var cats = new[] { "Food", "Beverage", "Kitchen Supply" };
            var inv = db.InventoryItems
                .Where(i => i.IsActive && cats.Contains(i.Category))
                .OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
            ViewBag.Role = CurrentRole;
            return View(inv);
        }

        public ActionResult AddInventoryItem() { ViewBag.Role = CurrentRole; return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddInventoryItem(InventoryItem item)
        {
            if (ModelState.IsValid)
            {
                item.CreatedAt = DateTime.Now;
                item.IsActive = true;
                db.InventoryItems.Add(item);
                db.SaveChanges();
                Audit.Log("InventoryItemAdded", "InventoryItem", item.Id, null, item.Name);
                TempData["Success"] = "Item added.";
                return RedirectToAction("Inventory");
            }
            ViewBag.Role = CurrentRole;
            return View(item);
        }

        [HttpPost]
        public ActionResult UpdateInventory(int id, int quantity)
        {
            var item = db.InventoryItems.Find(id);
            if (item == null) return Json(new { success = false, message = "Not found" });

            var oldQty = item.CurrentStock;
            item.CurrentStock = quantity;
            db.SaveChanges();

            Audit.LogStockAdjustment(id, oldQty, quantity, "Manual update");
            return Json(new { success = true });
        }

        // ══════════════════════════════════════════════════════════════
        // STOCK ORDERS
        // ══════════════════════════════════════════════════════════════
        public ActionResult StockOrders()
        {
            ViewBag.Role = CurrentRole;
            return View(db.StockOrders
                .Include("Lines.InventoryItem")
                .OrderByDescending(o => o.RequestedAt).ToList());
        }

        public ActionResult PlaceStockOrder()
        {
            var cats = new[] { "Food", "Beverage", "Kitchen Supply" };
            ViewBag.LowStockItems = db.InventoryItems
                .Where(i => i.IsActive && cats.Contains(i.Category)).ToList()
                .Where(i => i.CurrentStock <= i.ReorderLevel).ToList();
            ViewBag.AllItems = db.InventoryItems
                .Where(i => i.IsActive && cats.Contains(i.Category))
                .OrderBy(i => i.Name).ToList();
            ViewBag.Role = CurrentRole;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceStockOrder(FormCollection form)
        {
            var order = new StockOrder
            {
                OrderNumber = "SO-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                RequestedAt = DateTime.Now,
                Status = StockOrderStatus.Requested,
                RequestedByUserId = CurrentUserId,
                Priority = StockOrderPriority.Normal,
                Notes = form["Notes"] ?? ""
            };
            db.StockOrders.Add(order);
            db.SaveChanges();

            // Parse item_<id> = qty
            foreach (string key in form.AllKeys)
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith("qty_")) continue;
                int invId;
                if (!int.TryParse(key.Substring(4), out invId)) continue;
                decimal qty;
                if (!decimal.TryParse(form[key], out qty) || qty <= 0) continue;

                var inv = db.InventoryItems.Find(invId);
                if (inv == null) continue;

                db.StockOrderLines.Add(new StockOrderLine
                {
                    StockOrderId = order.Id,
                    InventoryItemId = invId,
                    Quantity = qty,
                    Unit = inv.Unit
                });
            }
            db.SaveChanges();

            Audit.Log("StockOrderPlaced", "StockOrder", order.Id, null, order.OrderNumber);
            TempData["Success"] = "Order placed.";
            return RedirectToAction("StockOrders");
        }

        public ActionResult AmendStockOrder(int id)
        {
            var order = db.StockOrders.Include("Lines.InventoryItem").FirstOrDefault(o => o.Id == id);
            if (order == null) return HttpNotFound();
            ViewBag.Role = CurrentRole;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AmendStockOrder(int id, string notes)
        {
            var order = db.StockOrders.Find(id);
            if (order == null) return HttpNotFound();

            order.Notes = (order.Notes ?? "") + " | Amended: " + (notes ?? "");
            db.SaveChanges();

            Audit.Log("StockOrderAmended", "StockOrder", id, null, notes);
            TempData["Success"] = "Order amended.";
            return RedirectToAction("StockOrders");
        }

        public ActionResult RecordDelivery(int id)
        {
            var order = db.StockOrders.Include("Lines.InventoryItem").FirstOrDefault(o => o.Id == id);
            if (order == null) return HttpNotFound();
            ViewBag.Role = CurrentRole;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RecordDelivery(int id, string notes)
        {
            var order = db.StockOrders.Include("Lines.InventoryItem").FirstOrDefault(o => o.Id == id);
            if (order == null) return HttpNotFound();

            foreach (var line in order.Lines)
            {
                var inv = db.InventoryItems.Find(line.InventoryItemId);
                if (inv != null)
                {
                    var old = inv.CurrentStock;
                    inv.CurrentStock += line.Quantity;
                    line.ReceivedQuantity = line.Quantity;
                    Audit.LogStockAdjustment(inv.Id, old, inv.CurrentStock, "Delivery for " + order.OrderNumber);
                }
            }

            order.Status = StockOrderStatus.Received;
            order.Notes = (order.Notes ?? "") + " | Received: " + (notes ?? "");
            db.SaveChanges();

            Audit.Log("StockReceived", "StockOrder", id, "Requested", "Received");
            TempData["Success"] = "Delivery recorded and stock updated.";
            return RedirectToAction("StockOrders");
        }

        // ══════════════════════════════════════════════════════════════
        // EVENTS
        // ══════════════════════════════════════════════════════════════
        public ActionResult Events()
        {
            ViewBag.Role = CurrentRole;
            return View(db.Events.OrderByDescending(e => e.Date).ToList());
        }

        public ActionResult CreateEvent() { ViewBag.Role = CurrentRole; return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateEvent(Event eventModel)
        {
            if (ModelState.IsValid)
            {
                eventModel.CreatedDate = DateTime.Now;
                eventModel.Status = "Planned";
                db.Events.Add(eventModel);
                db.SaveChanges();

                Audit.Log("EventCreated", "Event", eventModel.EventID, null, eventModel.Name);
                TempData["Success"] = "Event created.";
                return RedirectToAction("Events");
            }
            ViewBag.Role = CurrentRole;
            return View(eventModel);
        }

        public ActionResult ViewRSVPs(int id)
        {
            var ev = db.Events.Find(id);
            if (ev == null) return HttpNotFound();

            var rsvps = db.RSVPs.Where(r => r.EventID == id).ToList();
            var sids = rsvps.Select(r => r.StudentID).Distinct().ToList();

            ViewBag.Students = db.Students.Where(s => sids.Contains(s.StudentId)).ToList();
            ViewBag.Event = ev;
            ViewBag.Role = CurrentRole;
            return View(rsvps);
        }

        public ActionResult FeastPlan(int id)
        {
            var ev = db.Events.Find(id);
            if (ev == null) return HttpNotFound();

            var rsvps = db.RSVPs.Where(r => r.EventID == id && r.Attending == true).ToList();
            var dc = new Dictionary<string, int>();
            foreach (var r in rsvps)
            {
                if (!string.IsNullOrEmpty(r.DietaryNeeds))
                {
                    foreach (var need in r.DietaryNeeds
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(n => n.Trim()))
                    {
                        if (!string.IsNullOrEmpty(need))
                        {
                            if (dc.ContainsKey(need)) dc[need]++;
                            else dc[need] = 1;
                        }
                    }
                }
            }

            ViewBag.TotalAttendees = rsvps.Sum(r => r.NumberOfPeople);
            ViewBag.DietaryCounts = dc;
            ViewBag.Event = ev;
            ViewBag.Role = CurrentRole;
            return View();
        }

        public ActionResult EventsForRSVP()
        {
            var today = DateTime.Now.Date;
            var events = db.Events
                .Where(e => e.Date >= today && e.Status != "Cancelled")
                .OrderBy(e => e.Date).ToList();

            ViewBag.MyRSVPs = db.RSVPs.Where(r => r.StudentID == CurrentStudentId).ToList();
            ViewBag.Role = CurrentRole;
            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitRSVP(int eventId, int numberOfPeople, string dietaryNeeds)
        {
            int sid = CurrentStudentId;
            if (sid == 0) return Json(new { success = false, message = "Not authenticated" });

            var existing = db.RSVPs.FirstOrDefault(r => r.EventID == eventId && r.StudentID == sid);
            if (existing != null)
            {
                existing.Attending = true;
                existing.NumberOfPeople = numberOfPeople;
                existing.DietaryNeeds = dietaryNeeds;
                existing.RSVPDate = DateTime.Now;
            }
            else
            {
                db.RSVPs.Add(new RSVP
                {
                    EventID = eventId,
                    StudentID = sid,
                    Attending = true,
                    NumberOfPeople = numberOfPeople,
                    DietaryNeeds = dietaryNeeds,
                    RSVPDate = DateTime.Now
                });
            }
            db.SaveChanges();
            return Json(new { success = true, message = "RSVP confirmed" });
        }

        // ══════════════════════════════════════════════════════════════
        // COACHES (Sports Director)
        // ══════════════════════════════════════════════════════════════
        public ActionResult Coaches()
        {
            ViewBag.Role = CurrentRole;
            return View(db.Coaches.Include("Sport").OrderBy(c => c.Surname).ToList());
        }

        public ActionResult AddCoach() { ViewBag.Role = CurrentRole; return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddCoach(Coach coach)
        {
            if (ModelState.IsValid)
            {
                coach.CreatedDate = DateTime.Now;
                coach.IsActive = true;
                db.Coaches.Add(coach);
                db.SaveChanges();

                Audit.Log("CoachAdded", "Coach", coach.CoachID, null, coach.Name + " " + coach.Surname);
                TempData["Success"] = "Coach added.";
                return RedirectToAction("Coaches");
            }
            ViewBag.Role = CurrentRole;
            return View(coach);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    
            // ══════════════════════════════════════════════════════════════
        // REPORTS
        // ══════════════════════════════════════════════════════════════
        public ActionResult Reports(DateTime? date = null)
        {
            var targetDate = (date ?? DateTime.Now.Date).Date;

            // ─── Daily Meal Report ────────────────────────────────
            var publishedMenu = db.WeeklyMenus
                .Include("Days.Meals.MealSlot")
                .OrderByDescending(w => w.StartDate)
                .FirstOrDefault(w => w.Status == "Published"
                                  && w.StartDate <= targetDate
                                  && w.EndDate >= targetDate);

            var dailyRows = new List<DailyMealReportRow>();

            if (publishedMenu != null)
            {
                var day = publishedMenu.Days.FirstOrDefault(d => d.Date.Date == targetDate);

                if (day != null)
                {
                    var forecastSvc = new DemandForecastService(db);

                    foreach (var meal in day.Meals
                        .OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99))
                    {
                        var slotName = meal.MealSlot != null ? meal.MealSlot.Name : "";

                        int expected = 0;
                        try { expected = forecastSvc.ForecastForMeal(meal.Id).ForecastTotal; } catch { }

                        var selections = db.Selections
                            .Where(s => s.Date == targetDate && s.MealType == slotName).ToList();

                        int selected = selections.Count;
                        int served = selections.Count(s => s.IsCollected);
                        int notCollected = selected - served;

                        dailyRows.Add(new DailyMealReportRow
                        {
                            SlotName = slotName,
                            Expected = expected,
                            Selected = selected,
                            Served = served,
                            NotCollected = notCollected
                        });
                    }
                }
            }

            // ─── Dining Hall Report ───────────────────────────────
            var halls = db.DiningHalls.Where(h => h.IsActive).OrderBy(h => h.Name).ToList();
            var hallRows = new List<DiningHallReportRow>();
            foreach (var h in halls)
            {
                hallRows.Add(new DiningHallReportRow
                {
                    HallName = h.Name,
                    Served = 0 // per-hall tracking arrives in Phase 11; placeholder for now
                });
            }

            // ─── Dietary Summary ──────────────────────────────────
            var dietaryRows = new List<DietarySummaryRow>();
            try
            {
                dietaryRows.Add(new DietarySummaryRow
                {
                    Category = "Allergies",
                    Count = db.StudentDietaryRecords
                        .Where(r => r.IsActive && r.Status == DietaryRequestStatus.Approved
                                && r.RecordType == DietaryRecordType.Allergy)
                        .Select(r => r.StudentId).Distinct().Count()
                });
                dietaryRows.Add(new DietarySummaryRow
                {
                    Category = "Medical Restrictions",
                    Count = db.StudentDietaryRecords
                        .Where(r => r.IsActive && r.Status == DietaryRequestStatus.Approved
                                && r.RecordType == DietaryRecordType.MedicalRestriction)
                        .Select(r => r.StudentId).Distinct().Count()
                });
                dietaryRows.Add(new DietarySummaryRow
                {
                    Category = "Dietary Preferences",
                    Count = db.StudentDietaryRecords
                        .Where(r => r.IsActive && r.Status == DietaryRequestStatus.Approved
                                && r.RecordType == DietaryRecordType.DietaryPreference)
                        .Select(r => r.StudentId).Distinct().Count()
                });
                dietaryRows.Add(new DietarySummaryRow
                {
                    Category = "Religious / Cultural",
                    Count = db.StudentDietaryRecords
                        .Where(r => r.IsActive && r.Status == DietaryRequestStatus.Approved
                                && r.RecordType == DietaryRecordType.ReligiousCultural)
                        .Select(r => r.StudentId).Distinct().Count()
                });
            }
            catch { }

            // ─── Wastage (last 30 days) ───────────────────────────
            var thirtyDaysAgo = targetDate.AddDays(-30);
            var wasteRecords = db.WasteRecords
                .Where(w => w.RecordedDate >= thirtyDaysAgo)
                .ToList();

            var wasteRows = wasteRecords
                .GroupBy(w => w.Reason.ToString())
                .Select(g => new WasteSummaryRow
                {
                    Reason = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    Records = g.Count()
                })
                .OrderByDescending(x => x.TotalQuantity)
                .ToList();

            // ─── Production Summary (today) ───────────────────────
            var prodRows = new List<ProductionSummaryRow>();
            if (publishedMenu != null)
            {
                var day = publishedMenu.Days.FirstOrDefault(d => d.Date.Date == targetDate);
                if (day != null)
                {
                    foreach (var meal in day.Meals
                        .OrderBy(m => m.MealSlot != null ? m.MealSlot.DisplayOrder : 99))
                    {
                        var slotName = meal.MealSlot != null ? meal.MealSlot.Name : "";
                        var prod = db.ProductionRecords
                            .FirstOrDefault(p => p.MenuMealId == meal.Id && p.ProductionDate == targetDate);

                        var served = db.Selections
                            .Count(s => s.Date == targetDate && s.MealType == slotName && s.IsCollected);

                        int producedQty = prod != null ? prod.ProducedQuantity : 0;
                        int targetQty = prod != null ? prod.TargetQuantity : 0;

                        prodRows.Add(new ProductionSummaryRow
                        {
                            SlotName = slotName,
                            Target = targetQty,
                            Produced = producedQty,
                            Served = served,
                            Leftover = Math.Max(0, producedQty - served)
                        });
                    }
                }
            }

            ViewBag.Date = targetDate;
            ViewBag.DailyRows = dailyRows;
            ViewBag.HallRows = hallRows;
            ViewBag.DietaryRows = dietaryRows;
            ViewBag.WasteRows = wasteRows;
            ViewBag.ProductionRows = prodRows;
            ViewBag.PublishedMenu = publishedMenu;
            ViewBag.Role = CurrentRole;
            return View();
        }

        public ActionResult MenuHistory()
        {
            var menus = db.WeeklyMenus
                .OrderByDescending(w => w.StartDate)
                .Take(20).ToList();

            ViewBag.Role = CurrentRole;
            return View(menus);
        }

        public ActionResult AuditLog()
        {
            var logs = db.CafeteriaAuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(200).ToList();

            ViewBag.Role = CurrentRole;
            return View(logs);
        }

        public ActionResult Settings()
        {
            var settings = db.CafeteriaSettings.OrderBy(s => s.Key).ToList();
            ViewBag.Role = CurrentRole;
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveSetting(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                TempData["Error"] = "Setting key required.";
                return RedirectToAction("Settings");
            }

            var existing = db.CafeteriaSettings.FirstOrDefault(s => s.Key == key);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                db.CafeteriaSettings.Add(new CafeteriaSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.Now
                });
            }
            db.SaveChanges();

            Audit.Log("SettingChanged", "CafeteriaSetting", 0, null, key + "=" + value);
            TempData["Success"] = "Setting saved.";
            return RedirectToAction("Settings");
        }
    }
}

