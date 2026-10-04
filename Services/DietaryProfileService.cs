using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Student Dietary Profile Service
    //
    // Owns every dietary rule in one place:
    //   - the option lists shown to the student
    //   - parsing / validating / saving the profile
    //   - deciding whether a MenuItem conflicts with a profile
    //
    // Storage (no new tables — all on StudentProfile):
    //   Allergies                      → food allergies (existing field)
    //   DietaryPreference / ...Other   → preference
    //   MedicalDietaryRestrictions /
    //   MedicalDietaryRestrictionOther → medical restrictions
    //   DietaryNotes                   → free text for the kitchen
    //
    // Menu-side data used for conflicts:
    //   Ingredient.Allergens    → allergies, lactose / gluten restrictions,
    //                             and fish / shellfish / egg / dairy for
    //                             vegetarian and vegan
    //   Ingredient.DietaryTags  → meat / poultry / pork / gelatin / honey /
    //                             alcohol / halal
    //   MenuItem.DietaryClassification → dish-level cross-check for
    //                             vegetarian and vegan
    //
    // Rule of thumb: a dish is only EXCLUDED when the data proves a
    // conflict (or, for vegetarian / vegan / halal, when the data can't
    // prove the dish is suitable). Things the system can't check
    // reliably — diabetes, "Other" preferences and restrictions — are
    // never used to exclude; they are shown to the Dietitian and kitchen.
    // ============================================================

    public class DietaryOption
    {
        public DietaryOption(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; private set; }

        public string Label { get; private set; }
    }

    public enum DietaryConflictType
    {
        Allergy = 1,
        Preference = 2,
        MedicalRestriction = 3
    }

    public class DietaryConflict
    {
        public DietaryConflictType Type { get; set; }

        public string Message { get; set; }
    }

    // ============================================================
    // Parsed, normalised view of one student's dietary information.
    // Built from StudentProfile by DietaryProfileService.FromStudentProfile.
    // ============================================================

    public class StudentDietaryProfile
    {
        public StudentDietaryProfile()
        {
            Preference = DietaryProfileService.PreferenceNone;
            Allergies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            OtherAllergies = new List<string>();
            MedicalRestrictions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Preference { get; set; }

        public string PreferenceOther { get; set; }

        // Allergy codes from DietaryProfileService.AllergyOptions
        public HashSet<string> Allergies { get; set; }

        // Allergies the checklist doesn't cover, as typed
        public List<string> OtherAllergies { get; set; }

        // Codes from DietaryProfileService.MedicalRestrictionOptions
        public HashSet<string> MedicalRestrictions { get; set; }

        public string MedicalRestrictionOther { get; set; }

        public string Notes { get; set; }

        public bool HasAnyInformation
        {
            get
            {
                return Preference != DietaryProfileService.PreferenceNone
                       || Allergies.Count > 0
                       || OtherAllergies.Count > 0
                       || MedicalRestrictions.Count > 0
                       || !string.IsNullOrWhiteSpace(Notes);
            }
        }

        // "" when there is no preference
        public string PreferenceSummary
        {
            get
            {
                if (Preference == DietaryProfileService.PreferenceNone) return "";
                if (Preference == DietaryProfileService.OtherCode)
                {
                    return "Other: " + (PreferenceOther ?? "");
                }
                return DietaryProfileService.LabelFor(DietaryProfileService.PreferenceOptions, Preference);
            }
        }

        public string AllergySummary
        {
            get
            {
                var names = DietaryProfileService.AllergyOptions
                    .Where(o => Allergies.Contains(o.Code))
                    .Select(o => o.Code)
                    .Concat(OtherAllergies);

                return string.Join(", ", names);
            }
        }

        public string MedicalRestrictionSummary
        {
            get
            {
                var names = DietaryProfileService.MedicalRestrictionOptions
                    .Where(o => o.Code != DietaryProfileService.OtherCode
                                && MedicalRestrictions.Contains(o.Code))
                    .Select(o => o.Label)
                    .ToList();

                if (MedicalRestrictions.Contains(DietaryProfileService.OtherCode)
                    && !string.IsNullOrWhiteSpace(MedicalRestrictionOther))
                {
                    names.Add("Other: " + MedicalRestrictionOther);
                }

                return string.Join(", ", names);
            }
        }
    }

    public class DietaryProfileService
    {
        private readonly DBContextClass _db;

        public DietaryProfileService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // ============================================================
        // CODES AND OPTION LISTS
        // ============================================================

        public const string OtherCode = "Other";

        public const string PreferenceNone = "None";
        public const string PreferenceVegetarian = "Vegetarian";
        public const string PreferenceVegan = "Vegan";
        public const string PreferenceHalal = "Halal";

        public const string MedicalLactoseIntolerance = "LactoseIntolerance";
        public const string MedicalGlutenIntolerance = "GlutenIntolerance";
        public const string MedicalCoeliac = "Coeliac";
        public const string MedicalDiabetes = "Diabetes";

        public static readonly List<DietaryOption> PreferenceOptions = new List<DietaryOption>
        {
            new DietaryOption(PreferenceNone,       "No preference"),
            new DietaryOption(PreferenceVegetarian, "Vegetarian"),
            new DietaryOption(PreferenceVegan,      "Vegan"),
            new DietaryOption(PreferenceHalal,      "Halal"),
            new DietaryOption(OtherCode,            "Other")
        };

        // Allergy codes are also the names written to StudentProfile.Allergies,
        // so existing readers of that field keep seeing plain words.
        public static readonly List<DietaryOption> AllergyOptions = new List<DietaryOption>
        {
            new DietaryOption("Peanuts",   "Peanuts"),
            new DietaryOption("Tree nuts", "Tree nuts"),
            new DietaryOption("Dairy",     "Dairy / Milk"),
            new DietaryOption("Eggs",      "Eggs"),
            new DietaryOption("Gluten",    "Gluten"),
            new DietaryOption("Soy",       "Soy"),
            new DietaryOption("Fish",      "Fish"),
            new DietaryOption("Shellfish", "Shellfish"),
            new DietaryOption("Sesame",    "Sesame"),
            new DietaryOption(OtherCode,   "Other")
        };

        public static readonly List<DietaryOption> MedicalRestrictionOptions = new List<DietaryOption>
        {
            new DietaryOption(MedicalLactoseIntolerance, "Lactose intolerance"),
            new DietaryOption(MedicalGlutenIntolerance,  "Gluten intolerance"),
            new DietaryOption(MedicalCoeliac,            "Coeliac disease"),
            new DietaryOption(MedicalDiabetes,           "Diabetes-related restriction"),
            new DietaryOption(OtherCode,                 "Other")
        };

        // Student allergy code → Ingredient.Allergens tags that trigger it.
        // Ingredient data currently uses a single "Nuts" tag, so both
        // peanut and tree-nut allergies treat "Nuts" as a conflict.
        private static readonly Dictionary<string, string[]> AllergyIngredientTags =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Peanuts",   new[] { "Peanuts", "Peanut", "Nuts" } },
                { "Tree nuts", new[] { "Tree nuts", "TreeNuts", "Nuts" } },
                { "Dairy",     new[] { "Dairy", "Milk" } },
                { "Eggs",      new[] { "Egg", "Eggs" } },
                { "Gluten",    new[] { "Gluten", "Wheat" } },
                { "Soy",       new[] { "Soy" } },
                { "Fish",      new[] { "Fish" } },
                { "Shellfish", new[] { "Shellfish" } },
                { "Sesame",    new[] { "Sesame" } }
            };

        // Free-text words → allergy codes. Lets us read what the parent
        // wizard or older seed data wrote ("Nuts", "Milk", ...).
        private static readonly Dictionary<string, string[]> AllergyAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "peanut",      new[] { "Peanuts" } },
                { "peanuts",     new[] { "Peanuts" } },
                { "groundnuts",  new[] { "Peanuts" } },
                { "tree nut",    new[] { "Tree nuts" } },
                { "tree nuts",   new[] { "Tree nuts" } },
                { "treenuts",    new[] { "Tree nuts" } },
                { "nut",         new[] { "Peanuts", "Tree nuts" } },
                { "nuts",        new[] { "Peanuts", "Tree nuts" } },
                { "dairy",       new[] { "Dairy" } },
                { "milk",        new[] { "Dairy" } },
                { "dairy / milk", new[] { "Dairy" } },
                { "dairy/milk",  new[] { "Dairy" } },
                { "egg",         new[] { "Eggs" } },
                { "eggs",        new[] { "Eggs" } },
                { "gluten",      new[] { "Gluten" } },
                { "wheat",       new[] { "Gluten" } },
                { "soy",         new[] { "Soy" } },
                { "soya",        new[] { "Soy" } },
                { "fish",        new[] { "Fish" } },
                { "shellfish",   new[] { "Shellfish" } },
                { "sesame",      new[] { "Sesame" } }
            };

        // Medical restriction code → Ingredient.Allergens tags it rules out.
        // Diabetes and Other are deliberately absent: the menu data can't
        // reliably say whether a dish is unsuitable, so they only warn.
        private static readonly Dictionary<string, string[]> MedicalIngredientTags =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { MedicalLactoseIntolerance, new[] { "Dairy", "Milk" } },
                { MedicalGlutenIntolerance,  new[] { "Gluten", "Wheat" } },
                { MedicalCoeliac,            new[] { "Gluten", "Wheat" } }
            };

        // Words that belong in the preference or medical sections, used to
        // stop them being entered as "other allergies".
        private static readonly string[] PreferenceWords =
            { "vegetarian", "vegan", "halal", "kosher", "pescatarian" };

        private static readonly string[] MedicalWords =
            { "intoleran", "lactose", "coeliac", "celiac", "diabet" };

        private static readonly string[] VegetarianTags = { "Meat", "Poultry", "Pork", "Gelatin" };
        private static readonly string[] VegetarianAllergenTags = { "Fish", "Shellfish" };
        private static readonly string[] VeganTags = { "Meat", "Poultry", "Pork", "Gelatin", "Honey" };
        private static readonly string[] VeganAllergenTags = { "Fish", "Shellfish", "Egg", "Eggs", "Dairy", "Milk" };
        private static readonly string[] HalalForbiddenTags = { "Pork", "Alcohol" };
        private static readonly string[] HalalCertifiableTags = { "Meat", "Poultry", "Gelatin" };
        private const string HalalTag = "Halal";

        public static string LabelFor(IEnumerable<DietaryOption> options, string code)
        {
            var match = options.FirstOrDefault(o =>
                string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));

            return match != null ? match.Label : code;
        }

        // ============================================================
        // PARSE — StudentProfile → StudentDietaryProfile
        // Tolerant of free text written by the parent wizard.
        // ============================================================

        public static StudentDietaryProfile FromStudentProfile(StudentProfile profile)
        {
            var result = new StudentDietaryProfile();

            if (profile == null)
            {
                return result;
            }

            // Preference
            var pref = PreferenceOptions.FirstOrDefault(o =>
                string.Equals(o.Code, (profile.DietaryPreference ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

            result.Preference = pref != null ? pref.Code : PreferenceNone;

            if (result.Preference == OtherCode)
            {
                result.PreferenceOther = profile.DietaryPreferenceOther;
            }

            // Allergies
            foreach (var token in SplitList(profile.Allergies))
            {
                string[] codes;
                if (AllergyAliases.TryGetValue(token, out codes))
                {
                    foreach (var code in codes) result.Allergies.Add(code);
                }
                else if (!result.OtherAllergies.Contains(token, StringComparer.OrdinalIgnoreCase))
                {
                    result.OtherAllergies.Add(token);
                }
            }

            // Medical restrictions
            foreach (var token in SplitList(profile.MedicalDietaryRestrictions))
            {
                var option = MedicalRestrictionOptions.FirstOrDefault(o =>
                    string.Equals(o.Code, token, StringComparison.OrdinalIgnoreCase));

                if (option != null) result.MedicalRestrictions.Add(option.Code);
            }

            if (result.MedicalRestrictions.Contains(OtherCode))
            {
                result.MedicalRestrictionOther = profile.MedicalDietaryRestrictionOther;
            }

            result.Notes = profile.DietaryNotes;

            return result;
        }

        public StudentDietaryProfile GetProfileForStudent(int studentId)
        {
            var profile = _db.StudentProfiles.FirstOrDefault(p => p.StudentId == studentId);
            return FromStudentProfile(profile);
        }

        // ============================================================
        // STUDENT FORM — LOAD
        // ============================================================

        public DietaryProfileViewModel GetForStudent(int studentId)
        {
            var student = _db.Students
                .Include("StudentProfile")
                .FirstOrDefault(s => s.StudentId == studentId);

            if (student == null)
            {
                throw new InvalidOperationException("Student not found.");
            }

            var parsed = FromStudentProfile(student.StudentProfile);

            var vm = new DietaryProfileViewModel
            {
                StudentName = student.FirstName + " " + student.LastName,
                UpdatedAt = student.StudentProfile != null
                    ? student.StudentProfile.DietaryProfileUpdatedAt
                    : null,
                DietaryPreference = parsed.Preference,
                DietaryPreferenceOther = parsed.PreferenceOther,
                SelectedAllergies = parsed.Allergies.ToList(),
                OtherAllergies = string.Join(", ", parsed.OtherAllergies),
                SelectedMedicalRestrictions = parsed.MedicalRestrictions.ToList(),
                MedicalRestrictionOther = parsed.MedicalRestrictionOther,
                DietaryNotes = parsed.Notes
            };

            if (parsed.OtherAllergies.Count > 0)
            {
                vm.SelectedAllergies.Add(OtherCode);
            }

            return vm;
        }

        // ============================================================
        // STUDENT FORM — VALIDATE
        // Returns user-facing error messages. Empty list = valid.
        // ============================================================

        public List<string> Validate(DietaryProfileViewModel vm)
        {
            var errors = new List<string>();

            if (vm == null)
            {
                errors.Add("No dietary information was received.");
                return errors;
            }

            // ── Preference ──
            var preference = string.IsNullOrWhiteSpace(vm.DietaryPreference)
                ? PreferenceNone
                : vm.DietaryPreference.Trim();

            if (!PreferenceOptions.Any(o => string.Equals(o.Code, preference, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Please choose a dietary preference from the list.");
            }
            else if (string.Equals(preference, OtherCode, StringComparison.OrdinalIgnoreCase)
                     && string.IsNullOrWhiteSpace(vm.DietaryPreferenceOther))
            {
                errors.Add("Please describe your dietary preference, or choose one from the list.");
            }

            // ── Allergies ──
            var allergies = Clean(vm.SelectedAllergies);

            if (allergies.Any(a => !AllergyOptions.Any(o => string.Equals(o.Code, a, StringComparison.OrdinalIgnoreCase))))
            {
                errors.Add("One of the selected allergies is not recognised.");
            }

            if (allergies.Contains(OtherCode, StringComparer.OrdinalIgnoreCase))
            {
                var others = SplitList(vm.OtherAllergies);

                if (others.Count == 0)
                {
                    errors.Add("Please name the other allergy, or untick \"Other\".");
                }

                foreach (var other in others)
                {
                    var lower = other.ToLowerInvariant();

                    if (PreferenceWords.Any(w => lower.Contains(w)))
                    {
                        errors.Add(string.Format(
                            "\"{0}\" is a dietary preference, not an allergy. Choose it under Dietary preference instead.",
                            other));
                    }
                    else if (MedicalWords.Any(w => lower.Contains(w)))
                    {
                        errors.Add(string.Format(
                            "\"{0}\" is a medical dietary restriction, not an allergy. Tick it under Medical dietary restrictions instead.",
                            other));
                    }
                    else if (other.Length > 100)
                    {
                        errors.Add("Each other allergy must be 100 characters or fewer.");
                    }
                }
            }

            // ── Medical restrictions ──
            var medical = Clean(vm.SelectedMedicalRestrictions);

            if (medical.Any(m => !MedicalRestrictionOptions.Any(o => string.Equals(o.Code, m, StringComparison.OrdinalIgnoreCase))))
            {
                errors.Add("One of the selected medical dietary restrictions is not recognised.");
            }

            if (medical.Contains(OtherCode, StringComparer.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(vm.MedicalRestrictionOther))
            {
                errors.Add("Please describe the other medical dietary restriction, or untick \"Other\".");
            }

            // ── Lengths ──
            if ((vm.DietaryPreferenceOther ?? "").Trim().Length > 200)
                errors.Add("The dietary preference description must be 200 characters or fewer.");

            if ((vm.MedicalRestrictionOther ?? "").Trim().Length > 200)
                errors.Add("The medical restriction description must be 200 characters or fewer.");

            if ((vm.DietaryNotes ?? "").Trim().Length > 1000)
                errors.Add("Additional dietary information must be 1000 characters or fewer.");

            return errors;
        }

        // ============================================================
        // STUDENT FORM — SAVE
        // Normalises the input: no duplicates, no empty entries, and
        // "Other" text is only kept when "Other" is selected.
        // ============================================================

        public void Save(int studentId, DietaryProfileViewModel vm)
        {
            var errors = Validate(vm);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(errors[0]);
            }

            var student = _db.Students
                .Include("StudentProfile")
                .FirstOrDefault(s => s.StudentId == studentId);

            if (student == null)
            {
                throw new InvalidOperationException("Student not found.");
            }

            var profile = student.StudentProfile;

            if (profile == null)
            {
                profile = new StudentProfile { StudentId = studentId };
                _db.StudentProfiles.Add(profile);
            }

            ApplyTo(profile, vm);
            profile.DietaryProfileUpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
        }

        // Writes validated form input into the dietary fields of a
        // StudentProfile-shaped record. Used for the student profile
        // and (via an unsaved StudentProfile) for event RSVPs from
        // parents and staff, so both store the same format.
        public static void ApplyTo(StudentProfile profile, DietaryProfileViewModel vm)
        {
            // ── Preference ──
            var preference = PreferenceOptions.First(o =>
                string.Equals(o.Code, string.IsNullOrWhiteSpace(vm.DietaryPreference) ? PreferenceNone : vm.DietaryPreference.Trim(),
                              StringComparison.OrdinalIgnoreCase)).Code;

            profile.DietaryPreference = preference;
            profile.DietaryPreferenceOther = preference == OtherCode
                ? vm.DietaryPreferenceOther.Trim()
                : null;

            // ── Allergies ──
            // Known allergies in checklist order, then other allergies.
            // An "other" allergy that matches a known one (e.g. typing
            // "peanuts") is folded into the checklist entry.
            var selected = new HashSet<string>(Clean(vm.SelectedAllergies), StringComparer.OrdinalIgnoreCase);
            var otherAllergies = new List<string>();

            if (selected.Contains(OtherCode))
            {
                foreach (var other in SplitList(vm.OtherAllergies))
                {
                    string[] codes;
                    if (AllergyAliases.TryGetValue(other, out codes))
                    {
                        foreach (var code in codes) selected.Add(code);
                    }
                    else if (!otherAllergies.Contains(other, StringComparer.OrdinalIgnoreCase))
                    {
                        otherAllergies.Add(other);
                    }
                }
            }

            var allergyNames = AllergyOptions
                .Where(o => o.Code != OtherCode && selected.Contains(o.Code))
                .Select(o => o.Code)
                .Concat(otherAllergies)
                .ToList();

            profile.Allergies = allergyNames.Count > 0 ? string.Join(", ", allergyNames) : null;

            // ── Medical restrictions ──
            var medical = new HashSet<string>(Clean(vm.SelectedMedicalRestrictions), StringComparer.OrdinalIgnoreCase);

            var medicalCodes = MedicalRestrictionOptions
                .Where(o => medical.Contains(o.Code))
                .Select(o => o.Code)
                .ToList();

            profile.MedicalDietaryRestrictions = medicalCodes.Count > 0 ? string.Join(",", medicalCodes) : null;
            profile.MedicalDietaryRestrictionOther = medicalCodes.Contains(OtherCode)
                ? vm.MedicalRestrictionOther.Trim()
                : null;

            // ── Notes ──
            profile.DietaryNotes = string.IsNullOrWhiteSpace(vm.DietaryNotes)
                ? null
                : vm.DietaryNotes.Trim();
        }

        // ============================================================
        // CONFLICT CHECK — does this dish conflict with this profile?
        //
        // The MenuItem must have Recipe.RecipeIngredients.Ingredient
        // loaded (every caller already includes it).
        // ============================================================

        public bool IsSafe(StudentDietaryProfile profile, MenuItem item)
        {
            return GetConflicts(profile, item).Count == 0;
        }

        public List<DietaryConflict> GetConflicts(StudentDietaryProfile profile, MenuItem item)
        {
            var conflicts = new List<DietaryConflict>();

            if (profile == null || item == null)
            {
                return conflicts;
            }

            var ingredients = new List<Ingredient>();

            if (item.Recipe != null && item.Recipe.RecipeIngredients != null)
            {
                ingredients = item.Recipe.RecipeIngredients
                    .Where(ri => ri.Ingredient != null)
                    .Select(ri => ri.Ingredient)
                    .ToList();
            }

            // ── 1. Allergies ──
            foreach (var allergy in profile.Allergies)
            {
                string[] tags;
                if (!AllergyIngredientTags.TryGetValue(allergy, out tags)) continue;

                var hits = IngredientsWithAllergens(ingredients, tags);
                if (hits.Count > 0)
                {
                    conflicts.Add(new DietaryConflict
                    {
                        Type = DietaryConflictType.Allergy,
                        Message = string.Format("{0} allergy — contains {1}", allergy, JoinNames(hits))
                    });
                }
            }

            // Other allergies: best-effort name match on the dish and its ingredients
            foreach (var other in profile.OtherAllergies)
            {
                if (other.Length < 3) continue;

                var hits = ingredients
                    .Where(i => Contains(i.Name, other) || SplitList(i.Allergens).Any(a => Contains(a, other)))
                    .ToList();

                if (hits.Count > 0 || Contains(item.Name, other))
                {
                    conflicts.Add(new DietaryConflict
                    {
                        Type = DietaryConflictType.Allergy,
                        Message = hits.Count > 0
                            ? string.Format("{0} allergy — contains {1}", other, JoinNames(hits))
                            : string.Format("{0} allergy — dish name mentions it", other)
                    });
                }
            }

            // ── 2. Medical restrictions ──
            foreach (var restriction in profile.MedicalRestrictions)
            {
                string[] tags;
                if (!MedicalIngredientTags.TryGetValue(restriction, out tags)) continue;

                var hits = IngredientsWithAllergens(ingredients, tags);
                if (hits.Count > 0)
                {
                    conflicts.Add(new DietaryConflict
                    {
                        Type = DietaryConflictType.MedicalRestriction,
                        Message = string.Format("{0} — contains {1}",
                            LabelFor(MedicalRestrictionOptions, restriction), JoinNames(hits))
                    });
                }
            }

            // ── 3. Preference ──
            var preferenceConflict = GetPreferenceConflict(profile.Preference, item, ingredients);
            if (preferenceConflict != null)
            {
                conflicts.Add(new DietaryConflict
                {
                    Type = DietaryConflictType.Preference,
                    Message = preferenceConflict
                });
            }

            return conflicts;
        }

        private string GetPreferenceConflict(string preference, MenuItem item, List<Ingredient> ingredients)
        {
            bool declaredVegetarian = IsClassification(item, PreferenceVegetarian) || IsClassification(item, PreferenceVegan);
            bool hasRecipe = ingredients.Count > 0;

            if (preference == PreferenceVegetarian)
            {
                var hits = NonVegetarianIngredients(ingredients);

                if (hits.Count > 0)
                    return "Vegetarian — contains " + JoinNames(hits);

                // Dish-level cross-check: don't serve a dish the kitchen
                // hasn't marked vegetarian, even if its ingredients look fine.
                if (!declaredVegetarian)
                    return string.Format("Vegetarian — dish is marked \"{0}\"", item.DietaryClassification);
            }
            else if (preference == PreferenceVegan)
            {
                var hits = NonVeganIngredients(ingredients);

                if (hits.Count > 0)
                    return "Vegan — contains " + JoinNames(hits);

                if (!declaredVegetarian)
                    return string.Format("Vegan — dish is marked \"{0}\"", item.DietaryClassification);

                if (!hasRecipe && !IsClassification(item, PreferenceVegan))
                    return "Vegan — no recipe on file to confirm it is vegan";
            }
            else if (preference == PreferenceHalal)
            {
                var problem = GetHalalProblem(ingredients);
                if (problem != null)
                    return "Halal — " + problem;
            }

            // None / Other: nothing the menu data can check reliably.
            return null;
        }

        // ============================================================
        // INGREDIENT-LEVEL FACTS
        // Shared with MealLibraryService so a meal's classification,
        // halal status and allergens are worked out by the same rules
        // used to filter student options.
        // ============================================================

        // Tags the Dietitian can put on an ingredient (Ingredient.DietaryTags)
        public static readonly List<DietaryOption> IngredientTagOptions = new List<DietaryOption>
        {
            new DietaryOption("Meat",    "Meat"),
            new DietaryOption("Poultry", "Poultry"),
            new DietaryOption("Pork",    "Pork"),
            new DietaryOption("Gelatin", "Gelatin"),
            new DietaryOption("Honey",   "Honey"),
            new DietaryOption("Alcohol", "Alcohol"),
            new DietaryOption(HalalTag,  "Halal-certified")
        };

        public static List<Ingredient> NonVegetarianIngredients(IEnumerable<Ingredient> ingredients)
        {
            return ingredients
                .Where(i => HasAny(i.DietaryTags, VegetarianTags) || HasAny(i.Allergens, VegetarianAllergenTags))
                .ToList();
        }

        public static List<Ingredient> NonVeganIngredients(IEnumerable<Ingredient> ingredients)
        {
            return ingredients
                .Where(i => HasAny(i.DietaryTags, VeganTags) || HasAny(i.Allergens, VeganAllergenTags))
                .ToList();
        }

        // Null when the ingredients are halal; otherwise the reason.
        // Pork / alcohol always fail. Meat, poultry and gelatin must be
        // tagged halal-certified.
        public static string GetHalalProblem(IEnumerable<Ingredient> ingredients)
        {
            var list = ingredients.ToList();

            var forbidden = list
                .Where(i => HasAny(i.DietaryTags, HalalForbiddenTags))
                .ToList();

            if (forbidden.Count > 0)
                return "contains " + JoinNames(forbidden);

            var unverified = list
                .Where(i => HasAny(i.DietaryTags, HalalCertifiableTags)
                            && !HasAny(i.DietaryTags, new[] { HalalTag }))
                .ToList();

            if (unverified.Count > 0)
                return JoinNames(unverified) + " not marked halal";

            return null;
        }

        // Allergens across the ingredients, using the student-facing
        // names (older tags such as "Egg" and "Milk" are normalised).
        public static List<string> GetAllergens(IEnumerable<Ingredient> ingredients)
        {
            var result = new List<string>();

            foreach (var ingredient in ingredients)
            {
                foreach (var tag in SplitList(ingredient.Allergens))
                {
                    var name = NormaliseAllergenTag(tag);
                    if (!result.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(name);
                    }
                }
            }

            return result
                .OrderBy(n =>
                {
                    int idx = AllergyOptions.FindIndex(o => string.Equals(o.Code, n, StringComparison.OrdinalIgnoreCase));
                    return idx < 0 ? int.MaxValue : idx;
                })
                .ThenBy(n => n)
                .ToList();
        }

        public static string NormaliseAllergenTag(string tag)
        {
            var trimmed = (tag ?? "").Trim();

            // "Nuts" on its own doesn't say which kind
            if (string.Equals(trimmed, "Nuts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "Nut", StringComparison.OrdinalIgnoreCase))
            {
                return "Nuts (unspecified)";
            }

            string[] codes;
            if (AllergyAliases.TryGetValue(trimmed, out codes) && codes.Length == 1)
            {
                return codes[0];
            }

            return trimmed;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static List<Ingredient> IngredientsWithAllergens(List<Ingredient> ingredients, string[] tags)
        {
            return ingredients.Where(i => HasAny(i.Allergens, tags)).ToList();
        }

        private static bool HasAny(string csv, string[] tags)
        {
            var values = SplitList(csv);
            return values.Any(v => tags.Any(t => string.Equals(v, t, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsClassification(MenuItem item, string classification)
        {
            return string.Equals((item.DietaryClassification ?? "").Trim(), classification, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text)
                   && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string JoinNames(IEnumerable<Ingredient> ingredients)
        {
            return string.Join(", ", ingredients.Select(i => i.Name).Distinct());
        }

        // Splits "a, b; c" into trimmed, non-empty, de-duplicated entries.
        private static List<string> SplitList(string csv)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(csv))
            {
                return result;
            }

            foreach (var part in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0 && !result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }

        private static List<string> Clean(IEnumerable<string> values)
        {
            if (values == null) return new List<string>();

            return values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
