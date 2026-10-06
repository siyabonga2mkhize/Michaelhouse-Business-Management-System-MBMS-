using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC19 — the actions behind SRS steps 3 to 7 and step 10
    // (leftovers). The rules they use are in FeastAnalysisService.
    // ============================================================
    public partial class FeastPlanService
    {
        // A plan the Meal Coordinator can still work on
        private FeastPlanResult RequireWorkingPlan(int planId, FeastPlanUser user, out EventFeastPlan plan)
        {
            plan = null;

            if (!FeastPlanRoles.CanCoordinate(user.Role))
            {
                return FeastPlanResult.Fail("Only the Meal Coordinator can work on a feast plan.");
            }

            plan = FindPlan(planId);
            if (plan == null) return FeastPlanResult.Fail("Feast plan not found.");
            if (plan.IsLocked) return FeastPlanResult.Fail("This plan has been approved and is locked.");

            if (!plan.IsEditable)
            {
                return FeastPlanResult.Fail("This plan is " + StatusLabel(plan.Status).ToLowerInvariant() + " and can't be changed.");
            }

            return null;
        }

        private void Recalculate(EventFeastPlan plan)
        {
            Calculate(plan.TotalGuests, plan.VegetarianGuests, plan.HalalGuests, plan.BufferPercent, plan.Items.ToList());
            plan.CalculatedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
        }

        // ============================================================
        // STEP 3 — the Meal Coordinator selects which Available cultural
        // favourites to add
        // ============================================================

        public FeastPlanResult SaveFavourites(int planId, IEnumerable<int> selectedMenuItemIds, FeastPlanUser user)
        {
            EventFeastPlan plan;
            var blocked = RequireWorkingPlan(planId, user, out plan);
            if (blocked != null) return blocked;

            var analysis = new FeastAnalysisService(_db);
            var rows = analysis.ReadFavourites(plan);
            var wanted = new HashSet<int>(selectedMenuItemIds ?? new List<int>());

            // Only an Available favourite that isn't already on the buffet can be added
            var addable = rows
                .Where(r => r.Status == FavouriteStatus.Available && r.MenuItemId.HasValue && !r.AlreadyOnPlan)
                .ToList();

            var changes = new List<string>();
            var remaining = new List<EventFeastPlanItem>();

            foreach (var item in plan.Items.ToList())
            {
                if (item.Category != FeastDishCategory.Favourite)
                {
                    remaining.Add(item);
                    continue;
                }

                bool keep = item.MenuItemId.HasValue
                    && wanted.Contains(item.MenuItemId.Value)
                    && addable.Any(a => a.MenuItemId == item.MenuItemId);

                if (keep)
                {
                    remaining.Add(item);
                }
                else
                {
                    changes.Add("removed " + item.DishName);
                    _db.EventFeastPlanItems.Remove(item);
                }
            }

            int order = 1000;
            foreach (var row in addable.Where(a => wanted.Contains(a.MenuItemId.Value)))
            {
                var existing = remaining.FirstOrDefault(i => i.Category == FeastDishCategory.Favourite && i.MenuItemId == row.MenuItemId);
                if (existing != null)
                {
                    existing.GuestsCovered = row.Guests;
                    continue;
                }

                var added = new EventFeastPlanItem
                {
                    FeastPlanId = plan.Id,
                    MenuItemId = row.MenuItemId,
                    DishName = Trim(row.MatchedMeal, 200),
                    Section = "Favourites",
                    Category = FeastDishCategory.Favourite,
                    GuestsCovered = row.Guests,
                    Source = "Favourite",
                    SortOrder = order++
                };

                plan.Items.Add(added);
                remaining.Add(added);
                changes.Add(string.Format("added {0} for {1} guest{2}", added.DishName, row.Guests, row.Guests == 1 ? "" : "s"));
            }

            Calculate(plan.TotalGuests, plan.VegetarianGuests, plan.HalalGuests, plan.BufferPercent, remaining);
            plan.CalculatedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;

            if (plan.Stage < FeastStage.Quantities && plan.Status == FeastPlanStatus.Drafting) plan.Stage = FeastStage.Quantities;

            AddHistory(plan, ActionFavourites, user, changes.Count > 0
                ? string.Join("; ", changes) + "."
                : "No cultural favourites added.");

            var saved = Save(plan, null);
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, changes.Count > 0
                ? "Cultural favourites saved and the quantities recalculated."
                : "No cultural favourites were added.");
        }

        // ============================================================
        // Continue to the next step (the Meal Coordinator confirms the
        // current step is done)
        // ============================================================

        public FeastPlanResult Advance(int planId, int fromStep, FeastPlanUser user)
        {
            EventFeastPlan plan;
            var blocked = RequireWorkingPlan(planId, user, out plan);
            if (blocked != null) return blocked;

            if (plan.Status != FeastPlanStatus.Drafting)
            {
                return FeastPlanResult.Fail("Steps 3 to 7 are only worked through while the plan is being drafted.");
            }

            if (fromStep < FeastStage.Favourites || fromStep > FeastStage.Waste)
            {
                return FeastPlanResult.Fail("Unknown step.");
            }

            if (fromStep > plan.Stage)
            {
                return FeastPlanResult.Fail("Finish step " + plan.Stage + " first.");
            }

            if (fromStep == FeastStage.Quantities)
            {
                var check = Check(plan);
                if (check.Problems.Count > 0)
                {
                    return FeastPlanResult.Fail("Fix these before continuing: " + string.Join(" ", check.Problems));
                }
            }

            if (fromStep == FeastStage.Waste)
            {
                bool run = plan.History.Any(h => h.Action == ActionWasteOptimised && h.Revision == plan.Revision);
                if (!run)
                {
                    return FeastPlanResult.Fail("Run the waste optimisation first. It is the Meal Coordinator's request to the system in step 7.");
                }
            }

            if (plan.Stage == fromStep)
            {
                plan.Stage = fromStep + 1;
                plan.UpdatedAt = DateTime.UtcNow;
                AddHistory(plan, ActionStepDone, user, "Step " + fromStep + " completed.");

                var saved = Save(plan, null);
                if (saved != null) return saved;
            }

            return FeastPlanResult.Ok(plan.Id, "");
        }

        // ============================================================
        // STEPS 5 and 6 — accept a suggested swap; quantities are
        // recalculated
        // ============================================================

        public FeastPlanResult ApplySwap(int planId, int itemId, int newMenuItemId, string kind, FeastPlanUser user)
        {
            EventFeastPlan plan;
            var blocked = RequireWorkingPlan(planId, user, out plan);
            if (blocked != null) return blocked;

            var item = plan.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return FeastPlanResult.Fail("That dish isn't on this plan.");

            if (item.Category == FeastDishCategory.Favourite)
            {
                return FeastPlanResult.Fail("A guest's cultural favourite isn't swapped. Remove it in step 3 instead.");
            }

            var option = new FeastAnalysisService(_db).FindReplacementOption(plan, item, newMenuItemId);
            if (option == null)
            {
                return FeastPlanResult.Fail("That meal isn't a suitable replacement for this dish.");
            }

            string before = item.DishName;
            bool balance = string.Equals(kind, "Balance", StringComparison.OrdinalIgnoreCase);

            item.MenuItemId = option.Meal.Id;
            item.DishName = Trim(option.Meal.Name, 200);
            item.Source = balance ? "Balance swap" : "Fatigue swap";
            item.OptimisedForWaste = false;
            item.LeftoverRatePercent = 0m;

            Recalculate(plan);

            AddHistory(plan, ActionSwap, user, string.Format("{0} swap: {1} → {2}; quantities recalculated.",
                balance ? "Colour/texture" : "Menu fatigue", before, item.DishName));

            var saved = Save(plan, null);
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, string.Format("{0} replaced with {1}. The quantities were recalculated.", before, item.DishName));
        }

        // ============================================================
        // STEP 7 — the Meal Coordinator prompts the system to ensure
        // maximum consumption and no unnecessary waste
        // ============================================================

        public FeastPlanResult OptimiseWaste(int planId, FeastPlanUser user)
        {
            EventFeastPlan plan;
            var blocked = RequireWorkingPlan(planId, user, out plan);
            if (blocked != null) return blocked;

            var rows = new FeastAnalysisService(_db).Waste(plan);

            int before = plan.Items.Sum(i => i.TotalQuantity);
            int trimmed = 0;

            foreach (var row in rows)
            {
                if (row.HasHistory && row.LeftoverPercent > 0m)
                {
                    row.Item.OptimisedForWaste = true;
                    row.Item.LeftoverRatePercent = row.LeftoverPercent;
                    trimmed++;
                }
                else
                {
                    row.Item.OptimisedForWaste = false;
                    row.Item.LeftoverRatePercent = 0m;
                }
            }

            Recalculate(plan);

            int after = plan.Items.Sum(i => i.TotalQuantity);

            string summary = trimmed == 0
                ? "Waste optimisation run. No leftover records exist for these dishes yet, so no quantities changed."
                : string.Format("Waste optimisation run on {0} dish{1} with leftover history: {2} → {3} portions ({4} fewer).",
                    trimmed, trimmed == 1 ? "" : "es", before, after, before - after);

            AddHistory(plan, ActionWasteOptimised, user, summary);

            var saved = Save(plan, null);
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, summary);
        }

        // ============================================================
        // STEP 10 — after the event the kitchen records leftovers per
        // dish. These feed future Menu Fatigue Scores (step 5) and the
        // waste optimisation (step 7).
        // ============================================================

        public FeastPlanResult SaveLeftovers(int planId, IDictionary<int, int> leftoverByItemId, FeastPlanUser user)
        {
            if (!FeastPlanRoles.CanRecordLeftovers(user.Role))
            {
                return FeastPlanResult.Fail("Only the kitchen can record leftovers.");
            }

            var plan = FindPlan(planId);
            if (plan == null) return FeastPlanResult.Fail("Feast plan not found.");

            if (plan.Status != FeastPlanStatus.Approved)
            {
                return FeastPlanResult.Fail("Leftovers are recorded for an approved feast plan.");
            }

            if (plan.Event.EventDate.Date > SchoolClock.Today)
            {
                return FeastPlanResult.Fail("Leftovers are recorded after the event, from " + plan.Event.EventDate.ToString("dd MMM yyyy") + ".");
            }

            var existing = _db.EventDishLeftovers.Where(l => l.FeastPlanId == plan.Id).ToList();
            int recorded = 0;

            foreach (var item in plan.Items.Where(i => i.MenuItemId.HasValue))
            {
                int left;
                if (leftoverByItemId == null || !leftoverByItemId.TryGetValue(item.Id, out left)) continue;

                if (left < 0 || left > item.TotalQuantity)
                {
                    return FeastPlanResult.Fail(string.Format("{0}: leftovers must be between 0 and the {1} portions prepared.", item.DishName, item.TotalQuantity));
                }

                var record = existing.FirstOrDefault(l => l.MenuItemId == item.MenuItemId.Value);
                if (record == null)
                {
                    record = new EventDishLeftover
                    {
                        FeastPlanId = plan.Id,
                        MenuItemId = item.MenuItemId.Value
                    };
                    _db.EventDishLeftovers.Add(record);
                }

                record.DishName = Trim(item.DishName, 200);
                record.EventDate = plan.Event.EventDate.Date;
                record.PreparedPortions = item.TotalQuantity;
                record.LeftoverPortions = left;
                record.RecordedByUserId = user.UserId;
                record.RecordedAt = DateTime.UtcNow;
                record.IsDemo = false;
                recorded++;
            }

            AddHistory(plan, ActionLeftovers, user, recorded + " dish" + (recorded == 1 ? "" : "es") + " recorded.");
            _db.SaveChanges();

            return FeastPlanResult.Ok(plan.Id, "Leftovers saved. They will count towards future Menu Fatigue Scores.");
        }

        public List<EventDishLeftover> LeftoversFor(int planId)
        {
            return _db.EventDishLeftovers.Where(l => l.FeastPlanId == planId).ToList();
        }

        // ============================================================
        // Sample history, so the fatigue and waste steps can be shown
        // before any real event has been recorded. Clearly marked as
        // sample data and removable.
        // ============================================================

        public FeastPlanResult AddDemoHistory(int planId, FeastPlanUser user)
        {
            EventFeastPlan plan;
            var blocked = RequireWorkingPlan(planId, user, out plan);
            if (blocked != null) return blocked;

            var dishes = plan.Items
                .Where(i => i.MenuItemId.HasValue && i.Category != FeastDishCategory.Favourite)
                .OrderBy(i => i.SortOrder)
                .Take(4)
                .ToList();

            if (dishes.Count == 0) return FeastPlanResult.Fail("There are no dishes on the plan to add sample history for.");

            ClearDemoRows();

            var eventDate = plan.Event.EventDate.Date;

            // dish 1: served twice, 30% left over   -> flagged (served twice)
            // dish 2: served once, 55% left over    -> score 52, trimmed in step 7
            // dish 3: served once, 10% left over    -> low score
            // dish 4: no history
            var pattern = new[]
            {
                new[] { new[] { -7, 100, 30 }, new[] { -14, 100, 30 } },
                new[] { new[] { -10, 100, 55 } },
                new[] { new[] { -21, 80, 8 } },
                new int[][] { }
            };

            int rows = 0;
            for (int i = 0; i < dishes.Count; i++)
            {
                foreach (var p in pattern[i])
                {
                    _db.EventDishLeftovers.Add(new EventDishLeftover
                    {
                        MenuItemId = dishes[i].MenuItemId.Value,
                        DishName = Trim(dishes[i].DishName, 200),
                        EventDate = eventDate.AddDays(p[0]),
                        PreparedPortions = p[1],
                        LeftoverPortions = p[2],
                        RecordedByUserId = user.UserId,
                        IsDemo = true
                    });
                    rows++;
                }
            }

            AddHistory(plan, ActionDemoHistory, user, "Added " + rows + " sample leftover records (demo data).");
            var saved = Save(plan, null);
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, "Sample history added. It is marked as demo data and can be removed.");
        }

        public FeastPlanResult RemoveDemoHistory(int planId, FeastPlanUser user)
        {
            EventFeastPlan plan;
            var blocked = RequireWorkingPlan(planId, user, out plan);
            if (blocked != null) return blocked;

            int removed = ClearDemoRows();

            AddHistory(plan, ActionDemoHistory, user, "Removed " + removed + " sample leftover records.");
            var saved = Save(plan, null);
            if (saved != null) return saved;

            return FeastPlanResult.Ok(plan.Id, "Sample history removed.");
        }

        private int ClearDemoRows()
        {
            var demo = _db.EventDishLeftovers.Where(l => l.IsDemo).ToList();
            if (demo.Count > 0) _db.EventDishLeftovers.RemoveRange(demo);
            return demo.Count;
        }

        public bool HasDemoHistory()
        {
            return _db.EventDishLeftovers.Any(l => l.IsDemo);
        }
    }
}
