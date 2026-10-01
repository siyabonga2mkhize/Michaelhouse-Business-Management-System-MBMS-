using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Kitchen ingredient issue — the point where stock goes down.
    //
    // Weekly menu: once per day. The Chef opens e.g. "Wednesday 07
    // Oct", sees the ingredients the kitchen plan needs for that
    // day's meals (built from students' submitted choices), with what
    // is in stock, adjusts to what was actually used, and confirms.
    // Events: once per event, from the feast plan (RSVP numbers).
    //
    // Farm stock is used first, then bought-in (external) stock.
    // A day / event can only be issued once (KitchenIngredientIssue
    // .IssueKey is unique). Menu scheduling and meal selection never
    // reduce stock.
    // ============================================================

    public class KitchenIssueService
    {
        // How early food can be issued: the day before (prep), and up
        // to 2 days before an event (feast plans start 2 days ahead)
        public const int MenuDaysAhead = 1;
        public const int EventDaysAhead = 2;

        private readonly DBContextClass _db;
        private readonly Func<DateTime> _now;

        public KitchenIssueService(DBContextClass db)
            : this(db, null)
        {
        }

        public KitchenIssueService(DBContextClass db, Func<DateTime> now)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _now = now ?? (() => SchoolClock.Now);
        }

        public static string MenuDayKey(int menuId, DateTime date)
        {
            return "MENU-" + menuId + "-" + date.ToString("yyyyMMdd");
        }

        public static string EventKey(int eventId)
        {
            return "EVENT-" + eventId;
        }

        public KitchenIngredientIssue FindIssue(string key)
        {
            return _db.KitchenIngredientIssues.FirstOrDefault(i => i.IssueKey == key);
        }

        // ============================================================
        // WEEKLY MENU DAY
        // ============================================================

        public KitchenIssueViewModel ForMenuDay(int menuId, DateTime date)
        {
            date = date.Date;
            var menu = _db.MealMenus.FirstOrDefault(m => m.Id == menuId);
            if (menu == null) throw new InventoryException("Menu not found.");

            var vm = new KitchenIssueViewModel
            {
                Title = date.ToString("dddd dd MMMM"),
                IssueKey = MenuDayKey(menuId, date),
                MealMenuId = menuId,
                Date = date
            };

            if (menu.MenuStatus != MenuStatus.Accepted)
                return CannotIssue(vm, "Ingredients can only be issued for a published menu.");
            if (date < menu.StartDate.Date || date > menu.EndDate.Date)
                return CannotIssue(vm, "That day isn't on this menu.");

            var plan = new MenuSchedulingService(_db).BuildProductionPlan(menuId);
            var day = plan.Days.FirstOrDefault(d => d.Date.Date == date);
            if (day == null) return CannotIssue(vm, "That day isn't on this menu.");

            // Meals per ingredient for the labels
            var usedFor = new Dictionary<int, List<string>>();
            foreach (var slot in day.Slots)
                foreach (var task in slot.Tasks)
                    foreach (var line in task.Ingredients)
                    {
                        if (!usedFor.ContainsKey(line.IngredientId)) usedFor[line.IngredientId] = new List<string>();
                        var label = slot.MealSlot + ": " + task.DishName;
                        if (!usedFor[line.IngredientId].Contains(label)) usedFor[line.IngredientId].Add(label);
                    }

            FillLines(vm, day.DayTotalIngredients.Select(l => Tuple.Create(l.IngredientId, l.TotalRequired)), usedFor);
            FillIssued(vm);

            if (vm.Issue == null)
            {
                if (date > _now().Date.AddDays(MenuDaysAhead))
                    return CannotIssue(vm, "Ingredients for " + date.ToString("ddd dd MMM") + " can be issued from the day before.");
                if (!day.SelectionsFinal)
                    return CannotIssue(vm, "Students can still change their meals for this day, so the quantities aren't final yet.");
                if (vm.Lines.Count == 0)
                    return CannotIssue(vm, "No meals have been chosen for this day, so there's nothing to issue.");
                vm.CanIssue = true;
            }

            return vm;
        }

        public KitchenIngredientIssue IssueMenuDay(int menuId, DateTime date, IDictionary<int, decimal> quantities, string notes, int userId)
        {
            var vm = ForMenuDay(menuId, date);
            return Issue(vm, quantities, notes, userId, "KP-" + menuId + "-" + date.ToString("yyyyMMdd"));
        }

        // ============================================================
        // EVENT
        // ============================================================

        public KitchenIssueViewModel ForEvent(int eventId)
        {
            var evt = _db.CafeteriaEvents
                .Include(e => e.Venue)
                .Include(e => e.MenuTemplate)
                .FirstOrDefault(e => e.Id == eventId);
            if (evt == null) throw new InventoryException("Event not found.");

            var vm = new KitchenIssueViewModel
            {
                Title = evt.EventName + " — " + evt.EventDate.ToString("dddd dd MMMM"),
                IssueKey = EventKey(eventId),
                CafeteriaEventId = eventId,
                Date = evt.EventDate.Date
            };

            if (evt.Status != EventStatus.FeastPlanGenerated && evt.Status != EventStatus.RsvpClosed
                && evt.Status != EventStatus.InProgress && evt.Status != EventStatus.Completed)
                return CannotIssue(vm, "Ingredients are issued once RSVPs have closed and the kitchen plan is ready.");

            var plan = new EventBuffetService(_db).BuildFeastPlan(evt);

            var usedFor = new Dictionary<int, List<string>>();
            foreach (var dish in plan.Dishes)
                foreach (var line in dish.Ingredients)
                {
                    if (!usedFor.ContainsKey(line.IngredientId)) usedFor[line.IngredientId] = new List<string>();
                    if (!usedFor[line.IngredientId].Contains(dish.DishName)) usedFor[line.IngredientId].Add(dish.DishName);
                }

            FillLines(vm, plan.TotalIngredients.Select(l => Tuple.Create(l.IngredientId, l.TotalRequired)), usedFor);
            FillIssued(vm);

            if (vm.Issue == null)
            {
                if (evt.EventDate.Date > _now().Date.AddDays(EventDaysAhead))
                    return CannotIssue(vm, "Event ingredients can be issued from " + EventDaysAhead + " days before the event.");
                if (vm.Lines.Count == 0)
                    return CannotIssue(vm, "The event's meals have no recipe ingredients to issue.");
                vm.CanIssue = true;
            }

            return vm;
        }

        public KitchenIngredientIssue IssueEvent(int eventId, IDictionary<int, decimal> quantities, string notes, int userId)
        {
            var vm = ForEvent(eventId);
            return Issue(vm, quantities, notes, userId, "EV-" + eventId);
        }

        // ============================================================
        // ISSUE (shared)
        // quantities: ingredient id → amount actually used, in the
        // ingredient's own unit. Only ingredients on the plan; never
        // more than is in stock.
        // ============================================================

        private KitchenIngredientIssue Issue(KitchenIssueViewModel vm, IDictionary<int, decimal> quantities, string notes, int userId, string reference)
        {
            if (vm.Issue != null)
                throw new InventoryException("Ingredients for " + vm.Title + " have already been issued.");
            if (!vm.CanIssue)
                throw new InventoryException(vm.CannotIssueReason ?? "Ingredients can't be issued yet.");

            quantities = quantities ?? new Dictionary<int, decimal>();
            var byId = vm.Lines.ToDictionary(l => l.IngredientId);

            foreach (var kv in quantities)
            {
                KitchenIssueLine line;
                if (!byId.TryGetValue(kv.Key, out line))
                    throw new InventoryException("One of the ingredients isn't on this kitchen plan.");
                if (kv.Value < 0m)
                    throw new InventoryException(line.Name + ": the quantity used can't be negative.");
                if (Math.Round(kv.Value, 2) > line.Available)
                    throw new InventoryException(string.Format("{0}: only {1} is in stock.", line.Name, IngredientUnits.Format(line.Available, line.Unit)));
            }

            var toIssue = quantities.Where(kv => Math.Round(kv.Value, 2) > 0m).ToList();
            if (toIssue.Count == 0)
                throw new InventoryException("Enter the quantities used.");

            var inventory = new IngredientInventoryService(_db, _now);

            using (var tx = _db.Database.BeginTransaction())
            {
                try
                {
                    var issue = new KitchenIngredientIssue
                    {
                        IssueKey = vm.IssueKey,
                        MealMenuId = vm.MealMenuId,
                        Date = vm.Date,
                        CafeteriaEventId = vm.CafeteriaEventId,
                        IssuedByUserId = userId,
                        IssuedAt = DateTime.UtcNow,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : (notes.Trim().Length > 500 ? notes.Trim().Substring(0, 500) : notes.Trim())
                    };
                    _db.KitchenIngredientIssues.Add(issue);
                    _db.SaveChanges();   // unique key: a second issue fails here

                    foreach (var kv in toIssue)
                    {
                        var line = byId[kv.Key];
                        decimal qty = Math.Round(kv.Value, 2);

                        // Farm first, then bought-in stock
                        var ingredient = _db.Ingredients.Find(kv.Key);
                        _db.Entry(ingredient).Reload();

                        decimal fromFarm = Math.Min(ingredient.FarmAvailableQuantity, qty);
                        decimal fromExternal = qty - fromFarm;

                        string note = "Used for " + vm.Title;
                        if (fromFarm > 0m)
                            inventory.Apply(kv.Key, StockTransactionType.KitchenIssue, StockSource.Farm, -fromFarm, reference, note, userId, kitchenIssueId: issue.Id);
                        if (fromExternal > 0m)
                            inventory.Apply(kv.Key, StockTransactionType.KitchenIssue, StockSource.External, -fromExternal, reference, note, userId, kitchenIssueId: issue.Id);
                    }

                    tx.Commit();
                    return issue;
                }
                catch (DbUpdateException)
                {
                    tx.Rollback();
                    throw new InventoryException("Ingredients for " + vm.Title + " have already been issued.");
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        // The issue form's quantities (qty_{ingredientId}, in kg / L),
        // converted with each ingredient's own unit. Only ingredients on
        // the plan are read.
        public static Dictionary<int, decimal> ReadQuantities(Func<string, string> formValue, KitchenIssueViewModel vm)
        {
            var result = new Dictionary<int, decimal>();

            foreach (var line in vm.Lines)
            {
                var raw = formValue("qty_" + line.IngredientId);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var qty = IngredientUnits.ParseQuantity(raw);
                if (!qty.HasValue) throw new InventoryException(line.Name + ": enter the quantity used as a number.");

                result[line.IngredientId] = IngredientUnits.ToBase(qty.Value, line.Unit);
            }

            return result;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void FillLines(KitchenIssueViewModel vm, IEnumerable<Tuple<int, decimal>> required, Dictionary<int, List<string>> usedFor)
        {
            var totals = required
                .GroupBy(r => r.Item1)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Item2));

            var ids = totals.Keys.ToList();
            var ingredients = _db.Ingredients.Where(i => ids.Contains(i.Id)).ToList();

            foreach (var i in ingredients.OrderBy(i => i.Name))
            {
                vm.Lines.Add(new KitchenIssueLine
                {
                    IngredientId = i.Id,
                    Name = i.Name,
                    Unit = i.Unit,
                    Required = Math.Round(totals[i.Id], 2),
                    Farm = i.IsActive ? i.FarmAvailableQuantity : 0m,
                    External = i.IsActive ? i.ExternalAvailableQuantity : 0m,
                    UsedFor = usedFor.ContainsKey(i.Id) ? usedFor[i.Id] : new List<string>()
                });
            }
        }

        private void FillIssued(KitchenIssueViewModel vm)
        {
            vm.Issue = FindIssue(vm.IssueKey);
            if (vm.Issue == null) return;

            int issueId = vm.Issue.Id;
            vm.Issued = _db.IngredientStockTransactions
                .Include(t => t.Ingredient)
                .Where(t => t.KitchenIssueId == issueId)
                .OrderBy(t => t.Ingredient.Name)
                .ToList();

            int byUser = vm.Issue.IssuedByUserId;
            vm.IssuedByName = _db.Users.Where(u => u.UserId == byUser).Select(u => u.Name).FirstOrDefault();
            vm.CanIssue = false;
        }

        private static KitchenIssueViewModel CannotIssue(KitchenIssueViewModel vm, string reason)
        {
            vm.CanIssue = false;
            vm.CannotIssueReason = reason;
            return vm;
        }
    }
}
