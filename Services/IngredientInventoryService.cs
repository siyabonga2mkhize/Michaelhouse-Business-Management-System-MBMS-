using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Cafeteria ingredient stock
    //
    // The ONLY place that changes ingredient stock (Apply). Every
    // change updates Ingredient.FarmAvailableQuantity or
    // ExternalAvailableQuantity with a single SQL statement that
    // refuses to go below zero, and writes an
    // IngredientStockTransaction saying why.
    //
    // Also answers the manager's questions: what's in stock, what is
    // low (available <= reorder level), what is out, what's on order,
    // and what upcoming kitchen plans need that we don't have — which
    // is a different list from "low stock".
    //
    // Menu scheduling and student meal selection never change stock;
    // only deliveries, farm produce, kitchen issues and adjustments do.
    // ============================================================

    public class InventoryException : Exception
    {
        public InventoryException(string message) : base(message) { }
    }

    // Quantities are stored in the ingredient's own unit (g / ml, as the
    // recipes use). People order and count in kg / litres.
    public static class IngredientUnits
    {
        public static string DisplayUnit(string unit)
        {
            switch (Normalise(unit))
            {
                case "g": return "kg";
                case "ml": return "L";
                default: return (unit ?? "").Trim();
            }
        }

        public static decimal Factor(string unit)
        {
            var u = Normalise(unit);
            return u == "g" || u == "ml" ? 1000m : 1m;
        }

        public static decimal ToBase(decimal displayQuantity, string unit)
        {
            return Math.Round(displayQuantity * Factor(unit), 2);
        }

        public static decimal ToDisplay(decimal baseQuantity, string unit)
        {
            return Math.Round(baseQuantity / Factor(unit), 3);
        }

        public static string Format(decimal baseQuantity, string unit)
        {
            return ToDisplay(baseQuantity, unit).ToString("#,##0.###") + " " + DisplayUnit(unit);
        }

        // An invoice quantity in the ingredient's own unit; null when the
        // units can't be converted (e.g. "case" vs grams)
        public static decimal? Convert(decimal quantity, string fromUnit, string ingredientUnit)
        {
            var from = Normalise(fromUnit);
            var to = Normalise(ingredientUnit);

            if (from == "") return null;
            if (from == to) return quantity;

            if (to == "g")
            {
                if (from == "kg") return quantity * 1000m;
            }
            else if (to == "ml")
            {
                if (from == "l") return quantity * 1000m;
            }
            else if (to == "kg")
            {
                if (from == "g") return quantity / 1000m;
            }
            else if (to == "l")
            {
                if (from == "ml") return quantity / 1000m;
            }

            return null;
        }

        // A number typed in a form: "35.5" or "35,5" (en-ZA), no thousands separators
        public static decimal? ParseQuantity(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            decimal value;
            return decimal.TryParse(raw.Trim().Replace(" ", "").Replace(',', '.'),
                System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out value) ? value : (decimal?)null;
        }

        public static string Normalise(string unit)
        {
            var u = (unit ?? "").Trim().ToLowerInvariant().TrimEnd('.');

            switch (u)
            {
                case "g": case "gr": case "gram": case "grams": case "grm": return "g";
                case "kg": case "kgs": case "kilo": case "kilos": case "kilogram": case "kilograms": return "kg";
                case "ml": case "millilitre": case "millilitres": case "milliliter": case "milliliters": return "ml";
                case "l": case "lt": case "ltr": case "ltrs": case "litre": case "litres": case "liter": case "liters": return "l";
                case "ea": case "each": case "unit": case "units": case "pc": case "pcs": case "piece": case "pieces": return "each";
                default: return u;
            }
        }
    }

    public class IngredientStockLine
    {
        public Ingredient Ingredient { get; set; }
        public decimal Farm { get; set; }
        public decimal External { get; set; }
        public decimal Available { get { return Farm + External; } }
        public decimal ReorderLevel { get; set; }
        public decimal? TargetStockLevel { get; set; }

        // Still expected on orders already sent to suppliers
        public decimal OnOrder { get; set; }

        public string PreferredSupplier { get; set; }

        public bool IsOutOfStock { get { return Ingredient.IsActive && Available <= 0m; } }
        public bool IsLowStock { get { return Ingredient.IsActive && !IsOutOfStock && Available <= ReorderLevel; } }

        public string Status
        {
            get
            {
                if (!Ingredient.IsActive) return "Inactive";
                if (IsOutOfStock) return "Out of stock";
                if (IsLowStock) return "Low stock";
                return "In stock";
            }
        }

        // What to order to get back to the target (or twice the reorder
        // level when no target is set), allowing for what's on order
        public decimal SuggestedOrder
        {
            get
            {
                decimal target = TargetStockLevel ?? ReorderLevel * 2m;
                return Math.Max(0m, target - Available - OnOrder);
            }
        }
    }

    // An ingredient needed by upcoming kitchen plans
    public class UpcomingRequirement
    {
        public UpcomingRequirement()
        {
            NeededFor = new List<string>();
        }

        public Ingredient Ingredient { get; set; }
        public decimal Required { get; set; }
        public decimal Available { get; set; }
        public decimal OnOrder { get; set; }
        public List<string> NeededFor { get; set; }

        public decimal Shortfall { get { return Math.Max(0m, Required - Available); } }
        public decimal ShortfallAfterOrders { get { return Math.Max(0m, Required - Available - OnOrder); } }
    }

    public class InventorySummary
    {
        public int TotalIngredients { get; set; }
        public int LowStock { get; set; }
        public int OutOfStock { get; set; }
        public int UpcomingShortages { get; set; }
        public int PendingOrders { get; set; }
        public int ExpectedDeliveries { get; set; }
    }

    public class IngredientInventoryService
    {
        public static readonly IngredientOrderStatus[] OpenStatuses =
        {
            IngredientOrderStatus.Pending,
            IngredientOrderStatus.Confirmed,
            IngredientOrderStatus.PartiallyFulfilled
        };

        private readonly DBContextClass _db;
        private readonly Func<DateTime> _now;

        public IngredientInventoryService(DBContextClass db)
            : this(db, null)
        {
        }

        public IngredientInventoryService(DBContextClass db, Func<DateTime> now)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _now = now ?? (() => SchoolClock.Now);
        }

        // ============================================================
        // STOCK CHANGES — every change comes through here
        // ============================================================

        public IngredientStockTransaction Apply(
            int ingredientId,
            StockTransactionType type,
            StockSource source,
            decimal quantity,
            string reference,
            string notes,
            int? userId,
            int? purchaseOrderId = null,
            int? deliveryId = null,
            int? kitchenIssueId = null,
            StockAdjustmentReason? adjustmentReason = null)
        {
            quantity = Math.Round(quantity, 2);
            if (quantity == 0m) return null;

            var ingredient = _db.Ingredients.Find(ingredientId);
            if (ingredient == null)
            {
                throw new InventoryException("Ingredient not found.");
            }

            // One statement: add/remove, and never below zero
            string column = source == StockSource.Farm ? "FarmAvailableQuantity" : "ExternalAvailableQuantity";
            int changed = _db.Database.ExecuteSqlCommand(
                "UPDATE dbo.Ingredients SET " + column + " = " + column + " + @q WHERE Id = @id AND " + column + " + @q >= 0",
                new SqlParameter("@q", quantity),
                new SqlParameter("@id", ingredientId));

            if (changed == 0)
            {
                throw new InventoryException(string.Format(
                    "Not enough {0} in {1} stock for this change.",
                    ingredient.Name, source == StockSource.Farm ? "farm" : "bought-in"));
            }

            _db.Entry(ingredient).Reload();

            var tx = new IngredientStockTransaction
            {
                IngredientId = ingredientId,
                Type = type,
                Source = source,
                AdjustmentReason = type == StockTransactionType.Adjustment ? adjustmentReason : null,
                Quantity = quantity,
                SourceQuantityAfter = source == StockSource.Farm ? ingredient.FarmAvailableQuantity : ingredient.ExternalAvailableQuantity,
                TotalAfter = ingredient.FarmAvailableQuantity + ingredient.ExternalAvailableQuantity,
                Reference = Trim(reference, 60),
                Notes = Trim(notes, 500),
                PurchaseOrderId = purchaseOrderId,
                DeliveryId = deliveryId,
                KitchenIssueId = kitchenIssueId,
                CreatedByUserId = userId > 0 ? userId : null,
                CreatedAt = DateTime.UtcNow
            };

            _db.IngredientStockTransactions.Add(tx);
            _db.SaveChanges();
            return tx;
        }

        // Produce from the school farm (no purchase order)
        public void AddFarmProduce(int ingredientId, decimal quantity, string notes, int userId)
        {
            var ingredient = ActiveIngredient(ingredientId);

            if (quantity <= 0m)
                throw new InventoryException("Enter how much farm produce was received.");

            Apply(ingredient.Id, StockTransactionType.FarmProduce, StockSource.Farm, quantity,
                "FARM-" + _now().ToString("yyyyMMdd"), notes, userId);
        }

        // Stock count / correction: sets one source to the counted amount
        public IngredientStockTransaction Adjust(int ingredientId, StockSource source, decimal countedQuantity, string reason, int userId)
        {
            var ingredient = _db.Ingredients.Find(ingredientId);
            if (ingredient == null) throw new InventoryException("Ingredient not found.");

            if (countedQuantity < 0m)
                throw new InventoryException("A stock count can't be negative.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InventoryException("Please give a reason for the adjustment.");

            decimal current = source == StockSource.Farm ? ingredient.FarmAvailableQuantity : ingredient.ExternalAvailableQuantity;
            decimal change = Math.Round(countedQuantity, 2) - current;

            if (change == 0m)
                throw new InventoryException("The counted quantity is the same as the recorded stock.");

            return AdjustBy(ingredientId, source, change, StockAdjustmentReason.StockCountCorrection, reason, userId);
        }

        // ============================================================
        // MANUAL ADJUSTMENT — damaged, spoiled, lost, count correction…
        // change: + adds, − removes (ingredient's own unit). Never
        // overwrites stock: it goes through Apply like every other
        // change, so the transaction records the reason, who and when.
        // ============================================================

        public IngredientStockTransaction AdjustBy(int ingredientId, StockSource source, decimal change,
            StockAdjustmentReason reason, string notes, int userId)
        {
            var ingredient = _db.Ingredients.Find(ingredientId);
            if (ingredient == null) throw new InventoryException("Ingredient not found.");

            if (!Enum.IsDefined(typeof(StockAdjustmentReason), reason))
                throw new InventoryException("Choose a reason for the adjustment.");

            change = Math.Round(change, 2);
            if (change == 0m)
                throw new InventoryException("Enter the quantity to add or remove.");

            notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            if (reason == StockAdjustmentReason.Other && notes == null)
                throw new InventoryException("Please describe the reason for an \"Other\" adjustment.");

            decimal current = source == StockSource.Farm ? ingredient.FarmAvailableQuantity : ingredient.ExternalAvailableQuantity;
            if (current + change < 0m)
                throw new InventoryException(string.Format(
                    "Adjustment would result in negative stock: {0} {1} stock is {2}.",
                    ingredient.Name, source == StockSource.Farm ? "farm" : "bought-in",
                    IngredientUnits.Format(current, ingredient.Unit)));

            var label = ReasonLabel(reason);
            return Apply(ingredient.Id, StockTransactionType.Adjustment, source, change,
                "ADJ-" + _now().ToString("yyyyMMdd"),
                notes == null ? label : label + ": " + notes,
                userId,
                adjustmentReason: reason);
        }

        public static string ReasonLabel(StockAdjustmentReason reason)
        {
            switch (reason)
            {
                case StockAdjustmentReason.Damaged: return "Damaged";
                case StockAdjustmentReason.Spoiled: return "Spoiled / expired";
                case StockAdjustmentReason.Lost: return "Lost";
                case StockAdjustmentReason.StockCountCorrection: return "Stock count correction";
                default: return "Other";
            }
        }

        public void SetLevels(int ingredientId, decimal reorderLevel, decimal? targetLevel)
        {
            var ingredient = _db.Ingredients.Find(ingredientId);
            if (ingredient == null) throw new InventoryException("Ingredient not found.");

            if (reorderLevel < 0m || (targetLevel.HasValue && targetLevel.Value < 0m))
                throw new InventoryException("Levels can't be negative.");

            if (targetLevel.HasValue && targetLevel.Value > 0m && targetLevel.Value < reorderLevel)
                throw new InventoryException("The target level should be at least the reorder level.");

            ingredient.ReorderLevel = Math.Round(reorderLevel, 2);
            ingredient.TargetStockLevel = targetLevel.HasValue && targetLevel.Value > 0m ? Math.Round(targetLevel.Value, 2) : (decimal?)null;
            _db.SaveChanges();
        }

        public Ingredient ActiveIngredient(int ingredientId)
        {
            var ingredient = _db.Ingredients.Find(ingredientId);
            if (ingredient == null || !ingredient.IsActive)
                throw new InventoryException("That ingredient doesn't exist or is no longer active.");
            return ingredient;
        }

        // ============================================================
        // STOCK LEVELS
        // ============================================================

        public List<IngredientStockLine> GetStockLines()
        {
            var ingredients = _db.Ingredients.OrderBy(i => i.Name).ToList();
            var onOrder = OnOrderByIngredient();

            var preferred = _db.IngredientSuppliers
                .Include(x => x.Supplier)
                .Where(x => x.IsActive && x.Supplier.IsActive)
                .ToList()
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPreferred).First().Supplier.Name);

            return ingredients.Select(i => new IngredientStockLine
            {
                Ingredient = i,
                Farm = i.FarmAvailableQuantity,
                External = i.ExternalAvailableQuantity,
                ReorderLevel = i.ReorderLevel,
                TargetStockLevel = i.TargetStockLevel,
                OnOrder = onOrder.ContainsKey(i.Id) ? onOrder[i.Id] : 0m,
                PreferredSupplier = preferred.ContainsKey(i.Id) ? preferred[i.Id] : null
            }).ToList();
        }

        public IngredientStockLine GetStockLine(int ingredientId)
        {
            return GetStockLines().FirstOrDefault(l => l.Ingredient.Id == ingredientId);
        }

        // Outstanding quantity on orders already sent to suppliers
        public Dictionary<int, decimal> OnOrderByIngredient()
        {
            return _db.IngredientPurchaseOrderLines
                .Where(l => (l.PurchaseOrder.Status == IngredientOrderStatus.Pending || l.PurchaseOrder.Status == IngredientOrderStatus.Confirmed || l.PurchaseOrder.Status == IngredientOrderStatus.PartiallyFulfilled))
                .ToList()
                .GroupBy(l => l.IngredientId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Outstanding));
        }

        public List<IngredientStockTransaction> History(int ingredientId, int take = 100)
        {
            return _db.IngredientStockTransactions
                .Where(t => t.IngredientId == ingredientId)
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Take(take)
                .ToList();
        }

        // ============================================================
        // UPCOMING KITCHEN REQUIREMENTS
        // From kitchen plans built on actual choices: published weekly
        // menus (days not yet issued) and events whose RSVPs have closed
        // (not yet issued). Compared with what's in stock now.
        // ============================================================

        public List<UpcomingRequirement> UpcomingRequirements(int days = 14)
        {
            var today = _now().Date;
            var until = today.AddDays(days);

            var result = new Dictionary<int, UpcomingRequirement>();
            var ingredients = _db.Ingredients.ToList().ToDictionary(i => i.Id);

            Action<int, decimal, string> add = (ingredientId, quantity, label) =>
            {
                Ingredient ingredient;
                if (quantity <= 0m || !ingredients.TryGetValue(ingredientId, out ingredient)) return;

                UpcomingRequirement req;
                if (!result.TryGetValue(ingredientId, out req))
                {
                    req = new UpcomingRequirement
                    {
                        Ingredient = ingredient,
                        Available = ingredient.IsActive ? ingredient.FarmAvailableQuantity + ingredient.ExternalAvailableQuantity : 0m
                    };
                    result[ingredientId] = req;
                }

                req.Required += quantity;
                if (!req.NeededFor.Contains(label)) req.NeededFor.Add(label);
            };

            // Weekly menus
            var menus = _db.MealMenus
                .Where(m => m.MenuStatus == MenuStatus.Accepted && m.EndDate >= today && m.StartDate <= until)
                .Select(m => m.Id)
                .ToList();

            var scheduling = new MenuSchedulingService(_db);
            foreach (var menuId in menus)
            {
                var plan = scheduling.BuildProductionPlan(menuId);
                foreach (var day in plan.Days.Where(d => d.Date >= today && d.Date <= until && !d.IsIssued))
                {
                    foreach (var line in day.DayTotalIngredients)
                    {
                        add(line.IngredientId, line.TotalRequired, day.Date.ToString("ddd dd MMM"));
                    }
                }
            }

            // Events with a confirmed headcount
            var events = _db.CafeteriaEvents
                .Include(e => e.Venue)
                .Include(e => e.MenuTemplate)
                .Where(e => e.EventDate >= today && e.EventDate <= until
                            && (e.Status == EventStatus.RsvpClosed || e.Status == EventStatus.FeastPlanGenerated))
                .ToList();

            var issuedEvents = new HashSet<int>(_db.KitchenIngredientIssues
                .Where(i => i.CafeteriaEventId.HasValue)
                .Select(i => i.CafeteriaEventId.Value)
                .ToList());

            var buffet = new EventBuffetService(_db);
            foreach (var evt in events.Where(e => !issuedEvents.Contains(e.Id)))
            {
                var plan = buffet.BuildFeastPlan(evt);
                foreach (var line in plan.TotalIngredients)
                {
                    add(line.IngredientId, line.TotalRequired, evt.EventName + " (" + evt.EventDate.ToString("ddd dd MMM") + ")");
                }
            }

            var onOrder = OnOrderByIngredient();
            foreach (var req in result.Values)
            {
                decimal ordered;
                if (onOrder.TryGetValue(req.Ingredient.Id, out ordered)) req.OnOrder = ordered;
            }

            return result.Values.OrderBy(r => r.Ingredient.Name).ToList();
        }

        public InventorySummary Summary(List<IngredientStockLine> lines, List<UpcomingRequirement> upcoming)
        {
            var today = _now().Date;

            return new InventorySummary
            {
                TotalIngredients = lines.Count(l => l.Ingredient.IsActive),
                LowStock = lines.Count(l => l.IsLowStock),
                OutOfStock = lines.Count(l => l.IsOutOfStock),
                UpcomingShortages = upcoming.Count(u => u.Shortfall > 0m),
                PendingOrders = _db.IngredientPurchaseOrders.Count(o => o.Status == IngredientOrderStatus.Draft || (o.Status == IngredientOrderStatus.Pending || o.Status == IngredientOrderStatus.Confirmed || o.Status == IngredientOrderStatus.PartiallyFulfilled)),
                ExpectedDeliveries = _db.IngredientPurchaseOrders.Count(o => (o.Status == IngredientOrderStatus.Pending || o.Status == IngredientOrderStatus.Confirmed || o.Status == IngredientOrderStatus.PartiallyFulfilled))
            };
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
