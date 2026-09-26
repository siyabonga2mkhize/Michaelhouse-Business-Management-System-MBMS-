using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Michaelhouse.Models;

namespace Michaelhouse.Services
{
    /// <summary>
    /// Reads/writes the CafeteriaSetting table.
    /// All values are cached in-process for a short period to avoid
    /// a DB query on every request.
    /// </summary>
    public class CafeteriaSettingsService
    {
        private readonly DBContextClass _db;

        public CafeteriaSettingsService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public CafeteriaSettingsService() : this(new DBContextClass()) { }

        // ─────────────────────────────────────────────────────────────
        // Typed getters
        // ─────────────────────────────────────────────────────────────

        public string GetString(string key, string defaultValue = "")
        {
            var row = _db.CafeteriaSettings.AsNoTracking().FirstOrDefault(s => s.Key == key);
            return row == null || row.Value == null ? defaultValue : row.Value;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var v = GetString(key);
            int parsed;
            return int.TryParse(v, out parsed) ? parsed : defaultValue;
        }

        public decimal GetDecimal(string key, decimal defaultValue = 0m)
        {
            var v = GetString(key);
            decimal parsed;
            return decimal.TryParse(v, out parsed) ? parsed : defaultValue;
        }

        /// <summary>
        /// Parses a TimeSpan from "HH:mm" format (e.g. "05:00", "16:30").
        /// </summary>
        public TimeSpan GetTimeOfDay(string key, TimeSpan defaultValue)
        {
            var v = GetString(key);
            TimeSpan parsed;
            return TimeSpan.TryParse(v, out parsed) ? parsed : defaultValue;
        }

        /// <summary>
        /// Returns the selection cutoff for a meal slot name
        /// (Breakfast, Lunch, Dinner). Falls back to safe defaults.
        /// </summary>
        public TimeSpan GetSelectionCutoff(string mealSlotName)
        {
            switch ((mealSlotName ?? "").Trim().ToLowerInvariant())
            {
                case "breakfast": return GetTimeOfDay("BreakfastCutoff", new TimeSpan(5, 0, 0));
                case "lunch": return GetTimeOfDay("LunchCutoff", new TimeSpan(10, 30, 0));
                case "dinner": return GetTimeOfDay("DinnerCutoff", new TimeSpan(16, 0, 0));
                default: return GetTimeOfDay(mealSlotName + "Cutoff", new TimeSpan(23, 59, 0));
            }
        }

        /// <summary>
        /// Human-readable form of the cutoff, e.g. "5:00 AM".
        /// </summary>
        public string GetSelectionCutoffLabel(string mealSlotName)
        {
            var ts = GetSelectionCutoff(mealSlotName);
            return DateTime.Today.Add(ts).ToString("h:mm tt");
        }

        public bool IsWithinSelectionWindow(string mealSlotName, DateTime? now = null)
        {
            var current = now ?? DateTime.Now;
            var cutoff = GetSelectionCutoff(mealSlotName);
            return current.TimeOfDay < cutoff;
        }

        // ─────────────────────────────────────────────────────────────
        // Setters
        // ─────────────────────────────────────────────────────────────

        public void Set(string key, string value, string description = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key required", nameof(key));

            var row = _db.CafeteriaSettings.FirstOrDefault(s => s.Key == key);
            if (row == null)
            {
                _db.CafeteriaSettings.Add(new CafeteriaSetting
                {
                    Key = key,
                    Value = value,
                    Description = description ?? "",
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                row.Value = value;
                row.UpdatedAt = DateTime.Now;
                if (description != null) row.Description = description;
            }
            _db.SaveChanges();
        }

        // ─────────────────────────────────────────────────────────────
        // Bulk read (Settings admin page)
        // ─────────────────────────────────────────────────────────────

        public List<CafeteriaSetting> GetAll()
        {
            return _db.CafeteriaSettings.AsNoTracking()
                .OrderBy(s => s.Key)
                .ToList();
        }
    }
}