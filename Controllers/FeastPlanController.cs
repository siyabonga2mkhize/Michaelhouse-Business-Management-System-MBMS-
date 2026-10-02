using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    // ============================================================
    // UC19 — Generate Feast Plan
    //
    // One screen per SRS step:
    //   1  Rsvps/{eventId}          RSVP responses retrieved
    //   2  EventDetails/{eventId}   combined with the event details
    //   3  Calculate/{eventId}      dish quantities calculated
    //   4  Generate (POST)          proposed plan generated and saved
    //   5  Review/{planId}          Meal Coordinator reviews
    //   6  Decision/{planId}        Cafeteria Manager decides
    //   7  Edit/{planId}            edit + recalculate → back to 5
    //   8  Approved/{planId}        approved, locked and sent
    //
    // Login and roles come from Session (UserId / UserRole /
    // UserName). Role names: FeastPlanRoles.
    // ============================================================
    public class FeastPlanController : Controller
    {
        private readonly DBContextClass _db;
        private readonly FeastPlanService _plans;

        public FeastPlanController()
        {
            _db = new DBContextClass();
            _plans = new FeastPlanService(_db);
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

            return View(_plans.ReadSource(_plans.FindEvent(id)));
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
            return View(source);
        }

        // ============================================================
        // STEP 3 — GET: FeastPlan/Calculate/5?buffer=5
        // Calculated for review; nothing is saved until step 4.
        // ============================================================

        [HttpGet]
        public ActionResult Calculate(int id, string buffer)
        {
            ActionResult blocked = StartGuard(id);
            if (blocked != null) return blocked;

            decimal bufferPercent;
            if (!TryParseBuffer(buffer, out bufferPercent) || !FeastPlanService.IsValidBuffer(bufferPercent))
            {
                TempData["Error"] = "The buffer must be between 0% and 100%.";
                return RedirectToAction("EventDetails", new { id });
            }

            var source = _plans.ReadSource(_plans.FindEvent(id));
            if (!source.CanGenerate)
            {
                // Step 1 shows why
                return RedirectToAction("Rsvps", new { id });
            }

            FeastPlanService.Calculate(source.TotalGuests, source.VegetarianGuests, source.HalalGuests, bufferPercent, source.Dishes);

            ViewBag.Buffer = bufferPercent;
            ViewBag.Check = FeastPlanService.Check(source.TotalGuests, source.VegetarianGuests, source.HalalGuests, source.Dishes);
            return View(source);
        }

        // ============================================================
        // STEP 4 — POST: FeastPlan/Generate
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
                    : RedirectToAction("Rsvps", new { id });
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Review", new { id = result.PlanId });
        }

        // ============================================================
        // STEP 5 — GET: FeastPlan/Review/5 (plan id)
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
            return View(plan);
        }

        // POST: FeastPlan/Submit — step 5 → step 6
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
        // STEP 6 — GET/POST: FeastPlan/Decision/5 (Cafeteria Manager)
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
        // STEP 7 — GET/POST: FeastPlan/Edit/5 — edit and recalculate
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

            // Back to step 5
            TempData["Success"] = result.Message;
            return RedirectToAction("Review", new { id = input.Id });
        }

        // ============================================================
        // STEP 8 — GET: FeastPlan/Approved/5 — the locked plan the
        // Cafeteria Manager and Kitchen Staff receive
        // ============================================================

        [HttpGet]
        public ActionResult Approved(int id)
        {
            if (!FeastPlanRoles.CanViewApproved(CurrentRole())) return Forbidden();

            var plan = _plans.FindPlan(id);
            if (plan == null || (plan.Status != FeastPlanStatus.Approved && !FeastPlanRoles.CanCoordinate(CurrentRole()) && !FeastPlanRoles.CanApprove(CurrentRole())))
            {
                return HttpNotFound();
            }

            if (plan.Status != FeastPlanStatus.Approved)
            {
                return RedirectToAction("Review", new { id });
            }

            return View(plan);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // Steps 1–3: coordinators only, and not when the event
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
