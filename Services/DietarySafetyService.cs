using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Services
{
    /// <summary>
    /// Severity of a safety check.
    /// </summary>
    public enum SafetyLevel
    {
        Safe = 0,
        Warning = 1,
        Blocked = 2
    }

    public class SafetyCheckResult
    {
        public bool IsSafe { get; set; }
        public SafetyLevel Level { get; set; }
        public string Reason { get; set; }
        public List<string> ConflictingAllergens { get; set; } = new List<string>();
        public List<string> ConflictingDietaryCategories { get; set; } = new List<string>();
        public List<string> InformationMessages { get; set; } = new List<string>();
    }

    /// <summary>
    /// Hard safety rule engine.
    ///
    /// PRIORITY (Section 36 of the Master Plan):
    ///   1. Food Safety / Allergy  — always wins
    ///   2. Medical Restriction    — always wins
    ///   3. Dietary Preference     — wins over sports and student choice
    ///   4. Religious / Cultural   — informational, escalated where possible
    ///   5. Sports requirements    — cannot override any of the above
    ///   6. Student preference     — lowest
    ///
    /// NOTHING in the system may override a result with Level == Blocked.
    /// </summary>
    public class DietarySafetyService
    {
        private readonly DBContextClass _db;

        public DietarySafetyService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public DietarySafetyService() : this(new DBContextClass()) { }

        // ─────────────────────────────────────────────────────────────
        // Active restrictions for a student
        // ─────────────────────────────────────────────────────────────

        public List<StudentDietaryRecord> GetActiveRestrictions(int studentId)
        {
            return _db.StudentDietaryRecords
                .Include("Allergen")
                .Include("DietaryCategory")
                .Where(r => r.StudentId == studentId && r.IsActive && r.Status == DietaryRequestStatus.Approved)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────
        // Core safety check: student vs single menu item
        // ─────────────────────────────────────────────────────────────

        public SafetyCheckResult CheckMenuItem(int studentId, int menuItemId)
        {
            var result = new SafetyCheckResult
            {
                IsSafe = true,
                Level = SafetyLevel.Safe,
                Reason = ""
            };

            var item = _db.MenuItems
                .Include("Allergens.Allergen")
                .Include("DietaryTags.DietaryCategory")
                .FirstOrDefault(x => x.Id == menuItemId);

            if (item == null)
            {
                result.IsSafe = false;
                result.Level = SafetyLevel.Blocked;
                result.Reason = "Menu item not found.";
                return result;
            }

            var restrictions = GetActiveRestrictions(studentId);
            if (restrictions.Count == 0) return result;

            foreach (var r in restrictions)
            {
                switch (r.RecordType)
                {
                    // ─── ALLERGY: hard block if ingredient contains it ─
                    case DietaryRecordType.Allergy:
                        if (r.AllergenId.HasValue)
                        {
                            var hit = item.Allergens.FirstOrDefault(a => a.AllergenId == r.AllergenId.Value);
                            if (hit != null)
                            {
                                if (hit.PresenceType == AllergenPresenceType.Contains ||
                                    hit.PresenceType == AllergenPresenceType.PreparedWith ||
                                    hit.PresenceType == AllergenPresenceType.CrossContact)
                                {
                                    result.IsSafe = false;
                                    result.Level = SafetyLevel.Blocked;
                                    result.ConflictingAllergens.Add(hit.Allergen != null ? hit.Allergen.Name : "Unknown allergen");
                                }
                                else if (hit.PresenceType == AllergenPresenceType.MayContain)
                                {
                                    // May contain → warning, not block
                                    if (result.Level < SafetyLevel.Warning) result.Level = SafetyLevel.Warning;
                                    result.InformationMessages.Add("May contain traces of " +
                                        (hit.Allergen != null ? hit.Allergen.Name : "an allergen"));
                                }
                            }
                        }
                        break;

                    // ─── MEDICAL RESTRICTION: hard block if name match ─
                    case DietaryRecordType.MedicalRestriction:
                        if (!string.IsNullOrEmpty(r.CustomDescription))
                        {
                            var desc = r.CustomDescription.ToLowerInvariant();
                            var name = (item.Name ?? "").ToLowerInvariant();
                            if (name.Contains(desc))
                            {
                                result.IsSafe = false;
                                result.Level = SafetyLevel.Blocked;
                                result.ConflictingDietaryCategories.Add(r.CustomDescription);
                            }
                        }
                        break;

                    // ─── DIETARY PREFERENCE: block if item violates it ─
                    case DietaryRecordType.DietaryPreference:
                        if (r.DietaryCategoryId.HasValue)
                        {
                            var cat = r.DietaryCategory ?? _db.DietaryCategories
                                .FirstOrDefault(c => c.Id == r.DietaryCategoryId.Value);

                            if (cat != null)
                            {
                                bool violation = false;
                                var cn = (cat.Name ?? "").Trim().ToLowerInvariant();

                                if (cn == "vegetarian" && !item.IsVegetarian) violation = true;
                                if (cn == "vegan" && !item.IsVegan) violation = true;
                                if (cn == "gluten-free" && !item.IsGlutenFree) violation = true;
                                if (cn == "lactose-free" && !item.IsLactoseFree) violation = true;

                                if (violation)
                                {
                                    result.IsSafe = false;
                                    result.Level = SafetyLevel.Blocked;
                                    result.ConflictingDietaryCategories.Add(cat.Name);
                                }
                            }
                        }
                        break;

                    // ─── RELIGIOUS / CULTURAL: informational only ─────
                    case DietaryRecordType.ReligiousCultural:
                        if (!string.IsNullOrEmpty(r.CustomDescription))
                            result.InformationMessages.Add("Religious/cultural requirement: " + r.CustomDescription);
                        break;

                    // ─── DISLIKE: never blocks ────────────────────────
                    case DietaryRecordType.Dislike:
                        if (!string.IsNullOrEmpty(r.CustomDescription))
                            result.InformationMessages.Add("You marked a dislike: " + r.CustomDescription);
                        break;
                }
            }

            if (!result.IsSafe)
            {
                if (result.ConflictingAllergens.Count > 0)
                    result.Reason = "Contains allergen(s): " + string.Join(", ", result.ConflictingAllergens);
                else if (result.ConflictingDietaryCategories.Count > 0)
                    result.Reason = "Conflicts with: " + string.Join(", ", result.ConflictingDietaryCategories);
                else
                    result.Reason = "Not safe for this student.";
            }
            else if (result.Level == SafetyLevel.Warning)
            {
                result.Reason = "May contain allergen traces. Choose carefully.";
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // Safe alternatives
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Given a menu meal and a set of candidate menu items,
        /// returns only the ones that are Safe for the student.
        /// </summary>
        public List<MenuItem> FilterSafeMenuItems(int studentId, IEnumerable<MenuItem> candidates)
        {
            if (candidates == null) return new List<MenuItem>();

            var safe = new List<MenuItem>();
            foreach (var m in candidates)
            {
                var check = CheckMenuItem(studentId, m.Id);
                if (check.IsSafe) safe.Add(m);
            }
            return safe;
        }

        /// <summary>
        /// Recommends safe alternatives when a student's chosen item is blocked.
        /// Looks at MenuMealAlternatives for the same MenuMeal first,
        /// then falls back to other MenuMealComponents in the same meal.
        /// </summary>
        public List<MenuItem> GetSafeAlternatives(int studentId, int menuMealId, int blockedMenuItemId)
        {
            var meal = _db.MenuMeals
                .Include("Components.MenuItem")
                .Include("Alternatives.MenuItem")
                .FirstOrDefault(m => m.Id == menuMealId);

            if (meal == null) return new List<MenuItem>();

            var candidates = new List<MenuItem>();
            candidates.AddRange(meal.Alternatives.Where(a => a.MenuItem != null).Select(a => a.MenuItem));
            candidates.AddRange(meal.Components.Where(c => c.MenuItem != null).Select(c => c.MenuItem));

            return candidates
                .Where(c => c.Id != blockedMenuItemId)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .Where(c => CheckMenuItem(studentId, c.Id).IsSafe)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────
        // Batch check (used by kitchen / menu-plan generation)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns how many students would be BLOCKED from a given menu item.
        /// Used by the validation service to decide if a menu item is safe
        /// for the whole student body.
        /// </summary>
        public int CountStudentsBlockedFromMenuItem(int menuItemId)
        {
            var studentsWithRestrictions = _db.StudentDietaryRecords
                .Where(r => r.IsActive && r.Status == DietaryRequestStatus.Approved)
                .Select(r => r.StudentId)
                .Distinct()
                .ToList();

            int blocked = 0;
            foreach (var sid in studentsWithRestrictions)
            {
                var check = CheckMenuItem(sid, menuItemId);
                if (!check.IsSafe) blocked++;
            }
            return blocked;
        }
    }
}