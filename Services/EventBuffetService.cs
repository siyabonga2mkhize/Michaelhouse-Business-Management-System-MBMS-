using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC17 — Event buffet meals and dietary coverage
    //
    // The manager picks buffet meals from the Dietitian's meal
    // library (active MenuItems). This service:
    //   • lists the meals with what they suit (vegetarian / vegan /
    //     halal) and their allergens
    //   • saves the selection (CafeteriaEventMenuItem)
    //   • checks whether, TOGETHER, the meals give every dietary
    //     group of students a suitable choice — the same grouping
    //     and safety rules as the daily menu (MenuCoverageService /
    //     DietaryProfileService.IsSafe). Not every meal has to suit
    //     everyone.
    //
    // Who is checked:
    //   • before the headcount is confirmed: every student invited
    //     (any of them may still RSVP), plus everyone — student,
    //     parent, staff or guest — who has accepted so far
    //   • once RSVPs are closed: everyone who accepted
    //
    // It also turns the accepted RSVPs into the kitchen's
    // requirements and feast plan (CalculateRequirements,
    // BuildFeastPlan): attendance by type, dietary groups, meals
    // chosen, portions and ingredients (Recipe → RecipeIngredient →
    // Ingredient). Nothing is sent to the kitchen before RSVPs close.
    // ============================================================

    public class BuffetMealOption
    {
        public BuffetMealOption()
        {
            SuitableFor = new List<string>();
        }

        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public string DietaryClassification { get; set; }
        public string Allergens { get; set; }
        public bool IsActive { get; set; }

        // "Vegetarian" / "Vegan" / "Halal"
        public List<string> SuitableFor { get; set; }
    }

    public class BuffetGroupCoverage
    {
        public BuffetGroupCoverage()
        {
            Requirements = new List<string>();
            SuitableMeals = new List<string>();
        }

        public string Label { get; set; }
        public List<string> Requirements { get; set; }
        public int StudentCount { get; set; }
        public bool IsUnrestricted { get; set; }
        public List<string> SuitableMeals { get; set; }

        public bool IsCovered { get { return SuitableMeals.Count > 0; } }
    }

    public class BuffetCoverageReport
    {
        public BuffetCoverageReport()
        {
            Meals = new List<BuffetMealOption>();
            Groups = new List<BuffetGroupCoverage>();
            AttendingGroups = new List<BuffetGroupCoverage>();
            GuestDietaryNotes = new List<string>();
            Warnings = new List<string>();
        }

        // Whose requirements were checked, e.g. "All registered students"
        public string Basis { get; set; }

        // "student" or "attendee" — who the counts below are
        public string Noun { get; set; }
        public int StudentCount { get; set; }

        public List<BuffetMealOption> Meals { get; set; }
        public List<BuffetGroupCoverage> Groups { get; set; }

        // Everyone (any type) who has accepted so far, while the
        // check is still against the students invited
        public int AttendingStudentCount { get; set; }
        public List<BuffetGroupCoverage> AttendingGroups { get; set; }

        // Parents, staff and guests who accepted
        public int GuestAttendees { get; set; }
        public int GuestVegetarian { get; set; }
        public int GuestOtherDiets { get; set; }
        public List<string> GuestDietaryNotes { get; set; }

        public List<string> Warnings { get; set; }

        public bool AllGroupsCovered { get { return Warnings.Count == 0; } }
    }

    // A buffet line for the feast plan: the event's own selection,
    // or its template for events scheduled before buffets existed
    public class BuffetLine
    {
        public MenuItem MenuItem { get; set; }
        public string Section { get; set; }
        public decimal QuantityPerGuest { get; set; }
    }

    public class EventBuffetService
    {
        private readonly DBContextClass _db;
        private readonly DietaryProfileService _dietary;
        private readonly MenuCoverageService _coverage;
        private readonly EventRsvpService _rsvp;

        public EventBuffetService(DBContextClass db)
            : this(db, null)
        {
        }

        public EventBuffetService(DBContextClass db, EventRsvpService rsvp)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _dietary = new DietaryProfileService(db);
            _coverage = new MenuCoverageService(db);
            _rsvp = rsvp ?? new EventRsvpService(db);
        }

        private static readonly string[] Preferences =
        {
            DietaryProfileService.PreferenceVegetarian,
            DietaryProfileService.PreferenceVegan,
            DietaryProfileService.PreferenceHalal
        };

        // ============================================================
        // MEALS
        // ============================================================

        private IQueryable<MenuItem> MealsWithIngredients()
        {
            return _db.MenuItems
                .Include(m => m.Recipe.RecipeIngredients.Select(ri => ri.Ingredient));
        }

        // Every active meal in the meal library, for the event form
        public List<BuffetMealOption> AvailableMeals()
        {
            return MealsWithIngredients()
                .Where(m => m.IsActive)
                .OrderBy(m => m.Name)
                .ToList()
                .Select(Describe)
                .ToList();
        }

        public BuffetMealOption Describe(MenuItem item)
        {
            var option = new BuffetMealOption
            {
                MenuItemId = item.Id,
                Name = item.Name,
                DietaryClassification = item.DietaryClassification,
                IsActive = item.IsActive,
                Allergens = string.Join(", ", DietaryProfileService.GetAllergens(
                    item.Recipe != null
                        ? item.Recipe.RecipeIngredients.Where(ri => ri.Ingredient != null).Select(ri => ri.Ingredient)
                        : Enumerable.Empty<Ingredient>()))
            };

            foreach (var pref in Preferences)
            {
                if (_dietary.IsSafe(new StudentDietaryProfile { Preference = pref }, item)) option.SuitableFor.Add(pref);
            }

            return option;
        }

        // The meal ids selected for an event (its own selection, or its
        // template's meals if it has none)
        public List<int> SelectedMealIds(CafeteriaEvent evt)
        {
            return GetBuffetLines(evt).Select(l => l.MenuItem.Id).ToList();
        }

        public List<BuffetLine> GetBuffetLines(CafeteriaEvent evt)
        {
            var own = _db.CafeteriaEventMenuItems
                .Where(b => b.EventId == evt.Id)
                .OrderBy(b => b.SortOrder)
                .ToList();

            if (own.Count > 0)
            {
                var ids = own.Select(b => b.MenuItemId).ToList();
                var meals = MealsWithIngredients().Where(m => ids.Contains(m.Id)).ToList().ToDictionary(m => m.Id);

                return own
                    .Where(b => meals.ContainsKey(b.MenuItemId))
                    .Select(b => new BuffetLine { MenuItem = meals[b.MenuItemId], Section = b.Section, QuantityPerGuest = b.QuantityPerGuest })
                    .ToList();
            }

            if (!evt.MenuTemplateId.HasValue) return new List<BuffetLine>();

            var templateId = evt.MenuTemplateId.Value;
            var template = _db.EventMenuTemplateItems
                .Where(t => t.TemplateId == templateId)
                .OrderBy(t => t.SortOrder)
                .ToList();

            var templateIds = template.Select(t => t.MenuItemId).ToList();
            var templateMeals = MealsWithIngredients().Where(m => templateIds.Contains(m.Id)).ToList().ToDictionary(m => m.Id);

            return template
                .Where(t => templateMeals.ContainsKey(t.MenuItemId))
                .Select(t => new BuffetLine { MenuItem = templateMeals[t.MenuItemId], Section = t.Section, QuantityPerGuest = t.QuantityPerGuest })
                .ToList();
        }

        // Template lines keyed by meal — the form uses them to pre-fill
        public Dictionary<int, List<EventMenuTemplateItem>> TemplateItems()
        {
            return _db.EventMenuTemplateItems
                .Where(t => t.Template.IsActive)
                .ToList()
                .GroupBy(t => t.TemplateId)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.SortOrder).ToList());
        }

        // Replaces the event's buffet. Only active library meals are
        // kept; portions per guest are clamped to 0.1–5.
        // Returns the number of meals saved.
        public int SetBuffetMeals(CafeteriaEvent evt, IDictionary<int, decimal> mealsWithPortions)
        {
            var wanted = mealsWithPortions ?? new Dictionary<int, decimal>();
            var ids = wanted.Keys.ToList();

            var valid = _db.MenuItems
                .Where(m => ids.Contains(m.Id) && m.IsActive)
                .Select(m => m.Id)
                .ToList();

            // Sections from the event's template, when it has the meal
            var sections = new Dictionary<int, string>();
            if (evt.MenuTemplateId.HasValue)
            {
                var templateId = evt.MenuTemplateId.Value;
                foreach (var t in _db.EventMenuTemplateItems.Where(t => t.TemplateId == templateId).ToList())
                {
                    sections[t.MenuItemId] = t.Section;
                }
            }

            var existing = _db.CafeteriaEventMenuItems.Where(b => b.EventId == evt.Id).ToList();
            foreach (var row in existing.Where(r => !valid.Contains(r.MenuItemId)).ToList())
            {
                _db.CafeteriaEventMenuItems.Remove(row);
            }

            int order = 0;
            foreach (var id in ids.Where(valid.Contains))
            {
                var row = existing.FirstOrDefault(r => r.MenuItemId == id);
                if (row == null)
                {
                    row = new CafeteriaEventMenuItem
                    {
                        EventId = evt.Id,
                        MenuItemId = id,
                        Section = sections.ContainsKey(id) ? sections[id] : "Main"
                    };
                    _db.CafeteriaEventMenuItems.Add(row);
                }

                row.QuantityPerGuest = Math.Min(5m, Math.Max(0.1m, Math.Round(wanted[id], 2)));
                row.SortOrder = order++;
            }

            _db.SaveChanges();
            return order;
        }

        // ============================================================
        // DIETARY COVERAGE
        // ============================================================

        // For a saved event. Before the headcount is confirmed: the
        // students invited, plus everyone (any type) who has accepted
        // so far. Afterwards: everyone who accepted — students,
        // parents, staff and guests.
        public BuffetCoverageReport Analyse(CafeteriaEvent evt, ICollection<int> menuItemIds)
        {
            bool headcountConfirmed = evt.Status == EventStatus.RsvpClosed
                || evt.Status == EventStatus.FeastPlanGenerated
                || evt.Status == EventStatus.InProgress
                || evt.Status == EventStatus.Completed;

            var meals = LoadMeals(menuItemIds);
            var attendees = evt.Id > 0 ? _rsvp.GetAttendees(evt.Id) : new List<EventAttendee>();

            var report = new BuffetCoverageReport { Meals = meals.Select(Describe).ToList() };

            if (headcountConfirmed)
            {
                var groups = MenuCoverageService.GroupProfiles(attendees.Select(a => a.Profile));
                report.Basis = "Everyone who accepted (students, parents, staff and guests)";
                report.Noun = "attendee";
                report.StudentCount = attendees.Count;
                report.Groups = CoverGroups(groups, meals);
            }
            else
            {
                List<int> students = evt.InviteStudents ? _rsvp.EligibleStudentIds(evt) : new List<int>();
                report.Basis = !evt.InviteStudents
                    ? "Students are not invited"
                    : EventRsvpService.ParseIds(evt.InviteStudentResidenceIds).Count > 0
                        ? "Invited students (selected houses)"
                        : "All registered students";
                report.Noun = "student";
                report.StudentCount = students.Count;
                report.Groups = CoverGroups(_coverage.LoadPopulation(students), meals);

                if (attendees.Count > 0)
                {
                    report.AttendingStudentCount = attendees.Count;
                    report.AttendingGroups = CoverGroups(MenuCoverageService.GroupProfiles(attendees.Select(a => a.Profile)), meals);
                }
            }

            var others = attendees.Where(a => a.Type != AttendeeType.Student).ToList();
            report.GuestAttendees = others.Count;
            report.GuestVegetarian = others.Count(a => a.Profile.Preference == DietaryProfileService.PreferenceVegetarian
                                                       || a.Profile.Preference == DietaryProfileService.PreferenceVegan);
            report.GuestOtherDiets = others.Count(a => a.Profile.Preference == DietaryProfileService.OtherCode);
            report.GuestDietaryNotes = _db.EventRsvps
                .Where(r => r.EventId == evt.Id && r.ResponseStatus == EventRsvpService.ResponseAttending)
                .Where(r => r.DietaryNotes != null && r.DietaryNotes != "")
                .Select(r => r.ResponderName + ": " + r.DietaryNotes)
                .ToList();

            report.Warnings = BuildWarnings(report, headcountConfirmed);
            return report;
        }

        // For the form, before the event is saved (or while editing)
        public BuffetCoverageReport Preview(int? eventId, bool inviteStudents, string studentHouseIds, ICollection<int> menuItemIds)
        {
            var evt = eventId.HasValue ? _db.CafeteriaEvents.FirstOrDefault(e => e.Id == eventId.Value) : null;

            var probe = new CafeteriaEvent
            {
                Status = evt != null ? evt.Status : EventStatus.Confirmed,
                InviteStudents = inviteStudents,
                InviteStudentResidenceIds = studentHouseIds
            };
            if (evt != null) probe.Id = evt.Id;

            return Analyse(probe, menuItemIds);
        }

        private List<MenuItem> LoadMeals(ICollection<int> menuItemIds)
        {
            var ids = (menuItemIds ?? new List<int>()).Distinct().ToList();
            var meals = MealsWithIngredients().Where(m => ids.Contains(m.Id)).ToList();
            return ids.Select(id => meals.FirstOrDefault(m => m.Id == id)).Where(m => m != null).ToList();
        }

        // Inactive meals can't be served, so they don't count
        private List<BuffetGroupCoverage> CoverGroups(List<DietaryGroup> groups, List<MenuItem> meals)
        {
            var served = meals.Where(m => m.IsActive).ToList();

            return groups.Select(g => new BuffetGroupCoverage
            {
                Label = g.Label,
                Requirements = g.Requirements,
                StudentCount = g.StudentCount,
                IsUnrestricted = g.IsUnrestricted,
                SuitableMeals = served.Where(m => _coverage.Suits(g, m)).Select(m => m.Name).ToList()
            })
            .OrderBy(g => g.IsUnrestricted ? 0 : 1)
            .ThenByDescending(g => g.StudentCount)
            .ToList();
        }

        private static List<string> BuildWarnings(BuffetCoverageReport report, bool headcountConfirmed)
        {
            var warnings = new List<string>();

            if (report.Meals.Count == 0)
            {
                warnings.Add("No buffet meals have been selected for this event.");
                return warnings;
            }

            foreach (var meal in report.Meals.Where(m => !m.IsActive))
            {
                warnings.Add(string.Format("{0} is no longer active in the meal library and won't be served.", meal.Name));
            }

            AddGroupWarnings(warnings, report.Groups, report.Noun, "");

            // While RSVPs are open: parents, staff and guests who have
            // already accepted aren't in the invited-student check
            if (!headcountConfirmed)
            {
                var already = new HashSet<string>(report.Groups.Where(g => !g.IsCovered).Select(g => g.Label));
                var newlyUncovered = report.AttendingGroups
                    .Where(g => !g.IsCovered && g.StudentCount > 0 && !already.Contains(g.Label))
                    .ToList();

                foreach (var g in newlyUncovered)
                {
                    warnings.Add(string.Format("{0} attendee{1} who accepted ({2}) may not have a suitable buffet option.",
                        g.StudentCount, g.StudentCount == 1 ? "" : "s", g.Label));
                }
            }

            return warnings;
        }

        private static void AddGroupWarnings(List<string> warnings, List<BuffetGroupCoverage> groups, string noun, string suffix)
        {
            var uncovered = groups.Where(g => !g.IsCovered && g.StudentCount > 0).ToList();
            string nounPlural = noun + "s";

            bool restrictionMissed = uncovered.Any(g => !(g.Requirements.Count == 1 && Preferences.Contains(g.Requirements[0])));
            if (restrictionMissed)
            {
                warnings.Add(string.Format("Some {0} with known dietary restrictions may not have a suitable buffet option.", nounPlural));
            }

            foreach (var group in uncovered)
            {
                string people = group.StudentCount + " " + (group.StudentCount == 1 ? noun : nounPlural);

                if (group.Requirements.Count == 1 && Preferences.Contains(group.Requirements[0]))
                {
                    warnings.Add(string.Format("No suitable {0} meal has been selected for this event ({1}).",
                        group.Requirements[0].ToLower(CultureInfo.InvariantCulture), people));
                }
                else if (group.Requirements.Count == 1)
                {
                    warnings.Add(string.Format("{0} with {1} ({2}) may not have a suitable buffet option.",
                        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(nounPlural), group.Requirements[0], group.StudentCount));
                }
                else if (group.IsUnrestricted)
                {
                    warnings.Add(string.Format("No selected meal can be served to {0} without dietary restrictions ({1}).", nounPlural, people));
                }
                else
                {
                    warnings.Add(string.Format("{0} ({1}) may not have a suitable buffet option.", people, group.Label));
                }
            }
        }

        // ============================================================
        // KITCHEN REQUIREMENTS — from the accepted RSVPs only
        //
        // Meal choice events: portions = the people who chose each
        // meal (any attendee type, guests included). Anyone without a
        // choice (public-form responses) is given the most chosen
        // meal that is safe for them.
        // Buffet events: portions = attendees × portions per guest.
        // ============================================================

        public EventRequirements CalculateRequirements(CafeteriaEvent evt)
        {
            var attendees = _rsvp.GetAttendees(evt.Id);
            var lines = GetBuffetLines(evt).Where(l => l.MenuItem.IsActive).ToList();

            var result = new EventRequirements
            {
                OffersMealChoice = evt.OffersMealChoice,
                Attendance = new EventAttendance
                {
                    Students = attendees.Count(a => a.Type == AttendeeType.Student),
                    Parents = attendees.Count(a => a.Type == AttendeeType.Parent),
                    Staff = attendees.Count(a => a.Type == AttendeeType.Staff),
                    Guests = attendees.Count(a => a.Type == AttendeeType.Guest)
                }
            };

            var mealLines = lines.Select(l => new EventMealRequirement
            {
                MenuItem = l.MenuItem,
                Section = l.Section,
                QuantityPerGuest = l.QuantityPerGuest
            }).ToList();
            var byId = mealLines.GroupBy(m => m.MenuItem.Id).ToDictionary(g => g.Key, g => g.First());

            if (evt.OffersMealChoice)
            {
                var unassigned = new List<EventAttendee>();

                foreach (var a in attendees)
                {
                    EventMealRequirement line;
                    if (a.MenuItemId.HasValue && byId.TryGetValue(a.MenuItemId.Value, out line))
                    {
                        line.Selected++;
                        line.AddType(a.Type);
                    }
                    else
                    {
                        unassigned.Add(a);
                    }
                }

                // Most chosen first, so extra portions go where demand is
                var ranked = mealLines.OrderByDescending(m => m.Selected).ToList();
                int noSuitable = 0;

                foreach (var a in unassigned)
                {
                    var pick = ranked.FirstOrDefault(m => _dietary.IsSafe(a.Profile, m.MenuItem));
                    if (pick == null) { noSuitable++; continue; }
                    pick.Allocated++;
                }

                result.WithoutChoice = unassigned.Count;

                if (unassigned.Count > 0)
                {
                    result.Notes.Add(string.Format(
                        "{0} attendee{1} didn't choose a meal (public RSVP form) — given the most chosen meal that suits them.",
                        unassigned.Count, unassigned.Count == 1 ? "" : "s"));
                }

                if (noSuitable > 0)
                {
                    result.Warnings.Add(string.Format(
                        "{0} attendee{1} without a meal choice have no suitable meal among this event's meals.",
                        noSuitable, noSuitable == 1 ? "" : "s"));
                }

                foreach (var m in mealLines) m.Portions = m.Selected + m.Allocated;
            }
            else
            {
                int total = attendees.Count;
                foreach (var m in mealLines)
                {
                    m.Portions = total == 0 ? 0 : (int)Math.Ceiling(total * m.QuantityPerGuest);
                }
            }

            result.Meals = mealLines;

            // Dietary summary across every attendee type
            foreach (var a in attendees)
            {
                var p = a.Profile;
                string pref = p.Preference == DietaryProfileService.PreferenceVegetarian ? "Vegetarian"
                    : p.Preference == DietaryProfileService.PreferenceVegan ? "Vegan"
                    : p.Preference == DietaryProfileService.PreferenceHalal ? "Halal"
                    : p.Preference == DietaryProfileService.OtherCode ? "Other requirement"
                    : "No special requirement";
                result.PreferenceCounts[pref] = (result.PreferenceCounts.ContainsKey(pref) ? result.PreferenceCounts[pref] : 0) + 1;

                foreach (var req in MenuCoverageService.FilteringRequirements(p)
                                                       .Where(r => !Preferences.Contains(r)))
                {
                    result.RestrictionCounts[req] = (result.RestrictionCounts.ContainsKey(req) ? result.RestrictionCounts[req] : 0) + 1;
                }
            }

            return result;
        }

        // ============================================================
        // FEAST PLAN — the kitchen's view of the event, built from the
        // RSVPs. Used by the web page and the mobile API.
        // ============================================================

        public FeastPlanViewModel BuildFeastPlan(CafeteriaEvent evt)
        {
            var requirements = CalculateRequirements(evt);

            // UC19: once the Cafeteria Manager approves a feast plan, its
            // dishes and portions replace the RSVP estimate
            var approved = ApprovedFeastPlan(evt.Id);
            if (approved != null) UseApprovedPlan(requirements, approved);

            var vm = new FeastPlanViewModel
            {
                EventId = evt.Id,
                EventName = evt.EventName,
                EventDate = evt.EventDate,
                StartTime = evt.StartTime,
                EndTime = evt.EndTime,
                VenueName = evt.Venue != null ? evt.Venue.Name : "",
                MenuTemplateName = evt.MenuTemplate != null ? evt.MenuTemplate.Name : "",
                TotalGuests = requirements.Attendance.Total,
                Requirements = requirements,
                ApprovedFeastPlanId = approved != null ? approved.Id : (int?)null,
                ApprovedFeastPlanAt = approved != null ? approved.DecidedAt : null
            };

            vm.Coverage = Analyse(evt, requirements.Meals.Where(m => m.MenuItem.Id > 0).Select(m => m.MenuItem.Id).ToList());

            var attending = _db.EventRsvps
                .Where(r => r.EventId == evt.Id && r.ResponseStatus == EventRsvpService.ResponseAttending)
                .ToList();

            vm.StandardGuests = attending.Sum(r => r.StandardCount);
            vm.VegetarianGuests = attending.Sum(r => r.VegetarianCount);
            vm.OtherGuests = attending.Sum(r => r.OtherDietCount);
            vm.DietaryNotes = vm.Coverage.GuestDietaryNotes;

            var ingredientStock = _db.Ingredients
                .Where(i => i.IsActive)
                .ToDictionary(i => i.Id, i => i.FarmAvailableQuantity + i.ExternalAvailableQuantity);

            var serveTime = evt.StartTime;
            var combinedTotals = new Dictionary<int, FeastPlanIngredient>();

            foreach (var item in requirements.Meals)
            {
                int portions = item.Portions;
                if (portions <= 0) continue;   // nobody chose it

                var recipe = item.MenuItem.Recipe;

                var dish = new FeastPlanDish
                {
                    Section = item.Section,
                    DishName = item.MenuItem.Name,
                    Classification = item.MenuItem.DietaryClassification,
                    Portions = portions,
                    QuantityPerGuest = item.QuantityPerGuest,
                    SelectedCount = item.Selected,
                    AllocatedCount = item.Allocated,
                    SelectedByType = item.TypeSummary,
                    Station = recipe != null && !string.IsNullOrWhiteSpace(recipe.Station) ? recipe.Station : "Line",
                    PrepMinutes = recipe != null ? recipe.PrepTimeMinutes : 0,
                    CookMinutes = recipe != null ? recipe.CookTimeMinutes : 0
                };

                int totalMinutes = dish.PrepMinutes + dish.CookMinutes;
                dish.PrepPhase = totalMinutes >= 240 ? FeastPrepPhase.TwoDaysBefore
                    : totalMinutes >= 90 ? FeastPrepPhase.DayBefore
                    : FeastPrepPhase.DayOf;

                var startSpan = serveTime.Subtract(TimeSpan.FromMinutes(dish.CookMinutes));
                dish.StartTime = startSpan < TimeSpan.Zero ? startSpan.Add(TimeSpan.FromHours(24)) : startSpan;
                dish.ReadyTime = serveTime.Subtract(TimeSpan.FromMinutes(5));

                if (recipe != null && recipe.RecipeIngredients != null)
                {
                    foreach (var ri in recipe.RecipeIngredients)
                    {
                        if (ri.Ingredient == null) continue;

                        // Stored per portion (see MealLibraryService)
                        decimal perPortion = ri.QuantityPerStandardPortion;

                        decimal required = perPortion * portions;
                        decimal available = ingredientStock.ContainsKey(ri.IngredientId) ? ingredientStock[ri.IngredientId] : 0m;

                        var line = new FeastPlanIngredient
                        {
                            IngredientId = ri.IngredientId,
                            Name = ri.Ingredient.Name,
                            Unit = ri.Ingredient.Unit,
                            QuantityPerPortion = perPortion,
                            TotalRequired = required,
                            StockAvailable = available,
                            Shortfall = required > available ? required - available : 0m
                        };

                        dish.Ingredients.Add(line);

                        FeastPlanIngredient existing;
                        if (combinedTotals.TryGetValue(line.IngredientId, out existing))
                        {
                            existing.TotalRequired += line.TotalRequired;
                            existing.Shortfall = existing.TotalRequired > existing.StockAvailable
                                ? existing.TotalRequired - existing.StockAvailable
                                : 0m;
                        }
                        else
                        {
                            combinedTotals[line.IngredientId] = new FeastPlanIngredient
                            {
                                IngredientId = line.IngredientId,
                                Name = line.Name,
                                Unit = line.Unit,
                                QuantityPerPortion = line.QuantityPerPortion,
                                TotalRequired = line.TotalRequired,
                                StockAvailable = line.StockAvailable,
                                Shortfall = line.Shortfall
                            };
                        }
                    }
                }

                vm.Dishes.Add(dish);
            }

            vm.TotalIngredients = combinedTotals.Values.OrderBy(x => x.Name).ToList();

            // Already issued from stock: show as covered
            string issueKey = KitchenIssueService.EventKey(evt.Id);
            var issue = _db.KitchenIngredientIssues.FirstOrDefault(i => i.IssueKey == issueKey);
            if (issue != null)
            {
                vm.IngredientsIssuedAt = issue.IssuedAt;
                vm.IngredientsIssuedBy = _db.Users.Where(u => u.UserId == issue.IssuedByUserId).Select(u => u.Name).FirstOrDefault();

                foreach (var line in vm.TotalIngredients.Concat(vm.Dishes.SelectMany(d => d.Ingredients)))
                {
                    line.StockAvailable = line.TotalRequired;
                    line.Shortfall = 0m;
                }
            }

            var eventDate = evt.EventDate.Date;
            foreach (var phase in new[]
            {
                Tuple.Create(FeastPrepPhase.TwoDaysBefore, eventDate.AddDays(-2), "Two days before"),
                Tuple.Create(FeastPrepPhase.DayBefore, eventDate.AddDays(-1), "Day before"),
                Tuple.Create(FeastPrepPhase.DayOf, eventDate, "Day of event")
            })
            {
                var dishes = vm.Dishes.Where(d => d.PrepPhase == phase.Item1).ToList();
                if (dishes.Count > 0)
                {
                    vm.Timeline.Add(new FeastPlanTimelineDay { Phase = phase.Item1, CalendarDate = phase.Item2, DayLabel = phase.Item3, Dishes = dishes });
                }
            }

            return vm;
        }

        // The event's approved UC19 feast plan, if the Cafeteria Manager
        // has approved one (FeastPlanService allows one live plan per event)
        public EventFeastPlan ApprovedFeastPlan(int eventId)
        {
            return _db.EventFeastPlans
                .Include(p => p.Items)
                .Where(p => p.EventId == eventId && p.Status == FeastPlanStatus.Approved)
                .OrderByDescending(p => p.Id)
                .FirstOrDefault();
        }

        // Replaces the RSVP meal lines with the approved plan's dishes.
        // A dish without a library meal has no recipe, so it is listed
        // with its portions but adds no ingredients.
        private void UseApprovedPlan(EventRequirements requirements, EventFeastPlan plan)
        {
            var ids = plan.Items.Where(i => i.MenuItemId.HasValue).Select(i => i.MenuItemId.Value).Distinct().ToList();
            var meals = MealsWithIngredients().Where(m => ids.Contains(m.Id)).ToList().ToDictionary(m => m.Id);
            int guests = Math.Max(1, plan.TotalGuests);

            requirements.Meals = plan.Items
                .Where(i => i.TotalQuantity > 0)
                .OrderBy(i => i.SortOrder)
                .Select(i => new EventMealRequirement
                {
                    MenuItem = i.MenuItemId.HasValue && meals.ContainsKey(i.MenuItemId.Value)
                        ? meals[i.MenuItemId.Value]
                        : new MenuItem { Name = i.DishName },
                    Section = i.Section,
                    QuantityPerGuest = Math.Round((decimal)i.TotalQuantity / guests, 2),
                    Portions = i.TotalQuantity
                })
                .ToList();

            requirements.Notes.Add(string.Format(
                "Dishes and portions come from the approved feast plan (UC19, revision {0}), not the RSVP estimate.",
                plan.Revision));
        }
    }

    // ============================================================
    // Kitchen requirements for an event, from its RSVPs
    // ============================================================

    public class EventRequirements
    {
        public EventRequirements()
        {
            Meals = new List<EventMealRequirement>();
            PreferenceCounts = new Dictionary<string, int>();
            RestrictionCounts = new Dictionary<string, int>();
            Notes = new List<string>();
            Warnings = new List<string>();
        }

        public bool OffersMealChoice { get; set; }
        public EventAttendance Attendance { get; set; }
        public List<EventMealRequirement> Meals { get; set; }

        // "Vegan" → 10, "No special requirement" → 97 …
        public Dictionary<string, int> PreferenceCounts { get; set; }

        // "Peanuts allergy" → 3, "Coeliac disease" → 1 …
        public Dictionary<string, int> RestrictionCounts { get; set; }

        public int WithoutChoice { get; set; }
        public List<string> Notes { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class EventMealRequirement
    {
        public EventMealRequirement()
        {
            ByType = new Dictionary<string, int>();
        }

        public MenuItem MenuItem { get; set; }
        public string Section { get; set; }
        public decimal QuantityPerGuest { get; set; }

        // People who chose it, and how many of each attendee type
        public int Selected { get; set; }
        public Dictionary<string, int> ByType { get; set; }

        // Attendees without a choice given this meal
        public int Allocated { get; set; }

        public int Portions { get; set; }

        public void AddType(string type)
        {
            ByType[type] = (ByType.ContainsKey(type) ? ByType[type] : 0) + 1;
        }

        // e.g. "40 students, 15 parents, 10 staff"
        public string TypeSummary
        {
            get
            {
                var order = new[] { AttendeeType.Student, AttendeeType.Parent, AttendeeType.Staff, AttendeeType.Guest };
                var labels = new Dictionary<string, string[]>
                {
                    { AttendeeType.Student, new[] { "student", "students" } },
                    { AttendeeType.Parent, new[] { "parent", "parents" } },
                    { AttendeeType.Staff, new[] { "staff", "staff" } },
                    { AttendeeType.Guest, new[] { "guest", "guests" } }
                };
                return string.Join(", ", order.Where(ByType.ContainsKey)
                    .Select(t => ByType[t] + " " + (ByType[t] == 1 ? labels[t][0] : labels[t][1])));
            }
        }
    }
}
