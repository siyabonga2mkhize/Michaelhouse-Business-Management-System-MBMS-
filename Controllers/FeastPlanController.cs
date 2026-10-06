using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // UC19 — Generate Feast Plan (SRS v8, 10 steps)
    //
    // One screen per SRS step:
    //   1  Rsvps/{eventId}          collect the RSVP responses
    //   2  EventDetails/{eventId}   combine them with the event details
    //   3  Favourites/{planId}      cultural favourites vs projected stock;
    //                               the coordinator selects the available ones
    //   4  Quantities/{planId}      assortment and quantities
    //   5  Fatigue/{planId}         Menu Fatigue Score, flags, replacements
    //   6  Balance/{planId}         colour / texture balance, swaps
    //   7  Waste/{planId}           maximum consumption, less waste
    //   8  Review/{planId}          Meal Coordinator / Cafeteria Manager review
    //   9  Decision/{planId}        Cafeteria Manager decides (Edit changes it)
    //  10  Approved/{planId}        saved and sent; Leftovers/{planId} after the event
    //
    // Support screens: Tags (colour and texture of each meal).
    //
    // Login and roles come from Session (UserId / UserRole /
    // UserName). Role names: FeastPlanRoles.
    // ============================================================
    public class FeastPlanController : Controller
    {
        private readonly DBContextClass _db;
        private readonly FeastPlanService _plans;
        private readonly FeastAnalysisService _analysis;

        public FeastPlanController()
        {
            _db = new DBContextClass();
            _plans = new FeastPlanService(_db);
            _analysis = new FeastAnalysisService(_db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        // ============================================================
        // GET: FeastPlan — plans, and events ready for one
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            var role = CurrentRole();
            if (!FeastPlanRoles.CanViewApproved(role)) return Forbidden();

            ViewBag.CanCoordinate = FeastPlanRoles.CanCoordinate(role);
            ViewBag.CanApprove = FeastPlanRoles.CanApprove(role);
            ViewBag.CanRecordLeftovers = FeastPlanRoles.CanRecordLeftovers(role);
            ViewBag.ReadyEvents = FeastPlanRoles.CanCoordinate(role)
                ? _plans.EventsReadyForPlan()
                : new List<CafeteriaEvent>();

            return View(_plans.PlansFor(role));
        }

        // ============================================================
        // STEP 1 — GET: FeastPlan/Rsvps/5 (event id)
        // ============================================================

        [HttpGet]
        public ActionResult Rsvps(int id)
        {
            ActionResult blocked = StartGuard(id);
            if (blocked != null) return blocked;

            var source = _plans.ReadSource(_plans.FindEvent(id));
            ViewBag.Favourites = _analysis.FavouriteRequests(id);
            return View(source);
        }

        // ============================================================
        // STEP 2 — GET: FeastPlan/EventDetails/5?buffer=5
        // ============================================================

        [HttpGet]
        public ActionResult EventDetails(int id, string buffer)
        {
            ActionResult blocked = StartGuard(id);
            if (blocked != null) return blocked;

            var source = _plans.ReadSource(_plans.FindEvent(id));
            if (!source.CanGenerate)
            {
                // Step 1 shows why
                return RedirectToAction("Rsvps", new { id });
            }

            decimal bufferPercent;
            if (!TryParseBuffer(buffer, out bufferPercent) || !FeastPlanService.IsValidBuffer(bufferPercent))
            {
                bufferPercent = EventFeastPlan.DefaultBufferPercent;
            }

            ViewBag.Buffer = bufferPercent;
            ViewBag.Prerequisites = _analysis.Prerequisites(source.Event, source.TotalGuests, source.Dishes);
            return View(source);
        }

        // ============================================================
        // POST: FeastPlan/Generate — starts the plan (saved as Drafting)
        // and moves on to step 3
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Generate(int id, string buffer)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            decimal bufferPercent;
            if (!TryParseBuffer(buffer, out bufferPercent))
            {
                TempData["Error"] = "The buffer must be a number between 0 and 100.";
                return RedirectToAction("EventDetails", new { id });
            }

            var result = _plans.Generate(id, bufferPercent, CurrentUser());
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return result.PlanId > 0
                    ? RedirectToAction("Review", new { id = result.PlanId })
                    : RedirectToAction("EventDetails", new { id, buffer });
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Favourites", new { id = result.PlanId });
        }

        // ============================================================
        // STEP 3 — GET/POST: FeastPlan/Favourites/5 (plan id)
        // ============================================================

        [HttpGet]
        public ActionResult Favourites(int id)
        {
            ActionResult blocked;
            var plan = StepPlan(id, FeastStage.Favourites, out blocked);
            if (blocked != null) return blocked;

            ViewBag.Rows = _analysis.ReadFavourites(plan);
            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Favourites(int id, int[] selected)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var result = _plans.SaveFavourites(id, selected ?? new int[0], CurrentUser());
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Favourites", new { id });
            }

            if (!string.IsNullOrWhiteSpace(result.Message)) TempData["Success"] = result.Message;
            return RedirectToAction("Quantities", new { id });
        }

        // ============================================================
        // STEP 4 — GET: FeastPlan/Quantities/5
        // ============================================================

        [HttpGet]
        public ActionResult Quantities(int id)
        {
            ActionResult blocked;
            var plan = StepPlan(id, FeastStage.Quantities, out blocked);
            if (blocked != null) return blocked;

            ViewBag.Check = FeastPlanService.Check(plan);
            return View(plan);
        }

        // ============================================================
        // STEP 5 — GET: FeastPlan/Fatigue/5
        // ============================================================

        [HttpGet]
        public ActionResult Fatigue(int id)
        {
            ActionResult blocked;
            var plan = StepPlan(id, FeastStage.Fatigue, out blocked);
            if (blocked != null) return blocked;

            ViewBag.Rows = _analysis.Fatigue(plan);
            ViewBag.HasDemo = _plans.HasDemoHistory();
            return View(plan);
        }

        // ============================================================
        // STEP 6 — GET: FeastPlan/Balance/5
        // ============================================================

        [HttpGet]
        public ActionResult Balance(int id)
        {
            ActionResult blocked;
            var plan = StepPlan(id, FeastStage.Balance, out blocked);
            if (blocked != null) return blocked;

            ViewBag.Result = _analysis.Balance(plan);
            return View(plan);
        }

        // POST: accept a suggested swap (step 5 or 6) — recalculates
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptSwap(int id, int itemId, int menuItemId, string kind)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var result = _plans.ApplySwap(id, itemId, menuItemId, kind, CurrentUser());
            TempData[result.Success ? "Success" : "Error"] = result.Message;

            return RedirectToAction(string.Equals(kind, "Balance", System.StringComparison.OrdinalIgnoreCase) ? "Balance" : "Fatigue", new { id });
        }

        // POST: sample leftover history, for showing steps 5 and 7
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DemoHistory(int id, string mode, string returnTo)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var result = mode == "remove"
                ? _plans.RemoveDemoHistory(id, CurrentUser())
                : _plans.AddDemoHistory(id, CurrentUser());

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(returnTo == "Waste" ? "Waste" : "Fatigue", new { id });
        }

        // ============================================================
        // STEP 7 — GET: FeastPlan/Waste/5, POST: FeastPlan/OptimiseWaste
        // ============================================================

        [HttpGet]
        public ActionResult Waste(int id)
        {
            ActionResult blocked;
            var plan = StepPlan(id, FeastStage.Waste, out blocked);
            if (blocked != null) return blocked;

            ViewBag.Rows = _analysis.Waste(plan);
            ViewBag.HasRun = plan.History.Any(h => h.Action == FeastPlanService.ActionWasteOptimised && h.Revision == plan.Revision);
            ViewBag.HasDemo = _plans.HasDemoHistory();
            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OptimiseWaste(int id)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var result = _plans.OptimiseWaste(id, CurrentUser());
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Waste", new { id });
        }

        // POST: the coordinator confirms a step is done and moves on
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Advance(int id, int fromStep)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var result = _plans.Advance(id, fromStep, CurrentUser());
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(ActionForStep(fromStep), new { id });
            }

            return RedirectToAction(ActionForStep(fromStep + 1), new { id });
        }

        // ============================================================
        // STEP 8 — GET: FeastPlan/Review/5 (plan id)
        // ============================================================

        [HttpGet]
        public ActionResult Review(int id)
        {
            var role = CurrentRole();
            var plan = _plans.FindPlan(id);
            if (plan == null) return HttpNotFound();

            if (plan.Status == FeastPlanStatus.Approved)
            {
                return RedirectToAction("Approved", new { id });
            }

            if (!FeastPlanRoles.CanCoordinate(role) && !FeastPlanRoles.CanApprove(role)) return Forbidden();

            ViewBag.CanCoordinate = FeastPlanRoles.CanCoordinate(role);
            ViewBag.CanApprove = FeastPlanRoles.CanApprove(role);
            ViewBag.Check = FeastPlanService.Check(plan);
            ViewBag.Summary = _analysis.Summarise(plan);
            return View(plan);
        }

        // POST: FeastPlan/Submit — step 8 → step 9
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(int id, string rowVersion, string notes)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var result = _plans.Submit(id, FeastPlanService.ParseRowVersion(rowVersion), notes, CurrentUser(), PlanLink);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Review", new { id });
        }

        // ============================================================
        // STEP 9 — GET/POST: FeastPlan/Decision/5 (Cafeteria Manager)
        // ============================================================

        [HttpGet]
        public ActionResult Decision(int id)
        {
            if (!FeastPlanRoles.CanApprove(CurrentRole())) return Forbidden();

            var plan = _plans.FindPlan(id);
            if (plan == null) return HttpNotFound();

            if (plan.Status != FeastPlanStatus.AwaitingApproval)
            {
                TempData["Error"] = "This plan isn't waiting for approval (" + FeastPlanService.StatusLabel(plan.Status).ToLowerInvariant() + ").";
                return RedirectToAction("Review", new { id });
            }

            ViewBag.Check = FeastPlanService.Check(plan);
            ViewBag.Summary = _analysis.Summarise(plan);
            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Decision(int id, string decision, string comments, string rowVersion)
        {
            if (!FeastPlanRoles.CanApprove(CurrentRole())) return Forbidden();

            var result = _plans.Decide(id, decision, comments, FeastPlanService.ParseRowVersion(rowVersion), CurrentUser(), PlanLink);
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Decision", new { id });
            }

            TempData["Success"] = result.Message;
            return decision == FeastPlanService.DecisionApprove
                ? RedirectToAction("Approved", new { id })
                : RedirectToAction("Review", new { id });
        }

        // ============================================================
        // STEP 9 (changes) — GET/POST: FeastPlan/Edit/5 — edit and
        // recalculate, then the review (step 8) repeats
        // ============================================================

        [HttpGet]
        public ActionResult Edit(int id)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var plan = _plans.FindPlan(id);
            if (plan == null) return HttpNotFound();

            if (!plan.IsEditable)
            {
                TempData["Error"] = plan.IsLocked
                    ? "This plan has been approved and is locked."
                    : "This plan can't be edited while it is " + FeastPlanService.StatusLabel(plan.Status).ToLowerInvariant() + ".";
                return RedirectToAction("Review", new { id });
            }

            ViewBag.Meals = _plans.LibraryMeals();
            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FeastPlanEditInput input)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();
            if (input == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            // Read the buffer ourselves so "7.5" and "7,5" both work
            // whatever the server's culture
            decimal bufferPercent;
            if (!TryParseBuffer(Request.Form["BufferPercent"], out bufferPercent))
            {
                TempData["Error"] = "The buffer must be a number between 0 and 100.";
                return RedirectToAction("Edit", new { id = input.Id });
            }
            input.BufferPercent = bufferPercent;

            var result = _plans.Update(input, CurrentUser());
            if (!result.Success)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("Edit", new { id = input.Id });
            }

            // Back to the review
            TempData["Success"] = result.Message;
            return RedirectToAction("Review", new { id = input.Id });
        }

        // ============================================================
        // STEP 10 — GET: FeastPlan/Approved/5 — the locked plan the
        // Cafeteria Manager and Kitchen Staff receive
        // ============================================================

        [HttpGet]
        public ActionResult Approved(int id)
        {
            var role = CurrentRole();
            if (!FeastPlanRoles.CanViewApproved(role)) return Forbidden();

            var plan = _plans.FindPlan(id);
            if (plan == null) return HttpNotFound();

            if (plan.Status != FeastPlanStatus.Approved)
            {
                if (!FeastPlanRoles.CanCoordinate(role) && !FeastPlanRoles.CanApprove(role)) return HttpNotFound();
                return RedirectToAction("Review", new { id });
            }

            ViewBag.CanRecordLeftovers = FeastPlanRoles.CanRecordLeftovers(role);
            ViewBag.Leftovers = _plans.LeftoversFor(plan.Id);
            return View(plan);
        }

        // ============================================================
        // STEP 10 — after the event the kitchen records leftovers per
        // dish. They feed future Menu Fatigue Scores.
        // ============================================================

        [HttpGet]
        public ActionResult Leftovers(int id)
        {
            if (!FeastPlanRoles.CanRecordLeftovers(CurrentRole())) return Forbidden();

            var plan = _plans.FindPlan(id);
            if (plan == null) return HttpNotFound();

            if (plan.Status != FeastPlanStatus.Approved)
            {
                TempData["Error"] = "Leftovers are recorded for an approved feast plan.";
                return RedirectToAction("Index");
            }

            ViewBag.Leftovers = _plans.LeftoversFor(plan.Id);
            ViewBag.EventOver = plan.Event.EventDate.Date <= SchoolClock.Today;
            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Leftovers(int id, FormCollection form)
        {
            if (!FeastPlanRoles.CanRecordLeftovers(CurrentRole())) return Forbidden();

            var values = new Dictionary<int, int>();
            foreach (var key in form.AllKeys.Where(k => k != null && k.StartsWith("left_")))
            {
                int itemId, left;
                if (int.TryParse(key.Substring(5), out itemId) && int.TryParse((form[key] ?? "").Trim(), out left))
                {
                    values[itemId] = left;
                }
            }

            var result = _plans.SaveLeftovers(id, values, CurrentUser());
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Leftovers", new { id });
        }

        // ============================================================
        // Colour and texture tags (SRS: meals carry colour and texture tags)
        // ============================================================

        [HttpGet]
        public ActionResult Tags()
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            return View(_analysis.TagTable());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Tags(FormCollection form)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            var chosen = new Dictionary<int, KeyValuePair<string, string>>();
            foreach (var key in form.AllKeys.Where(k => k != null && k.StartsWith("colour_")))
            {
                int mealId;
                if (!int.TryParse(key.Substring(7), out mealId)) continue;

                chosen[mealId] = new KeyValuePair<string, string>(form[key], form["texture_" + mealId]);
            }

            int changed = _analysis.SaveTags(chosen);
            TempData["Success"] = changed == 0 ? "No tags were changed." : changed + " meal tag" + (changed == 1 ? "" : "s") + " saved.";
            return RedirectToAction("Tags");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StarterTags()
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            int added = _analysis.ApplyStarterTags();
            TempData["Success"] = added == 0
                ? "Every active meal already has a colour and texture."
                : "Starter colours and textures added to " + added + " meal" + (added == 1 ? "" : "s") + ". Check them and change any that are wrong.";
            return RedirectToAction("Tags");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // Steps 1–2: coordinators only, and not when the event
        // already has a live plan (one per event)
        private ActionResult StartGuard(int eventId)
        {
            if (!FeastPlanRoles.CanCoordinate(CurrentRole())) return Forbidden();

            if (_plans.FindEvent(eventId) == null) return HttpNotFound();

            var live = _plans.LivePlanFor(eventId);
            if (live != null)
            {
                TempData["Error"] = "This event already has a feast plan. Only one live plan is allowed per event.";
                return RedirectToAction(live.Status == FeastPlanStatus.Approved ? "Approved" : "Review", new { id = live.Id });
            }

            return null;
        }

        // Steps 3–7: the coordinator, on a plan that is still being drafted,
        // and not ahead of the step the plan has reached
        private EventFeastPlan StepPlan(int planId, int step, out ActionResult blocked)
        {
            blocked = null;

            if (!FeastPlanRoles.CanCoordinate(CurrentRole()))
            {
                blocked = Forbidden();
                return null;
            }

            var plan = _plans.FindPlan(planId);
            if (plan == null)
            {
                blocked = HttpNotFound();
                return null;
            }

            if (plan.Status == FeastPlanStatus.Approved)
            {
                blocked = RedirectToAction("Approved", new { id = plan.Id });
                return null;
            }

            if (plan.Status == FeastPlanStatus.Drafting && step > plan.Stage)
            {
                TempData["Error"] = "Finish step " + plan.Stage + " first.";
                blocked = RedirectToAction(ActionForStep(plan.Stage), new { id = plan.Id });
                return null;
            }

            ViewBag.CanEdit = plan.IsEditable;
            return plan;
        }

        private static string ActionForStep(int step)
        {
            switch (step)
            {
                case FeastStage.Favourites: return "Favourites";
                case FeastStage.Quantities: return "Quantities";
                case FeastStage.Fatigue: return "Fatigue";
                case FeastStage.Balance: return "Balance";
                case FeastStage.Waste: return "Waste";
                default: return "Review";
            }
        }

        // Empty means the default buffer
        private static bool TryParseBuffer(string text, out decimal value)
        {
            value = EventFeastPlan.DefaultBufferPercent;
            if (string.IsNullOrWhiteSpace(text)) return true;

            return decimal.TryParse(
                text.Trim().Replace(',', '.'),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out value);
        }

        private string CurrentRole()
        {
            return Session["UserRole"] != null ? Session["UserRole"].ToString() : "";
        }

        private FeastPlanUser CurrentUser()
        {
            int id = 0;
            if (Session["UserId"] != null) int.TryParse(Session["UserId"].ToString(), out id);

            return new FeastPlanUser
            {
                UserId = id,
                Name = Session["UserName"] != null ? Session["UserName"].ToString() : "",
                Role = CurrentRole()
            };
        }

        // Absolute link for notifications
        private string PlanLink(string action, int planId)
        {
            var scheme = Request != null && Request.Url != null ? Request.Url.Scheme : "https";
            return Url.Action(action, "FeastPlan", new { id = planId }, scheme);
        }

        private static ActionResult Forbidden()
        {
            return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "You don't have access to this part of the feast plan.");
        }
    }
}
