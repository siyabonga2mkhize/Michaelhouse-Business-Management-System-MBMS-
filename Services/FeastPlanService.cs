using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC19 — Generate Feast Plan
    //
    //   1  retrieve the event's RSVP responses
    //      (EventRsvpService.GetAttendees — one entry per person)
    //   2  combine them with the event details and its buffet
    //      (EventBuffetService.GetBuffetLines)
    //   3  calculate dish quantities
    //   4  generate the proposed plan (saved as Proposed)
    //   5  Meal Coordinator reviews
    //   6  Cafeteria Manager approves, rejects or requests changes
    //   7  on changes: edit, recalculate, back to step 5
    //   8  approved plan is saved, locked and sent to the Cafeteria
    //      Manager and Kitchen Staff
    //
    // Rules:
    //   • one portion per attending guest
    //   • Vegetarian (incl. vegan) and Halal counts come from the
    //     RSVPs; Standard is the rest
    //   • guests in a group are shared between that group's dishes,
    //     so each guest gets one portion; Common dishes (dessert,
    //     sides, drinks …) cover everyone
    //   • a buffer (default 5%) is added and rounded up
    //   • only the Cafeteria Manager approves; Approved is locked
    //   • an event with no attending RSVPs is refused
    //   • one live (not Rejected) plan per event
    //
    // Reads the team's CafeteriaEvent / EventRsvp data through their
    // services and doesn't change it.
    // ============================================================

    // Role names as stored in AppUser.Role / Session["UserRole"]
    public static class FeastPlanRoles
    {
        // There is no separate Meal Coordinator role: the
        // CafeteriaManager login is the one the app calls "Meal
        // Coordinator" (Views/Cafeteria/Dashboard.cshtml).
        public const string MealCoordinator = "CafeteriaManager";
        public const string CafeteriaManager = "CafeteriaManager";

        // Kitchen staff. There is no Caterer role.
        public const string KitchenStaff = "Chef";

        public const string Admin = "Admin";

        // Generate, edit, recalculate and submit (steps 1–5, 7)
        public static bool CanCoordinate(string role)
        {
            return role == MealCoordinator || role == Admin;
        }

        // Approve / reject / request changes (step 6)
        public static bool CanApprove(string role)
        {
            return role == CafeteriaManager;
        }

        // See approved plans (step 10)
        public static bool CanViewApproved(string role)
        {
            return CanCoordinate(role) || CanApprove(role) || role == KitchenStaff;
        }

        // Record leftovers after the event (step 10): the kitchen,
        // with the Cafeteria Manager and Admin able to help
        public static bool CanRecordLeftovers(string role)
        {
            return role == KitchenStaff || role == CafeteriaManager || role == Admin;
        }
    }

    public class FeastPlanUser
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
    }

    // One accepted RSVP (a person and anyone they bring)
    public class FeastPlanRsvpRow
    {
        public int RsvpId { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public int People { get; set; }
        public int Vegetarian { get; set; }
        public int Halal { get; set; }
        public int Standard { get; set; }
        public DateTime RespondedAt { get; set; }
    }

    // Steps 1–2: the RSVPs combined with the event and its buffet
    public class FeastPlanSource
    {
        public FeastPlanSource()
        {
            Rsvps = new List<FeastPlanRsvpRow>();
            Dishes = new List<EventFeastPlanItem>();
        }

        public CafeteriaEvent Event { get; set; }
        public List<FeastPlanRsvpRow> Rsvps { get; set; }
        public int DeclinedResponses { get; set; }

        public int TotalGuests { get; set; }
        public int VegetarianGuests { get; set; }
        public int VeganGuests { get; set; }
        public int HalalGuests { get; set; }
        public int OtherRequirementGuests { get; set; }

        public int StandardGuests
        {
            get { return Math.Max(0, TotalGuests - VegetarianGuests - HalalGuests); }
        }

        // The event's buffet as proposed plan lines (not saved)
        public List<EventFeastPlanItem> Dishes { get; set; }

        // Why a plan can't be generated; null when it can
        public string Refusal { get; set; }

        public bool CanGenerate
        {
            get { return Refusal == null; }
        }
    }

    // Problems block submission; notes are for information
    public class FeastPlanCheck
    {
        public FeastPlanCheck()
        {
            Problems = new List<string>();
            Notes = new List<string>();
        }

        public List<string> Problems { get; set; }
        public List<string> Notes { get; set; }
    }

    public class FeastPlanResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int PlanId { get; set; }

        public static FeastPlanResult Ok(int planId, string message)
        {
            return new FeastPlanResult { Success = true, PlanId = planId, Message = message };
        }

        public static FeastPlanResult Fail(string message)
        {
            return new FeastPlanResult { Success = false, Message = message };
        }
    }

    public partial class FeastPlanService
    {
        public const string ActionGenerated = "Generated";
        public const string ActionEdited = "Recalculated";
        public const string ActionSubmitted = "Submitted";
        public const string ActionApproved = "Approved";
        public const string ActionRejected = "Rejected";
        public const string ActionChangesRequested = "ChangesRequested";
        public const string ActionSent = "Sent";
        public const string ActionFavourites = "Favourites";
        public const string ActionStepDone = "StepDone";
        public const string ActionSwap = "Swap";
        public const string ActionWasteOptimised = "WasteOptimised";
        public const string ActionLeftovers = "Leftovers";
        public const string ActionDemoHistory = "DemoHistory";

        public const string DecisionApprove = "Approve";
        public const string DecisionReject = "Reject";
        public const string DecisionRequestChanges = "RequestChanges";

        public const decimal MinBufferPercent = 0m;
        public const decimal MaxBufferPercent = 100m;

        // Notification type, so these don't use the links the shared
        // notification list gives "FeastPlan" (Event/FeastPlan)
        private const string NotificationType = "EventFeastPlan";

        private readonly DBContextClass _db;
        private readonly EventRsvpService _rsvp;
        private readonly EventBuffetService _buffet;

        public FeastPlanService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _rsvp = new EventRsvpService(db);
            _buffet = new EventBuffetService(db, _rsvp);
        }

        // ============================================================
        // LOOKUPS
        // ============================================================

        public CafeteriaEvent FindEvent(int eventId)
        {
            return _db.CafeteriaEvents
                .Include("Venue")
                .Include("MenuTemplate")
                .FirstOrDefault(e => e.Id == eventId);
        }

        public EventFeastPlan FindPlan(int planId)
        {
            return _db.EventFeastPlans
                .Include("Event.Venue")
                .Include("Items")
                .Include("History")
                .FirstOrDefault(p => p.Id == planId);
        }

        // The event's live plan (anything not Rejected), or null
        public EventFeastPlan LivePlanFor(int eventId)
        {
            return _db.EventFeastPlans
                .Where(p => p.EventId == eventId && p.Status != FeastPlanStatus.Rejected)
                .OrderByDescending(p => p.Id)
                .FirstOrDefault();
        }

        // Plans the user may see. Kitchen staff see approved plans only.
        public List<EventFeastPlan> PlansFor(string role)
        {
            IQueryable<EventFeastPlan> query = _db.EventFeastPlans.Include("Event");

            if (!FeastPlanRoles.CanCoordinate(role) && !FeastPlanRoles.CanApprove(role))
            {
                query = query.Where(p => p.Status == FeastPlanStatus.Approved);
            }

            return query
                .OrderByDescending(p => p.UpdatedAt ?? p.GeneratedAt)
                .ToList();
        }

        // Events whose RSVPs are closed, still to come, without a live plan
        public List<CafeteriaEvent> EventsReadyForPlan()
        {
            var today = SchoolClock.Today;
            var planned = _db.EventFeastPlans
                .Where(p => p.Status != FeastPlanStatus.Rejected)
                .Select(p => p.EventId);

            return _db.CafeteriaEvents
                .Include("Venue")
                .Where(e => e.Status == EventStatus.RsvpClosed || e.Status == EventStatus.FeastPlanGenerated)
                .Where(e => e.EventDate >= today)
                .Where(e => !planned.Contains(e.Id))
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.StartTime)
                .ToList();
        }

        // Active meals in the library, for adding a dish (step 7)
        public List<MenuItem> LibraryMeals()
        {
            return _db.MenuItems
                .Where(m => m.IsActive)
                .OrderBy(m => m.Name)
                .ToList();
        }

        // ============================================================
        // STEPS 1–2 — RSVPs + event details
        // ============================================================

        // Why a plan can't be made for this event (null = it can)
        public static string EventRefusal(CafeteriaEvent evt)
        {
            if (evt == null) return "Event not found.";
            if (evt.Status == EventStatus.Cancelled) return "This event has been cancelled.";
            if (evt.Status == EventStatus.Completed) return "This event is already over.";

            if (evt.Status != EventStatus.RsvpClosed && evt.Status != EventStatus.FeastPlanGenerated)
            {
                return "RSVPs for this event aren't closed yet. Close RSVPs first so the headcount is final.";
            }

            if (evt.EventDate.Date < SchoolClock.Today) return "This event has already taken place.";

            return null;
        }

        public FeastPlanSource ReadSource(CafeteriaEvent evt)
        {
            var source = new FeastPlanSource { Event = evt, Refusal = EventRefusal(evt) };
            if (evt == null) return source;

            // Step 1 — everyone who accepted, one entry per person
            // (students' diets from their profile, parents / staff /
            // guests from the RSVP)
            var attendees = _rsvp.GetAttendees(evt.Id);
            var responses = _db.EventRsvps.Where(r => r.EventId == evt.Id).ToList();

            source.DeclinedResponses = responses.Count(r => r.ResponseStatus == EventRsvpService.ResponseDeclined);

            foreach (var r in responses
                         .Where(r => r.ResponseStatus == EventRsvpService.ResponseAttending)
                         .OrderBy(r => r.RespondedAt))
            {
                var party = attendees.Where(a => a.RsvpId == r.Id).ToList();
                int vegetarian = party.Count(a => IsVegetarian(a.Profile));
                int halal = party.Count(a => IsHalal(a.Profile));

                source.Rsvps.Add(new FeastPlanRsvpRow
                {
                    RsvpId = r.Id,
                    Name = r.ResponderName,
                    Group = AttendeeType.Of(r.ResponderGroup),
                    People = party.Count,
                    Vegetarian = vegetarian,
                    Halal = halal,
                    Standard = party.Count - vegetarian - halal,
                    RespondedAt = r.RespondedAt
                });
            }

            source.TotalGuests = attendees.Count;
            source.VegetarianGuests = attendees.Count(a => IsVegetarian(a.Profile));
            source.VeganGuests = attendees.Count(a => a.Profile != null && a.Profile.Preference == DietaryProfileService.PreferenceVegan);
            source.HalalGuests = attendees.Count(a => IsHalal(a.Profile));
            source.OtherRequirementGuests = attendees.Count(a => a.Profile != null && a.Profile.Preference == DietaryProfileService.OtherCode);

            if (source.Refusal == null && source.TotalGuests == 0)
            {
                source.Refusal = "Nobody has accepted the invitation to this event, so a feast plan can't be generated.";
            }

            // Step 2 — the event's buffet (its own meals, or its template's)
            int order = 0;
            foreach (var line in _buffet.GetBuffetLines(evt).Where(l => l.MenuItem.IsActive))
            {
                source.Dishes.Add(new EventFeastPlanItem
                {
                    MenuItemId = line.MenuItem.Id,
                    DishName = line.MenuItem.Name,
                    Section = string.IsNullOrWhiteSpace(line.Section) ? "Main" : line.Section.Trim(),
                    Category = DefaultCategory(line.Section, line.MenuItem),
                    SortOrder = order++
                });
            }

            return source;
        }

        private static bool IsVegetarian(StudentDietaryProfile p)
        {
            return p != null
                && (p.Preference == DietaryProfileService.PreferenceVegetarian
                    || p.Preference == DietaryProfileService.PreferenceVegan);
        }

        private static bool IsHalal(StudentDietaryProfile p)
        {
            return p != null && p.Preference == DietaryProfileService.PreferenceHalal;
        }

        // Main dishes go to a dietary group; everything else (dessert,
        // sides, starters, drinks) is common. The coordinator can
        // change any of these in step 7.
        public static FeastDishCategory DefaultCategory(string section, MenuItem item)
        {
            bool isMain = string.IsNullOrWhiteSpace(section)
                || string.Equals(section.Trim(), "Main", StringComparison.OrdinalIgnoreCase);

            if (!isMain) return FeastDishCategory.Common;

            string classification = item.DietaryClassification ?? "";
            if (string.Equals(classification, MealLibraryService.ClassificationVegetarian, StringComparison.OrdinalIgnoreCase)
                || string.Equals(classification, MealLibraryService.ClassificationVegan, StringComparison.OrdinalIgnoreCase))
            {
                return FeastDishCategory.Vegetarian;
            }

            if (HasHalalCertifiedMeat(item)) return FeastDishCategory.Halal;

            return FeastDishCategory.Standard;
        }

        // A dish with a halal-certified ingredient that passes the
        // team's halal check. Meat-free dishes stay Standard.
        private static bool HasHalalCertifiedMeat(MenuItem item)
        {
            if (item.Recipe == null || item.Recipe.RecipeIngredients == null) return false;

            var ingredients = item.Recipe.RecipeIngredients
                .Where(ri => ri.Ingredient != null)
                .Select(ri => ri.Ingredient)
                .ToList();

            return ingredients.Any(i => HasTag(i.DietaryTags, DietaryProfileService.PreferenceHalal))
                && DietaryProfileService.GetHalalProblem(ingredients) == null;
        }

        private static bool HasTag(string tags, string tag)
        {
            if (string.IsNullOrWhiteSpace(tags)) return false;

            return tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(t => string.Equals(t.Trim(), tag, StringComparison.OrdinalIgnoreCase));
        }

        // ============================================================
        // STEP 3 — dish quantities
        // ============================================================

        public static bool IsValidBuffer(decimal bufferPercent)
        {
            return bufferPercent >= MinBufferPercent && bufferPercent <= MaxBufferPercent;
        }

        // Fills GuestsCovered and the quantities on every item
        public static void Calculate(int totalGuests, int vegetarianGuests, int halalGuests, decimal bufferPercent, IList<EventFeastPlanItem> items)
        {
            foreach (var group in DietGroups(totalGuests, vegetarianGuests, halalGuests))
            {
                var dishes = InOrder(items.Where(i => i.Category == group.Category));
                if (dishes.Count == 0) continue;

                // Share the group between its dishes: one portion each
                int share = group.Guests / dishes.Count;
                int extra = group.Guests % dishes.Count;

                for (int i = 0; i < dishes.Count; i++)
                {
                    dishes[i].GuestsCovered = share + (i < extra ? 1 : 0);
                }
            }

            foreach (var item in items.Where(i => i.Category == FeastDishCategory.Common))
            {
                item.GuestsCovered = totalGuests;
            }

            // Favourite dishes keep the number of guests who asked for them
            // (set in step 3); everything else was set above.
            foreach (var item in items)
            {
                item.BaseQuantity = item.GuestsCovered;

                // Step 7: a dish with leftover history is trimmed by its
                // recorded leftover rate (never by more than the cap)
                int basis = item.BaseQuantity;
                if (item.OptimisedForWaste && item.LeftoverRatePercent > 0m)
                {
                    decimal trim = Math.Min(FeastRules.MaxWasteTrimPercent, item.LeftoverRatePercent);
                    basis = (int)Math.Ceiling(item.BaseQuantity * (100m - trim) / 100m);
                }

                item.TotalQuantity = WithBuffer(basis, bufferPercent);
                item.BufferQuantity = Math.Max(0, item.TotalQuantity - item.BaseQuantity);
            }
        }

        // Quantity plus the buffer, rounded up to a whole portion
        public static int WithBuffer(int quantity, decimal bufferPercent)
        {
            if (quantity <= 0) return 0;
            return (int)Math.Ceiling(quantity * (100m + bufferPercent) / 100m);
        }

        public static FeastPlanCheck Check(int totalGuests, int vegetarianGuests, int halalGuests, IEnumerable<EventFeastPlanItem> items)
        {
            var check = new FeastPlanCheck();
            var list = items.ToList();

            if (list.Count == 0)
            {
                check.Problems.Add("The plan has no dishes. Add meals to the event's buffet, or add a dish to the plan.");
                return check;
            }

            foreach (var group in DietGroups(totalGuests, vegetarianGuests, halalGuests))
            {
                var dishes = InOrder(list.Where(i => i.Category == group.Category));

                if (dishes.Count == 0 && group.Guests > 0)
                {
                    check.Problems.Add(string.Format("No {0} dish on the plan for {1} {0} guest{2}.",
                        group.Label, group.Guests, group.Guests == 1 ? "" : "s"));
                }
                else if (dishes.Count > 0 && group.Guests == 0)
                {
                    check.Notes.Add(string.Format("No {0} guests are attending, so {1} get{2} no portions.",
                        group.Label, string.Join(", ", dishes.Select(d => d.DishName)), dishes.Count == 1 ? "s" : ""));
                }
            }

            return check;
        }

        public static FeastPlanCheck Check(EventFeastPlan plan)
        {
            return Check(plan.TotalGuests, plan.VegetarianGuests, plan.HalalGuests, plan.Items);
        }

        private class DietGroup
        {
            public FeastDishCategory Category { get; set; }
            public int Guests { get; set; }
            public string Label { get; set; }
        }

        private static List<DietGroup> DietGroups(int totalGuests, int vegetarianGuests, int halalGuests)
        {
            return new List<DietGroup>
            {
                new DietGroup { Category = FeastDishCategory.Standard, Guests = Math.Max(0, totalGuests - vegetarianGuests - halalGuests), Label = "standard" },
                new DietGroup { Category = FeastDishCategory.Vegetarian, Guests = vegetarianGuests, Label = "vegetarian" },
                new DietGroup { Category = FeastDishCategory.Halal, Guests = halalGuests, Label = "halal" }
            };
        }

        public static List<EventFeastPlanItem> InOrder(IEnumerable<EventFeastPlanItem> items)
        {
            return items
                .OrderBy(i => i.Category)
                .ThenBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .ToList();
        }

        private static void ApplyCounts(EventFeastPlan plan, FeastPlanSource source)
        {
            plan.TotalGuests = source.TotalGuests;
            plan.VegetarianGuests = source.VegetarianGuests;
            plan.VeganGuests = source.VeganGuests;
            plan.HalalGuests = source.HalalGuests;
            plan.StandardGuests = source.StandardGuests;
            plan.OtherRequirementGuests = source.OtherRequirementGuests;
            plan.RsvpResponses = source.Rsvps.Count;
        }

        // ============================================================
        // STEP 4 — generate the proposed plan
        // ============================================================

        public FeastPlanResult Generate(int eventId, decimal bufferPercent, FeastPlanUser user)
        {
            if (!FeastPlanRoles.CanCoordinate(user.Role))
            {
                return FeastPlanResult.Fail("Only the Meal Coordinator can generate a feast plan.");
            }

            if (!IsValidBuffer(bufferPercent))
            {
                return FeastPlanResult.Fail("The buffer must be between 0% and 100%.");
            }

            var evt = FindEvent(eventId);
            var source = ReadSource(evt);
            if (!source.CanGenerate) return FeastPlanResult.Fail(source.Refusal);

            // The SRS preconditions are not optional (recipes, ingredients and
            // colour / texture tags on the buffet meals)
            var unmet = new FeastAnalysisService(_db)
                .Prerequisites(evt, source.TotalGuests, source.Dishes)
                .Where(p => !p.Met)
                .ToList();
            if (unmet.Count > 0)
            {
                return FeastPlanResult.Fail("These conditions in the use case aren't met yet: " + string.Join("; ", unmet.Select(u =>
                    u.Text + (string.IsNullOrWhiteSpace(u.Detail) ? "" : " (" + u.Detail + ")"))) + ".");
            }

            try
            {
                // Serializable so two people can't both create the
                // event's live plan at the same moment
                using (var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var live = LivePlanFor(eventId);
                    if (live != null)
                    {
                        return new FeastPlanResult
                        {
                            Success = false,
                            PlanId = live.Id,
                            Message = "This event already has a feast plan. Only one live plan is allowed per event."
                        };
                    }

                    var plan = new EventFeastPlan
                    {
                        Status = FeastPlanStatus.Drafting,
                        Stage = FeastStage.Favourites,
                        EventId = evt.Id,
                        BufferPercent = Math.Round(bufferPercent, 2),
                        GeneratedByUserId = user.UserId,
                        GeneratedAt = DateTime.UtcNow,
                        CalculatedAt = DateTime.UtcNow
                    };

                    ApplyCounts(plan, source);
                    Calculate(plan.TotalGuests, plan.VegetarianGuests, plan.HalalGuests, plan.BufferPercent, source.Dishes);

                    foreach (var item in source.Dishes) plan.Items.Add(item);

                    AddHistory(plan, ActionGenerated, user, string.Format(
                        "Started from {0} guest{1} in {2} RSVP response{3}; {4:0.##}% buffer.",
                        plan.TotalGuests, plan.TotalGuests == 1 ? "" : "s",
                        plan.RsvpResponses, plan.RsvpResponses == 1 ? "" : "s",
                        plan.BufferPercent));

                    _db.EventFeastPlans.Add(plan);
                    _db.SaveChanges();
                    tx.Commit();

                    return FeastPlanResult.Ok(plan.Id, "Feast plan started. Step 3: check the guests' cultural favourites against projected stock.");
                }
            }
            catch (DbUpdateException)
            {
                return FeastPlanResult.Fail("The plan couldn't be saved because someone else was working on this event. Please try again.");
            }
            catch (EntityCommandExecutionException)
            {
                return FeastPlanResult.Fail("The plan couldn't be saved because someone else was working on this event. Please try again.");
            }
        }

        // ============================================================
        // STEP 5 → 6 — the coordinator sends the plan for approval
        // ============================================================

        public FeastPlanResult Submit(int planId, byte[] rowVersion, string notes, FeastPlanUser user, Func<string, int, string> link)
        {
            if (!FeastPlanRoles.CanCoordinate(user.Role))
            {
                return FeastPlanResult.Fail("Only the Meal Coordinator can send a feast plan for approval.");
            }

            var plan = FindPlan(planId);
            if (plan == null) return FeastPlanResult.Fail("Feast plan not found.");
            if (plan.IsLocked) return FeastPlanResult.Fail("This plan has been approved and is locked.");
            if (!plan.IsEditable) return FeastPlanResult.Fail("This plan is " + StatusLabel(plan.Status).ToLowerInvariant() + " and can't be sent for approval.");

            if (plan.Stage < FeastStage.ReadyForReview)
            {
                return FeastPlanResult.Fail("Finish steps 3 to 7 first. The plan is at step " + plan.Stage + ".");
            }

            var check = Check(plan);
            if (check.Problems.Count > 0)
            {
                return FeastPlanResult.Fail("Fix these before sending the plan for approval: " + string.Join(" ", check.Problems));
            }

            if (!plan.Items.Any(i => i.TotalQuantity > 0))
            {
                return FeastPlanResult.Fail("Nothing on the plan has a quantity to prepare.");
            }

            // A resubmission after changes is a new revision
            if (plan.SubmittedAt.HasValue) plan.Revision++;

            if (!string.IsNullOrWhiteSpace(notes)) plan.CoordinatorNotes = Trim(notes, 1000);
            plan.Status = FeastPlanStatus.AwaitingApproval;
            plan.SubmittedByUserId = user.UserId;
            plan.SubmittedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;

            AddHistory(plan, ActionSubmitted, user, notes);

            var managers = UserIdsInRole(FeastPlanRoles.CafeteriaManager);
            Notify(managers, string.Format(
                "Feast plan for {0} on {1} (revision {2}) is waiting for your approval: {3}",
                plan.Event.EventName, plan.Event.EventDate.ToString("ddd dd MMM"), plan.Revision,
                link("Decision", plan.Id)), plan.Id);

            var saved = Save(plan, rowVersion);
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, managers.Count > 0
                ? "Plan sent to the Cafeteria Manager for approval."
                : "Plan is waiting for approval, but no Cafeteria Manager account was found to notify.");
        }

        // ============================================================
        // STEP 6 — the Cafeteria Manager decides
        // STEP 8 — on approval: saved, locked and sent
        // ============================================================

        public FeastPlanResult Decide(int planId, string decision, string comments, byte[] rowVersion, FeastPlanUser user, Func<string, int, string> link)
        {
            if (!FeastPlanRoles.CanApprove(user.Role))
            {
                return FeastPlanResult.Fail("Only the Cafeteria Manager can approve, reject or request changes to a feast plan.");
            }

            var plan = FindPlan(planId);
            if (plan == null) return FeastPlanResult.Fail("Feast plan not found.");
            if (plan.IsLocked) return FeastPlanResult.Fail("This plan has already been approved and is locked.");
            if (plan.Status != FeastPlanStatus.AwaitingApproval) return FeastPlanResult.Fail("This plan isn't waiting for approval.");

            if (decision != DecisionApprove && decision != DecisionReject && decision != DecisionRequestChanges)
            {
                return FeastPlanResult.Fail("Choose approve, reject or request changes.");
            }

            comments = Trim(comments, 1000);
            bool needsReason = decision == DecisionReject || decision == DecisionRequestChanges;
            if (needsReason && string.IsNullOrWhiteSpace(comments))
            {
                return FeastPlanResult.Fail(decision == DecisionReject
                    ? "Give a reason for rejecting the plan."
                    : "Say what needs to change.");
            }

            string eventLabel = string.Format("{0} on {1}", plan.Event.EventName, plan.Event.EventDate.ToString("ddd dd MMM"));
            string message;

            plan.DecidedByUserId = user.UserId;
            plan.DecidedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
            plan.ManagerComments = comments;

            if (decision == DecisionApprove)
            {
                plan.Status = FeastPlanStatus.Approved;
                AddHistory(plan, ActionApproved, user, comments);

                // Step 8 — send to the Cafeteria Manager and kitchen
                var managers = UserIdsInRole(FeastPlanRoles.CafeteriaManager);
                var kitchen = UserIdsInRole(FeastPlanRoles.KitchenStaff);
                var recipients = managers.Union(kitchen).ToList();

                Notify(recipients, string.Format(
                    "Approved feast plan — {0}: {1} guests, {2} dishes. {3}",
                    eventLabel, plan.TotalGuests, plan.Items.Count, link("Approved", plan.Id)), plan.Id);

                plan.SentAt = DateTime.UtcNow;
                AddHistory(plan, ActionSent, user, string.Format(
                    "Sent to {0} Cafeteria Manager and {1} Kitchen Staff account{2}.",
                    managers.Count, kitchen.Count, kitchen.Count == 1 ? "" : "s"));

                message = kitchen.Count > 0
                    ? "Feast plan approved, locked and sent to the Cafeteria Manager and Kitchen Staff."
                    : "Feast plan approved and locked. No Kitchen Staff (Chef) account was found to send it to.";
            }
            else if (decision == DecisionReject)
            {
                plan.Status = FeastPlanStatus.Rejected;
                AddHistory(plan, ActionRejected, user, comments);
                NotifyCoordinators(plan, string.Format(
                    "Feast plan for {0} was rejected: {1} A new plan can be generated. {2}",
                    eventLabel, comments, link("Review", plan.Id)));
                message = "Feast plan rejected. The Meal Coordinator can generate a new one.";
            }
            else
            {
                plan.Status = FeastPlanStatus.ChangesRequested;
                AddHistory(plan, ActionChangesRequested, user, comments);
                NotifyCoordinators(plan, string.Format(
                    "Changes requested on the feast plan for {0}: {1} {2}",
                    eventLabel, comments, link("Review", plan.Id)));
                message = "Changes requested. The plan has gone back to the Meal Coordinator.";
            }

            var saved = Save(plan, rowVersion);
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, message);
        }

        // ============================================================
        // STEP 7 — edit and recalculate (then back to step 5)
        // ============================================================

        public FeastPlanResult Update(FeastPlanEditInput input, FeastPlanUser user)
        {
            if (!FeastPlanRoles.CanCoordinate(user.Role))
            {
                return FeastPlanResult.Fail("Only the Meal Coordinator can edit a feast plan.");
            }

            var plan = FindPlan(input.Id);
            if (plan == null) return FeastPlanResult.Fail("Feast plan not found.");
            if (plan.IsLocked) return FeastPlanResult.Fail("This plan has been approved and is locked.");
            if (!plan.IsEditable) return FeastPlanResult.Fail("This plan is " + StatusLabel(plan.Status).ToLowerInvariant() + " and can't be edited.");

            if (!IsValidBuffer(input.BufferPercent))
            {
                return FeastPlanResult.Fail("The buffer must be between 0% and 100%.");
            }

            MenuItem addMeal = null;
            if (input.AddMenuItemId.HasValue)
            {
                int addId = input.AddMenuItemId.Value;
                addMeal = _db.MenuItems.FirstOrDefault(m => m.Id == addId && m.IsActive);
                if (addMeal == null) return FeastPlanResult.Fail("Choose an active meal from the meal library to add.");
                if (!Enum.IsDefined(typeof(FeastDishCategory), input.AddCategory)) return FeastPlanResult.Fail("Choose who the added dish is for.");
            }

            // Recalculate from the RSVPs as they are now (steps 1–3)
            var source = ReadSource(plan.Event);
            if (!source.CanGenerate) return FeastPlanResult.Fail(source.Refusal);

            var lines = (input.Items ?? new List<FeastPlanEditLine>())
                .GroupBy(l => l.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var remaining = new List<EventFeastPlanItem>();
            var changes = new List<string>();

            foreach (var item in plan.Items.ToList())
            {
                FeastPlanEditLine line;
                if (!lines.TryGetValue(item.Id, out line))
                {
                    remaining.Add(item);
                    continue;
                }

                if (line.Remove)
                {
                    changes.Add("removed " + item.DishName);
                    _db.EventFeastPlanItems.Remove(item);
                    continue;
                }

                if (item.Category != FeastDishCategory.Favourite
                    && line.Category != FeastDishCategory.Favourite
                    && Enum.IsDefined(typeof(FeastDishCategory), line.Category) && line.Category != item.Category)
                {
                    changes.Add(string.Format("{0}: {1} → {2}", item.DishName, CategoryLabel(item.Category), CategoryLabel(line.Category)));
                    item.Category = line.Category;
                }

                remaining.Add(item);
            }

            if (addMeal != null)
            {
                string section = string.IsNullOrWhiteSpace(input.AddSection)
                    ? (input.AddCategory == FeastDishCategory.Common ? "Other" : "Main")
                    : Trim(input.AddSection, 50);

                var added = new EventFeastPlanItem
                {
                    FeastPlanId = plan.Id,
                    MenuItemId = addMeal.Id,
                    DishName = Trim(addMeal.Name, 200),
                    Section = section,
                    Category = input.AddCategory,
                    SortOrder = remaining.Count == 0 ? 0 : remaining.Max(i => i.SortOrder) + 1
                };

                plan.Items.Add(added);
                remaining.Add(added);
                changes.Add(string.Format("added {0} ({1})", added.DishName, CategoryLabel(added.Category)));
            }

            decimal buffer = Math.Round(input.BufferPercent, 2);
            if (buffer != plan.BufferPercent)
            {
                changes.Add(string.Format("buffer {0:0.##}% → {1:0.##}%", plan.BufferPercent, buffer));
            }

            plan.BufferPercent = buffer;
            plan.CoordinatorNotes = Trim(input.CoordinatorNotes, 1000);

            int before = plan.TotalGuests;
            ApplyCounts(plan, source);
            if (plan.TotalGuests != before)
            {
                changes.Add(string.Format("RSVPs re-read: {0} → {1} guests", before, plan.TotalGuests));
            }

            Calculate(plan.TotalGuests, plan.VegetarianGuests, plan.HalalGuests, plan.BufferPercent, remaining);
            plan.CalculatedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;

            AddHistory(plan, ActionEdited, user, changes.Count > 0 ? string.Join("; ", changes) + "." : "Recalculated, no changes.");

            var saved = Save(plan, ParseRowVersion(input.RowVersion));
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, "Plan recalculated. Review it again, then send it for approval.");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        public static string StatusLabel(FeastPlanStatus status)
        {
            switch (status)
            {
                case FeastPlanStatus.Drafting: return "Drafting";
                case FeastPlanStatus.Proposed: return "Proposed";
                case FeastPlanStatus.AwaitingApproval: return "Awaiting approval";
                case FeastPlanStatus.ChangesRequested: return "Changes requested";
                case FeastPlanStatus.Approved: return "Approved";
                case FeastPlanStatus.Rejected: return "Rejected";
                default: return status.ToString();
            }
        }

        public static string StatusBadge(FeastPlanStatus status)
        {
            switch (status)
            {
                case FeastPlanStatus.Drafting: return "bg-primary";
                case FeastPlanStatus.Proposed: return "bg-secondary";
                case FeastPlanStatus.AwaitingApproval: return "bg-warning text-dark";
                case FeastPlanStatus.ChangesRequested: return "bg-info text-dark";
                case FeastPlanStatus.Approved: return "bg-success";
                case FeastPlanStatus.Rejected: return "bg-danger";
                default: return "bg-secondary";
            }
        }

        public static string CategoryLabel(FeastDishCategory category)
        {
            switch (category)
            {
                case FeastDishCategory.Standard: return "Standard";
                case FeastDishCategory.Vegetarian: return "Vegetarian";
                case FeastDishCategory.Halal: return "Halal";
                case FeastDishCategory.Common: return "Common (everyone)";
                case FeastDishCategory.Favourite: return "Cultural favourite";
                default: return category.ToString();
            }
        }

        public static string HistoryLabel(string action)
        {
            switch (action)
            {
                case ActionGenerated: return "Plan started";
                case ActionFavourites: return "Cultural favourites chosen";
                case ActionStepDone: return "Step completed";
                case ActionSwap: return "Dish swapped";
                case ActionWasteOptimised: return "Waste optimisation run";
                case ActionLeftovers: return "Leftovers recorded";
                case ActionDemoHistory: return "Sample history changed";
                case ActionEdited: return "Edited and recalculated";
                case ActionSubmitted: return "Sent for approval";
                case ActionApproved: return "Approved";
                case ActionRejected: return "Rejected";
                case ActionChangesRequested: return "Changes requested";
                case ActionSent: return "Sent to Cafeteria Manager and Kitchen Staff";
                default: return action;
            }
        }

        // Which of the 10 SRS steps a plan is at:
        // 3-7 while drafting, 8 review, 9 manager decision, 10 saved and sent
        public static int StepFor(EventFeastPlan plan)
        {
            switch (plan.Status)
            {
                case FeastPlanStatus.Drafting:
                    return plan.Stage >= FeastStage.ReadyForReview ? 8 : Math.Max(FeastStage.Favourites, plan.Stage);
                case FeastPlanStatus.Proposed: return 8;
                case FeastPlanStatus.AwaitingApproval: return 9;
                case FeastPlanStatus.ChangesRequested: return 9;
                case FeastPlanStatus.Approved: return 10;
                default: return 9;
            }
        }

        public static byte[] ParseRowVersion(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }

        private static void AddHistory(EventFeastPlan plan, string action, FeastPlanUser user, string comments)
        {
            plan.History.Add(new EventFeastPlanHistory
            {
                Revision = plan.Revision,
                Action = action,
                UserId = user.UserId,
                UserName = Trim(user.Name, 200),
                UserRole = Trim(user.Role, 50),
                Comments = Trim(comments, 1000),
                At = DateTime.UtcNow
            });
        }

        // Saves, failing if someone else changed the plan since the
        // page was opened. Returns null on success.
        private FeastPlanResult Save(EventFeastPlan plan, byte[] rowVersion)
        {
            if (rowVersion != null)
            {
                _db.Entry(plan).Property(p => p.RowVersion).OriginalValue = rowVersion;
            }

            try
            {
                _db.SaveChanges();
                return null;
            }
            catch (DbUpdateConcurrencyException)
            {
                return FeastPlanResult.Fail("Someone else changed this plan while you had it open. Please look at it again and retry.");
            }
        }

        private List<int> UserIdsInRole(string role)
        {
            return _db.Users
                .Where(u => u.Role == role)
                .Select(u => u.UserId)
                .ToList();
        }

        // Whoever generated and submitted the plan
        private void NotifyCoordinators(EventFeastPlan plan, string message)
        {
            var ids = new List<int> { plan.GeneratedByUserId };
            if (plan.SubmittedByUserId.HasValue) ids.Add(plan.SubmittedByUserId.Value);
            Notify(ids.Where(id => id > 0).Distinct().ToList(), message, plan.Id);
        }

        private void Notify(IEnumerable<int> userIds, string message, int planId)
        {
            var now = DateTime.Now;
            foreach (var uid in userIds.Distinct())
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Message = message,
                    RelatedEntityType = NotificationType,
                    RelatedEntityId = planId,
                    IsRead = false,
                    CreatedAt = now
                });
            }
        }
    }
}
