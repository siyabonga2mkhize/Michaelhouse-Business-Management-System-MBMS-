using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC19 — Generate Feast Plan: the analysis behind SRS steps
    //   3  cultural favourites checked against projected stock
    //   5  Menu Fatigue Score, variety matrix and replacements
    //   6  colour / texture balance of each buffet section
    //   7  consumption and waste optimisation
    //
    // This class only READS (and, for tags, writes the tag table).
    // Changing a plan is done by FeastPlanService, which calls
    // these rules.
    //
    // The numbers the SRS leaves open are in FeastRules and are
    // listed on screen, so the lecturer can see every assumption.
    // ============================================================

    public static class FeastRules
    {
        // Step 5 — Menu Fatigue Score (0-100)
        public const int FatigueWindowDays = 28;       // "last 28 days"
        public const int FatigueFlagScore = 70;        // "above 70" is flagged
        public const int FatigueFlagServings = 2;      // "already served twice" is flagged
        public const int PointsPerServing = 30;        // assumption: frequency points
        public const int MaxServingPoints = 60;
        public const int MaxWastePoints = 40;          // assumption: waste points at 100% left over

        // Step 6 — "more than two dishes share a colour or texture"
        public const int BalanceMaxShared = 2;

        // Step 7 — assumptions
        public const int LeftoverLookbackDays = 365;
        public const decimal MaxWasteTrimPercent = 30m;

        public static int FatigueScore(int timesServed, decimal leftoverFraction)
        {
            if (timesServed < 0) timesServed = 0;
            if (leftoverFraction < 0m) leftoverFraction = 0m;
            if (leftoverFraction > 1m) leftoverFraction = 1m;

            int servingPoints = Math.Min(MaxServingPoints, timesServed * PointsPerServing);
            int wastePoints = (int)Math.Round(leftoverFraction * MaxWastePoints, MidpointRounding.AwayFromZero);

            return Math.Min(100, servingPoints + wastePoints);
        }

        public static bool IsFlagged(int score, int timesServed)
        {
            return score > FatigueFlagScore || timesServed >= FatigueFlagServings;
        }
    }

    // ── Rows the screens show ───────────────────────────────────

    public class FeastPrerequisite
    {
        public FeastPrerequisite(string text, bool met, string detail)
        {
            Text = text;
            Met = met;
            Detail = detail;
        }

        public string Text { get; set; }
        public bool Met { get; set; }
        public string Detail { get; set; }
    }

    public class FavouriteRequest
    {
        public string Dish { get; set; }       // as the guests typed it (most common spelling)
        public string Key { get; set; }        // normalised, for grouping
        public int Guests { get; set; }
        public List<string> RequestedBy { get; set; }

        public FavouriteRequest()
        {
            RequestedBy = new List<string>();
        }
    }

    public enum FavouriteStatus
    {
        Available = 0,
        NotAvailable = 1,
        NotListed = 2
    }

    public class IngredientShortage
    {
        public string Ingredient { get; set; }
        public string Unit { get; set; }
        public decimal Required { get; set; }
        public decimal Projected { get; set; }
        public decimal Short { get { return Math.Max(0m, Required - Projected); } }
    }

    public class FavouriteRow
    {
        public FavouriteRow()
        {
            Shortages = new List<IngredientShortage>();
            RequestedBy = new List<string>();
        }

        public string Dish { get; set; }
        public int Guests { get; set; }
        public List<string> RequestedBy { get; set; }

        public int? MenuItemId { get; set; }
        public string MatchedMeal { get; set; }
        public FavouriteStatus Status { get; set; }
        public List<IngredientShortage> Shortages { get; set; }

        public bool AlreadyOnPlan { get; set; }   // matched meal is already a dish on the plan
        public bool Selected { get; set; }        // already added as a favourite dish
    }

    public class ProjectedStockLine
    {
        public int IngredientId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal OnHand { get; set; }
        public decimal DeliveriesDue { get; set; }
        public decimal Allocated { get; set; }
        public decimal Projected { get { return OnHand + DeliveriesDue - Allocated; } }
    }

    public class FatigueInfo
    {
        public int TimesServed { get; set; }
        public decimal LeftoverFraction { get; set; }
        public int LeftoverRecords { get; set; }
        public int Score { get; set; }
        public bool Flagged { get; set; }
    }

    public class ReplacementOption
    {
        public MenuItem Meal { get; set; }
        public string Colour { get; set; }
        public string Texture { get; set; }
        public FatigueInfo Fatigue { get; set; }
    }

    public class FatigueRow
    {
        public EventFeastPlanItem Item { get; set; }
        public FatigueInfo Fatigue { get; set; }
        public string Reason { get; set; }
        public ReplacementOption Suggestion { get; set; }   // null = none suitable
        public bool CanSwap { get; set; }                   // favourites are never swapped
    }

    public class BalanceFinding
    {
        public BalanceFinding()
        {
            Dishes = new List<EventFeastPlanItem>();
        }

        public string Section { get; set; }
        public string Attribute { get; set; }               // "Colour" or "Texture"
        public string Value { get; set; }
        public List<EventFeastPlanItem> Dishes { get; set; }

        public EventFeastPlanItem Replace { get; set; }     // the dish to swap out
        public ReplacementOption Suggestion { get; set; }   // null = none suitable
    }

    public class BalanceSection
    {
        public BalanceSection()
        {
            Dishes = new List<EventFeastPlanItem>();
            Findings = new List<BalanceFinding>();
        }

        public string Section { get; set; }
        public List<EventFeastPlanItem> Dishes { get; set; }
        public List<BalanceFinding> Findings { get; set; }
    }

    public class BalanceResult
    {
        public BalanceResult()
        {
            Sections = new List<BalanceSection>();
            Tags = new Dictionary<int, MenuItemBuffetTag>();
            Untagged = new List<EventFeastPlanItem>();
        }

        public List<BalanceSection> Sections { get; set; }
        public Dictionary<int, MenuItemBuffetTag> Tags { get; set; }
        public List<EventFeastPlanItem> Untagged { get; set; }

        public int FindingCount
        {
            get { return Sections.Sum(s => s.Findings.Count); }
        }
    }

    public class WasteRow
    {
        public EventFeastPlanItem Item { get; set; }
        public int LeftoverRecords { get; set; }
        public decimal LeftoverPercent { get; set; }
        public bool HasHistory { get; set; }
        public int CurrentQuantity { get; set; }
        public int OptimisedQuantity { get; set; }
        public int PortionsSaved { get { return Math.Max(0, CurrentQuantity - OptimisedQuantity); } }
    }

    public class FeastReviewSummary
    {
        public int FavouriteRequests { get; set; }
        public int FavouritesAdded { get; set; }
        public int FavouritesNotAdded { get; set; }
        public int FlaggedDishes { get; set; }
        public int OpenBalanceFindings { get; set; }
        public int UntaggedDishes { get; set; }
        public int SwapsMade { get; set; }
        public int DishesTrimmed { get; set; }
    }

    public class TagRow
    {
        public MenuItem Meal { get; set; }
        public MenuItemBuffetTag Tag { get; set; }
    }

    public class FeastAnalysisService
    {
        private readonly DBContextClass _db;

        public FeastAnalysisService(DBContextClass db)
        {
            if (db == null) throw new ArgumentNullException("db");
            _db = db;
        }

        // ============================================================
        // PRECONDITIONS (SRS UC19)
        // ============================================================

        public List<FeastPrerequisite> Prerequisites(CafeteriaEvent evt, int attendingGuests, IEnumerable<EventFeastPlanItem> dishes)
        {
            var list = new List<FeastPrerequisite>();
            var dishList = dishes == null ? new List<EventFeastPlanItem>() : dishes.ToList();

            list.Add(new FeastPrerequisite(
                "An event has been scheduled (Schedule Event)",
                evt != null,
                evt == null ? "Event not found." : evt.EventName + " on " + evt.EventDate.ToString("dd MMM yyyy")));

            bool closed = evt != null
                && (evt.Status == EventStatus.RsvpClosed || evt.Status == EventStatus.FeastPlanGenerated);
            list.Add(new FeastPrerequisite(
                "The RSVP period has closed",
                closed,
                evt == null ? "" : (closed ? "RSVPs are closed." : "Status is " + evt.Status + ". Close RSVPs first.")));

            list.Add(new FeastPrerequisite(
                "RSVPs for the event have been captured (RSVP to Event)",
                attendingGuests > 0,
                attendingGuests + " attending guest" + (attendingGuests == 1 ? "" : "s")));

            var ids = dishList.Where(d => d.MenuItemId.HasValue).Select(d => d.MenuItemId.Value).Distinct().ToList();

            var meals = ids.Count == 0
                ? new List<MenuItem>()
                : _db.MenuItems.Include("Recipe.RecipeIngredients").Where(m => ids.Contains(m.Id)).ToList();

            var noRecipe = meals
                .Where(m => m.Recipe == null || m.Recipe.RecipeIngredients == null || m.Recipe.RecipeIngredients.Count == 0)
                .Select(m => m.Name)
                .ToList();

            list.Add(new FeastPrerequisite(
                "Meals in the meal library are linked to recipes and ingredients",
                dishList.Count > 0 && noRecipe.Count == 0,
                dishList.Count == 0
                    ? "The event's buffet has no meals."
                    : noRecipe.Count == 0 ? "All buffet meals have a recipe." : "No recipe or ingredients: " + string.Join(", ", noRecipe)));

            var tagged = new HashSet<int>(_db.MenuItemBuffetTags.Where(t => ids.Contains(t.MenuItemId)).Select(t => t.MenuItemId).ToList());
            var untagged = meals.Where(m => !tagged.Contains(m.Id)).Select(m => m.Name).ToList();

            list.Add(new FeastPrerequisite(
                "Meals carry colour and texture tags (dietary tags are on the meal and its recipe)",
                dishList.Count > 0 && untagged.Count == 0,
                dishList.Count == 0
                    ? ""
                    : untagged.Count == 0 ? "All buffet meals are tagged." : "Not tagged yet: " + string.Join(", ", untagged)));

            return list;
        }

        // ============================================================
        // STEP 1 (cultural favourites entered on the RSVPs)
        // ============================================================

        public List<FavouriteRequest> FavouriteRequests(int eventId)
        {
            var raw = _db.EventRsvpGuests
                .Where(g => g.Rsvp.EventId == eventId
                            && g.Rsvp.ResponseStatus == EventRsvpService.ResponseAttending
                            && g.CulturalFavoriteDish != null
                            && g.CulturalFavoriteDish != "")
                .Select(g => new { Dish = g.CulturalFavoriteDish, Person = g.Rsvp.ResponderName })
                .ToList();

            var result = new List<FavouriteRequest>();

            foreach (var group in raw.GroupBy(r => NormaliseName(r.Dish)).Where(g => g.Key.Length > 0))
            {
                var spelling = group
                    .GroupBy(r => r.Dish.Trim())
                    .OrderByDescending(s => s.Count())
                    .ThenBy(s => s.Key)
                    .First().Key;

                var request = new FavouriteRequest
                {
                    Dish = spelling,
                    Key = group.Key,
                    Guests = group.Count()
                };

                request.RequestedBy.AddRange(group.Select(r => r.Person).Distinct().OrderBy(p => p));
                result.Add(request);
            }

            return result.OrderByDescending(r => r.Guests).ThenBy(r => r.Dish).ToList();
        }

        // Lower case, letters and digits only
        public static string NormaliseName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var chars = text.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
                .ToArray();

            return string.Join(" ", new string(chars).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        // ============================================================
        // STEP 3 — match favourites to the meal library and check
        // them against the projected stock for the event date
        // ============================================================

        public List<FavouriteRow> ReadFavourites(EventFeastPlan plan)
        {
            var rows = new List<FavouriteRow>();
            var requests = FavouriteRequests(plan.EventId);
            if (requests.Count == 0) return rows;

            var library = _db.MenuItems
                .Include("Recipe.RecipeIngredients.Ingredient")
                .Where(m => m.IsActive)
                .ToList();

            var projected = ProjectedStock(plan.Event.EventDate, plan.EventId);

            var onPlan = new HashSet<int>(plan.Items
                .Where(i => i.MenuItemId.HasValue)
                .Select(i => i.MenuItemId.Value));

            var addedFavourites = new HashSet<int>(plan.Items
                .Where(i => i.Category == FeastDishCategory.Favourite && i.MenuItemId.HasValue)
                .Select(i => i.MenuItemId.Value));

            foreach (var request in requests)
            {
                var row = new FavouriteRow
                {
                    Dish = request.Dish,
                    Guests = request.Guests,
                    RequestedBy = request.RequestedBy
                };

                var meal = MatchMeal(request.Key, library);

                if (meal == null)
                {
                    row.Status = FavouriteStatus.NotListed;
                    rows.Add(row);
                    continue;
                }

                row.MenuItemId = meal.Id;
                row.MatchedMeal = meal.Name;
                row.AlreadyOnPlan = onPlan.Contains(meal.Id) && !addedFavourites.Contains(meal.Id);
                row.Selected = addedFavourites.Contains(meal.Id);

                // One portion for each guest who asked for it, plus the plan's buffer
                int portions = FeastPlanService.WithBuffer(request.Guests, plan.BufferPercent);

                foreach (var need in Requirement(meal, portions))
                {
                    decimal available;
                    ProjectedStockLine line;
                    available = projected.TryGetValue(need.Key.Id, out line) ? line.Projected : 0m;

                    if (need.Value > available)
                    {
                        row.Shortages.Add(new IngredientShortage
                        {
                            Ingredient = need.Key.Name,
                            Unit = need.Key.Unit,
                            Required = need.Value,
                            Projected = available
                        });
                    }
                }

                row.Status = row.Shortages.Count == 0 ? FavouriteStatus.Available : FavouriteStatus.NotAvailable;
                rows.Add(row);
            }

            return rows;
        }

        // Best match for a normalised favourite name: exact first, then
        // one name containing the other
        public static MenuItem MatchMeal(string normalisedFavourite, IEnumerable<MenuItem> library)
        {
            if (string.IsNullOrWhiteSpace(normalisedFavourite)) return null;

            var named = library.Select(m => new { Meal = m, Key = NormaliseName(m.Name) }).Where(x => x.Key.Length > 0).ToList();

            var exact = named.FirstOrDefault(x => x.Key == normalisedFavourite);
            if (exact != null) return exact.Meal;

            if (normalisedFavourite.Length < 4) return null;

            var partial = named
                .Where(x => x.Key.Length >= 4 && (x.Key.Contains(normalisedFavourite) || normalisedFavourite.Contains(x.Key)))
                .OrderBy(x => Math.Abs(x.Key.Length - normalisedFavourite.Length))
                .FirstOrDefault();

            return partial == null ? null : partial.Meal;
        }

        // Each ingredient a meal needs for the given portions
        public static List<KeyValuePair<Ingredient, decimal>> Requirement(MenuItem meal, int portions)
        {
            var totals = new Dictionary<int, KeyValuePair<Ingredient, decimal>>();

            if (meal == null || meal.Recipe == null || meal.Recipe.RecipeIngredients == null || portions <= 0)
            {
                return new List<KeyValuePair<Ingredient, decimal>>();
            }

            foreach (var ri in meal.Recipe.RecipeIngredients.Where(r => r.Ingredient != null))
            {
                decimal quantity = ri.QuantityPerStandardPortion * portions;
                KeyValuePair<Ingredient, decimal> existing;

                totals[ri.IngredientId] = totals.TryGetValue(ri.IngredientId, out existing)
                    ? new KeyValuePair<Ingredient, decimal>(ri.Ingredient, existing.Value + quantity)
                    : new KeyValuePair<Ingredient, decimal>(ri.Ingredient, quantity);
            }

            return totals.Values.ToList();
        }

        // ── Projected stock (SRS): stock on hand + deliveries due
        //    before the event − stock already allocated ───────────────
        public Dictionary<int, ProjectedStockLine> ProjectedStock(DateTime eventDate, int excludeEventId)
        {
            var result = new Dictionary<int, ProjectedStockLine>();
            var day = eventDate.Date;

            foreach (var ingredient in _db.Ingredients.ToList())
            {
                result[ingredient.Id] = new ProjectedStockLine
                {
                    IngredientId = ingredient.Id,
                    Name = ingredient.Name,
                    Unit = ingredient.Unit,
                    OnHand = ingredient.IsActive
                        ? ingredient.FarmAvailableQuantity + ingredient.ExternalAvailableQuantity
                        : 0m
                };
            }

            // Deliveries due on or before the event: orders already with a
            // supplier that have a requested delivery date by then
            var openLines = _db.IngredientPurchaseOrderLines
                .Where(l => (l.PurchaseOrder.Status == IngredientOrderStatus.Pending
                             || l.PurchaseOrder.Status == IngredientOrderStatus.Confirmed
                             || l.PurchaseOrder.Status == IngredientOrderStatus.PartiallyFulfilled)
                            && l.PurchaseOrder.RequestedDeliveryDate != null
                            && l.PurchaseOrder.RequestedDeliveryDate <= day)
                .ToList();

            foreach (var group in openLines.GroupBy(l => l.IngredientId))
            {
                ProjectedStockLine line;
                if (result.TryGetValue(group.Key, out line)) line.DeliveriesDue = group.Sum(l => l.Outstanding);
            }

            foreach (var pair in AllocatedBefore(day, excludeEventId))
            {
                ProjectedStockLine line;
                if (result.TryGetValue(pair.Key, out line)) line.Allocated = pair.Value;
            }

            return result;
        }

        // What the kitchen has already committed between now and the
        // event: the accepted weekly menus and the other events, worked
        // out the same way the inventory screens do it
        private Dictionary<int, decimal> AllocatedBefore(DateTime eventDay, int excludeEventId)
        {
            var allocated = new Dictionary<int, decimal>();
            var today = SchoolClock.Today;

            Action<int, decimal> add = (ingredientId, quantity) =>
            {
                if (quantity <= 0m) return;
                decimal existing;
                allocated.TryGetValue(ingredientId, out existing);
                allocated[ingredientId] = existing + quantity;
            };

            // Accepted weekly menus
            var menuIds = _db.MealMenus
                .Where(m => m.MenuStatus == MenuStatus.Accepted && m.EndDate >= today && m.StartDate <= eventDay)
                .Select(m => m.Id)
                .ToList();

            var scheduling = new MenuSchedulingService(_db);
            foreach (var menuId in menuIds)
            {
                var production = scheduling.BuildProductionPlan(menuId);
                foreach (var d in production.Days.Where(d => d.Date >= today && d.Date <= eventDay && !d.IsIssued))
                {
                    foreach (var line in d.DayTotalIngredients) add(line.IngredientId, line.TotalRequired);
                }
            }

            // Other events whose headcount is confirmed
            var events = _db.CafeteriaEvents
                .Include(e => e.Venue)
                .Include(e => e.MenuTemplate)
                .Where(e => e.Id != excludeEventId
                            && e.EventDate >= today && e.EventDate <= eventDay
                            && (e.Status == EventStatus.RsvpClosed || e.Status == EventStatus.FeastPlanGenerated))
                .ToList();

            var issued = new HashSet<int>(_db.KitchenIngredientIssues
                .Where(i => i.CafeteriaEventId.HasValue)
                .Select(i => i.CafeteriaEventId.Value)
                .ToList());

            var buffet = new EventBuffetService(_db);
            foreach (var other in events.Where(e => !issued.Contains(e.Id)))
            {
                var otherPlan = buffet.BuildFeastPlan(other);
                foreach (var line in otherPlan.TotalIngredients) add(line.IngredientId, line.TotalRequired);
            }

            return allocated;
        }

        // ============================================================
        // STEP 5 — Menu Fatigue Score
        // ============================================================

        private class History
        {
            public Dictionary<int, HashSet<DateTime>> Served = new Dictionary<int, HashSet<DateTime>>();
            public Dictionary<int, int> Prepared = new Dictionary<int, int>();
            public Dictionary<int, int> Left = new Dictionary<int, int>();
            public Dictionary<int, int> Records = new Dictionary<int, int>();
        }

        // Meal collection records and leftover records for the 28 days
        // before the event
        private History LoadHistory(DateTime eventDate, int excludePlanId)
        {
            var history = new History();
            var end = eventDate.Date;
            var start = end.AddDays(-FeastRules.FatigueWindowDays);

            Action<int, DateTime> served = (menuItemId, date) =>
            {
                HashSet<DateTime> days;
                if (!history.Served.TryGetValue(menuItemId, out days))
                {
                    days = new HashSet<DateTime>();
                    history.Served[menuItemId] = days;
                }
                days.Add(date.Date);
            };

            // (a) what students collected at meal times
            var collections = _db.MealCollections
                .Where(c => c.MealPlanItemId != null && c.Date >= start && c.Date < end)
                .Select(c => new { c.Date, MenuItemId = c.MealPlanItem.MenuItemId })
                .ToList();
            foreach (var c in collections) served(c.MenuItemId, c.Date);

            // (b) what the kitchen recorded as left over at earlier events
            var leftovers = _db.EventDishLeftovers
                .Where(l => l.EventDate >= start && l.EventDate < end)
                .ToList();
            foreach (var l in leftovers)
            {
                served(l.MenuItemId, l.EventDate);

                int sum;
                history.Prepared.TryGetValue(l.MenuItemId, out sum);
                history.Prepared[l.MenuItemId] = sum + l.PreparedPortions;

                history.Left.TryGetValue(l.MenuItemId, out sum);
                history.Left[l.MenuItemId] = sum + l.LeftoverPortions;

                history.Records.TryGetValue(l.MenuItemId, out sum);
                history.Records[l.MenuItemId] = sum + 1;
            }

            // (c) dishes on other approved feast plans in the window
            var planned = _db.EventFeastPlanItems
                .Where(i => i.MenuItemId != null
                            && i.FeastPlanId != excludePlanId
                            && i.FeastPlan.Status == FeastPlanStatus.Approved
                            && i.FeastPlan.Event.EventDate >= start
                            && i.FeastPlan.Event.EventDate < end)
                .Select(i => new { MenuItemId = i.MenuItemId.Value, Date = i.FeastPlan.Event.EventDate })
                .ToList();
            foreach (var p in planned) served(p.MenuItemId, p.Date);

            return history;
        }

        private static FatigueInfo FatigueOf(History history, int menuItemId)
        {
            HashSet<DateTime> days;
            int timesServed = history.Served.TryGetValue(menuItemId, out days) ? days.Count : 0;

            int prepared, left, records;
            history.Prepared.TryGetValue(menuItemId, out prepared);
            history.Left.TryGetValue(menuItemId, out left);
            history.Records.TryGetValue(menuItemId, out records);

            decimal fraction = prepared > 0 ? Math.Min(1m, (decimal)left / prepared) : 0m;
            int score = FeastRules.FatigueScore(timesServed, fraction);

            return new FatigueInfo
            {
                TimesServed = timesServed,
                LeftoverFraction = fraction,
                LeftoverRecords = records,
                Score = score,
                Flagged = FeastRules.IsFlagged(score, timesServed)
            };
        }

        public Dictionary<int, FatigueInfo> FatigueFor(EventFeastPlan plan, IEnumerable<int> menuItemIds)
        {
            var history = LoadHistory(plan.Event.EventDate, plan.Id);
            var result = new Dictionary<int, FatigueInfo>();
            foreach (var id in menuItemIds.Distinct()) result[id] = FatigueOf(history, id);
            return result;
        }

        public List<FatigueRow> Fatigue(EventFeastPlan plan)
        {
            var history = LoadHistory(plan.Event.EventDate, plan.Id);
            var rows = new List<FatigueRow>();

            var candidates = EligibleLibrary(plan);

            foreach (var item in FeastPlanService.InOrder(plan.Items).Where(i => i.MenuItemId.HasValue))
            {
                var info = FatigueOf(history, item.MenuItemId.Value);

                var row = new FatigueRow
                {
                    Item = item,
                    Fatigue = info,
                    CanSwap = item.Category != FeastDishCategory.Favourite
                };

                if (info.Flagged)
                {
                    var reasons = new List<string>();
                    if (info.Score > FeastRules.FatigueFlagScore) reasons.Add("score " + info.Score + " is above " + FeastRules.FatigueFlagScore);
                    if (info.TimesServed >= FeastRules.FatigueFlagServings) reasons.Add("served " + info.TimesServed + " times in the last " + FeastRules.FatigueWindowDays + " days");
                    row.Reason = string.Join("; ", reasons);

                    if (row.CanSwap)
                    {
                        row.Suggestion = BestReplacement(candidates, item, history, null);
                    }
                }

                rows.Add(row);
            }

            return rows;
        }

        // ============================================================
        // REPLACEMENT CANDIDATES (steps 5 and 6)
        //
        // A replacement must be: in the active meal library, linked to a
        // recipe, tagged with colour and texture, not already on the plan,
        // used in the same buffet section before (the library has no
        // section of its own), and suitable for the same dietary group
        // as the dish it replaces.
        // ============================================================

        private class Candidate
        {
            public MenuItem Meal { get; set; }
            public MenuItemBuffetTag Tag { get; set; }
            public HashSet<string> Sections { get; set; }
        }

        private List<Candidate> EligibleLibrary(EventFeastPlan plan)
        {
            var onPlan = new HashSet<int>(plan.Items.Where(i => i.MenuItemId.HasValue).Select(i => i.MenuItemId.Value));

            var tags = _db.MenuItemBuffetTags.ToList().ToDictionary(t => t.MenuItemId);

            var meals = _db.MenuItems
                .Include("Recipe.RecipeIngredients.Ingredient")
                .Where(m => m.IsActive && m.RecipeId != null)
                .ToList()
                .Where(m => !onPlan.Contains(m.Id) && tags.ContainsKey(m.Id))
                .ToList();

            // Sections each meal has been used in, from past events and templates
            var sectionsByMeal = new Dictionary<int, HashSet<string>>();

            Action<int, string> addSection = (menuItemId, section) =>
            {
                HashSet<string> set;
                if (!sectionsByMeal.TryGetValue(menuItemId, out set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    sectionsByMeal[menuItemId] = set;
                }
                set.Add(string.IsNullOrWhiteSpace(section) ? "Main" : section.Trim());
            };

            foreach (var x in _db.CafeteriaEventMenuItems.Select(i => new { i.MenuItemId, i.Section }).ToList())
                addSection(x.MenuItemId, x.Section);

            foreach (var x in _db.EventMenuTemplateItems.Select(i => new { i.MenuItemId, i.Section }).ToList())
                addSection(x.MenuItemId, x.Section);

            return meals.Select(m =>
            {
                HashSet<string> sections;
                if (!sectionsByMeal.TryGetValue(m.Id, out sections)) sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                return new Candidate { Meal = m, Tag = tags[m.Id], Sections = sections };
            }).ToList();
        }

        private static bool SuitsItem(Candidate candidate, EventFeastPlanItem item)
        {
            string section = string.IsNullOrWhiteSpace(item.Section) ? "Main" : item.Section.Trim();
            if (!candidate.Sections.Contains(section)) return false;

            // Standard / vegetarian / halal dishes: the replacement must fall
            // in the same dietary group. Common dishes: any meal for that section.
            if (item.Category == FeastDishCategory.Standard
                || item.Category == FeastDishCategory.Vegetarian
                || item.Category == FeastDishCategory.Halal)
            {
                return FeastPlanService.DefaultCategory(section, candidate.Meal) == item.Category;
            }

            return true;
        }

        private ReplacementOption BestReplacement(List<Candidate> candidates, EventFeastPlanItem item, History history, Func<Candidate, bool> extraFilter)
        {
            Candidate best = null;
            FatigueInfo bestInfo = null;

            foreach (var candidate in candidates.Where(c => SuitsItem(c, item)))
            {
                if (extraFilter != null && !extraFilter(candidate)) continue;

                var info = FatigueOf(history, candidate.Meal.Id);

                if (best == null
                    || info.Score < bestInfo.Score
                    || (info.Score == bestInfo.Score && string.Compare(candidate.Meal.Name, best.Meal.Name, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = candidate;
                    bestInfo = info;
                }
            }

            if (best == null) return null;

            return new ReplacementOption
            {
                Meal = best.Meal,
                Colour = best.Tag.Colour,
                Texture = best.Tag.Texture,
                Fatigue = bestInfo
            };
        }

        // The swap the screens may offer: used to validate a posted swap
        public ReplacementOption FindReplacementOption(EventFeastPlan plan, EventFeastPlanItem item, int newMenuItemId)
        {
            var candidate = EligibleLibrary(plan).FirstOrDefault(c => c.Meal.Id == newMenuItemId && SuitsItem(c, item));
            if (candidate == null) return null;

            var history = LoadHistory(plan.Event.EventDate, plan.Id);
            return new ReplacementOption
            {
                Meal = candidate.Meal,
                Colour = candidate.Tag.Colour,
                Texture = candidate.Tag.Texture,
                Fatigue = FatigueOf(history, candidate.Meal.Id)
            };
        }

        // ============================================================
        // STEP 6 — colour and texture balance of each buffet section
        // ============================================================

        public BalanceResult Balance(EventFeastPlan plan)
        {
            var result = new BalanceResult();

            var ids = plan.Items.Where(i => i.MenuItemId.HasValue).Select(i => i.MenuItemId.Value).Distinct().ToList();
            result.Tags = _db.MenuItemBuffetTags.Where(t => ids.Contains(t.MenuItemId)).ToList().ToDictionary(t => t.MenuItemId);

            var history = LoadHistory(plan.Event.EventDate, plan.Id);
            var candidates = EligibleLibrary(plan);

            var ordered = FeastPlanService.InOrder(plan.Items).Where(i => i.MenuItemId.HasValue).ToList();
            result.Untagged = ordered.Where(i => !result.Tags.ContainsKey(i.MenuItemId.Value)).ToList();

            foreach (var group in ordered.GroupBy(i => string.IsNullOrWhiteSpace(i.Section) ? "Main" : i.Section.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var section = new BalanceSection { Section = group.Key };
                section.Dishes.AddRange(group);

                var tagged = section.Dishes.Where(d => result.Tags.ContainsKey(d.MenuItemId.Value)).ToList();

                foreach (var attribute in new[] { "Colour", "Texture" })
                {
                    Func<EventFeastPlanItem, string> valueOf = d =>
                        attribute == "Colour" ? result.Tags[d.MenuItemId.Value].Colour : result.Tags[d.MenuItemId.Value].Texture;

                    foreach (var shared in tagged.GroupBy(valueOf, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > FeastRules.BalanceMaxShared))
                    {
                        var finding = new BalanceFinding
                        {
                            Section = group.Key,
                            Attribute = attribute,
                            Value = shared.Key
                        };
                        finding.Dishes.AddRange(shared);

                        // Swap out the sharing dish with the highest fatigue score
                        // (favourites are never swapped)
                        var swappable = shared
                            .Where(d => d.Category != FeastDishCategory.Favourite)
                            .OrderByDescending(d => FatigueOf(history, d.MenuItemId.Value).Score)
                            .ThenByDescending(d => d.SortOrder)
                            .FirstOrDefault();

                        if (swappable != null)
                        {
                            finding.Replace = swappable;

                            // How many of each value the section has without that dish
                            var remaining = tagged.Where(d => d.Id != swappable.Id).ToList();

                            finding.Suggestion = BestReplacement(candidates, swappable, history, c =>
                            {
                                string value = attribute == "Colour" ? c.Tag.Colour : c.Tag.Texture;
                                if (string.Equals(value, shared.Key, StringComparison.OrdinalIgnoreCase)) return false;

                                int already = remaining.Count(d => string.Equals(valueOf(d), value, StringComparison.OrdinalIgnoreCase));
                                return already < FeastRules.BalanceMaxShared;   // the swap must not create a new imbalance
                            });
                        }

                        section.Findings.Add(finding);
                    }
                }

                result.Sections.Add(section);
            }

            return result;
        }

        // ============================================================
        // STEP 7 — consumption and waste
        // ============================================================

        public List<WasteRow> Waste(EventFeastPlan plan)
        {
            var rows = new List<WasteRow>();
            var since = SchoolClock.Today.AddDays(-FeastRules.LeftoverLookbackDays);

            var ids = plan.Items.Where(i => i.MenuItemId.HasValue).Select(i => i.MenuItemId.Value).Distinct().ToList();

            var records = _db.EventDishLeftovers
                .Where(l => ids.Contains(l.MenuItemId) && l.EventDate >= since && l.PreparedPortions > 0)
                .ToList()
                .GroupBy(l => l.MenuItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var item in FeastPlanService.InOrder(plan.Items))
            {
                var row = new WasteRow { Item = item, CurrentQuantity = item.TotalQuantity };

                List<EventDishLeftover> history;
                if (item.MenuItemId.HasValue && records.TryGetValue(item.MenuItemId.Value, out history) && history.Count > 0)
                {
                    int prepared = history.Sum(l => l.PreparedPortions);
                    int left = history.Sum(l => l.LeftoverPortions);

                    row.HasHistory = true;
                    row.LeftoverRecords = history.Count;
                    row.LeftoverPercent = prepared > 0 ? Math.Round(100m * left / prepared, 1) : 0m;
                }

                // What the quantity would be if this history were applied
                decimal trim = row.HasHistory ? Math.Min(FeastRules.MaxWasteTrimPercent, row.LeftoverPercent) : 0m;
                int basis = trim > 0m ? (int)Math.Ceiling(item.BaseQuantity * (100m - trim) / 100m) : item.BaseQuantity;
                row.OptimisedQuantity = FeastPlanService.WithBuffer(basis, plan.BufferPercent);

                rows.Add(row);
            }

            return rows;
        }

        // ============================================================
        // STEP 8 — what the reviewers see across steps 3 to 7
        // ============================================================

        public FeastReviewSummary Summarise(EventFeastPlan plan)
        {
            var favourites = ReadFavourites(plan);
            var fatigue = Fatigue(plan);
            var balance = Balance(plan);

            return new FeastReviewSummary
            {
                FavouriteRequests = favourites.Count,
                FavouritesAdded = plan.Items.Count(i => i.Category == FeastDishCategory.Favourite),
                FavouritesNotAdded = favourites.Count(f => !f.Selected && !f.AlreadyOnPlan),
                FlaggedDishes = fatigue.Count(f => f.Fatigue.Flagged),
                OpenBalanceFindings = balance.FindingCount,
                UntaggedDishes = balance.Untagged.Count,
                SwapsMade = plan.History.Count(h => h.Action == FeastPlanService.ActionSwap),
                DishesTrimmed = plan.Items.Count(i => i.OptimisedForWaste)
            };
        }

        // ============================================================
        // MEAL LIBRARY TAGS (colour and texture)
        // ============================================================

        public List<TagRow> TagTable()
        {
            var tags = _db.MenuItemBuffetTags.ToList().ToDictionary(t => t.MenuItemId);

            return _db.MenuItems
                .Where(m => m.IsActive)
                .OrderBy(m => m.Name)
                .ToList()
                .Select(m =>
                {
                    MenuItemBuffetTag tag;
                    tags.TryGetValue(m.Id, out tag);
                    return new TagRow { Meal = m, Tag = tag };
                })
                .ToList();
        }

        // Saves the chosen colour and texture for each posted meal.
        // A blank colour and texture removes the tag.
        public int SaveTags(IDictionary<int, KeyValuePair<string, string>> chosen)
        {
            int changed = 0;
            var existing = _db.MenuItemBuffetTags.ToList().ToDictionary(t => t.MenuItemId);

            foreach (var pair in chosen)
            {
                string colour = Canonical(pair.Value.Key, MenuItemBuffetTag.Colours);
                string texture = Canonical(pair.Value.Value, MenuItemBuffetTag.Textures);

                MenuItemBuffetTag tag;
                bool has = existing.TryGetValue(pair.Key, out tag);

                if (colour == null || texture == null)
                {
                    // Both are needed for a tag; a half-filled row is ignored,
                    // an empty row clears an existing tag
                    if (colour == null && texture == null && has)
                    {
                        _db.MenuItemBuffetTags.Remove(tag);
                        changed++;
                    }
                    continue;
                }

                if (!has)
                {
                    _db.MenuItemBuffetTags.Add(new MenuItemBuffetTag { MenuItemId = pair.Key, Colour = colour, Texture = texture });
                    changed++;
                }
                else if (tag.Colour != colour || tag.Texture != texture)
                {
                    tag.Colour = colour;
                    tag.Texture = texture;
                    tag.UpdatedAt = DateTime.UtcNow;
                    changed++;
                }
            }

            if (changed > 0) _db.SaveChanges();
            return changed;
        }

        private static string Canonical(string value, string[] allowed)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return allowed.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // Starter tags for meals that have none, from words in the meal's
        // name. They are suggestions the Meal Coordinator can change.
        public int ApplyStarterTags()
        {
            var tagged = new HashSet<int>(_db.MenuItemBuffetTags.Select(t => t.MenuItemId).ToList());
            int added = 0;

            foreach (var meal in _db.MenuItems.Where(m => m.IsActive).ToList().Where(m => !tagged.Contains(m.Id)))
            {
                var guess = GuessTags(meal.Name);
                _db.MenuItemBuffetTags.Add(new MenuItemBuffetTag
                {
                    MenuItemId = meal.Id,
                    Colour = guess.Key,
                    Texture = guess.Value
                });
                added++;
            }

            if (added > 0) _db.SaveChanges();
            return added;
        }

        private static readonly KeyValuePair<string, KeyValuePair<string, string>>[] KeywordTags =
        {
            Tag("salad", "Green", "Crunchy"), Tag("cabbage", "Green", "Crunchy"), Tag("spinach", "Green", "Soft"),
            Tag("broccoli", "Green", "Crunchy"), Tag("bean", "Green", "Soft"), Tag("pea", "Green", "Soft"),
            Tag("pumpkin", "Orange", "Soft"), Tag("butternut", "Orange", "Soft"), Tag("carrot", "Orange", "Soft"),
            Tag("curry", "Orange", "Soft"), Tag("tomato", "Red", "Juicy"), Tag("beetroot", "Purple", "Soft"),
            Tag("mash", "Cream", "Creamy"), Tag("potato", "Cream", "Soft"), Tag("cauliflower", "White", "Soft"),
            Tag("rice", "White", "Soft"), Tag("pap", "White", "Soft"), Tag("pasta", "Cream", "Chewy"),
            Tag("lasagne", "Orange", "Soft"), Tag("bread", "Golden", "Soft"), Tag("toast", "Golden", "Crispy"),
            Tag("chips", "Golden", "Crispy"), Tag("fried", "Golden", "Crispy"), Tag("roast", "Brown", "Tender"),
            Tag("stew", "Brown", "Tender"), Tag("beef", "Brown", "Tender"), Tag("lamb", "Brown", "Tender"),
            Tag("chicken", "Golden", "Tender"), Tag("fish", "Cream", "Flaky"), Tag("braai", "Brown", "Juicy"),
            Tag("pudding", "Cream", "Creamy"), Tag("cake", "Brown", "Soft"), Tag("custard", "Yellow", "Creamy"),
            Tag("fruit", "Red", "Juicy"), Tag("oats", "Cream", "Creamy"), Tag("pancake", "Golden", "Soft"),
            Tag("egg", "Yellow", "Soft"), Tag("cereal", "Golden", "Crunchy")
        };

        private static KeyValuePair<string, KeyValuePair<string, string>> Tag(string keyword, string colour, string texture)
        {
            return new KeyValuePair<string, KeyValuePair<string, string>>(keyword, new KeyValuePair<string, string>(colour, texture));
        }

        public static KeyValuePair<string, string> GuessTags(string mealName)
        {
            string name = NormaliseName(mealName);

            foreach (var entry in KeywordTags)
            {
                if (name.Contains(entry.Key)) return entry.Value;
            }

            return new KeyValuePair<string, string>("Brown", "Soft");
        }
    }
}
