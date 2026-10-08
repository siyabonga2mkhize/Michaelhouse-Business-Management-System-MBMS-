using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    // Development-only fixture creation. Protect this action and never expose it in production.
    [Authorize]
    public sealed class DevelopmentSeedController : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult MobileDemo()
        {
            if (!Request.IsLocal) return new HttpStatusCodeResult(404);

            using (var db = new DBContextClass())
            using (var tx = db.Database.BeginTransaction())
            {
                const string password = "Michael@123";
                var parent = db.Parents.FirstOrDefault(x => x.Contact == "test.parent@michaelhouse.co.za") ?? new Parent
                {
                    Name = "TEST Parent",
                    Contact = "test.parent@michaelhouse.co.za",
                    CellPhone = "0000000000",
                    Relationship = "Guardian"
                };
                if (parent.ParentId == 0) db.Parents.Add(parent);
                db.SaveChanges();

                var studentUser = User(db, "test.student@michaelhouse.co.za", "TEST Student", "Student", password);
                var teacherUser = User(db, "test.teacher@michaelhouse.co.za", "TEST Teacher", "Teacher", password);
                var driverUser = User(db, "test.driver@michaelhouse.co.za", "TEST Driver", "Driver", password);
                var housemasterUser = User(db, "test.housemaster@michaelhouse.co.za", "TEST Housemaster", "HouseMaster", password);
                db.SaveChanges();

                var student = db.Students.FirstOrDefault(x => x.StudentNumber == "TEST-001") ?? new Student
                {
                    FirstName = "TEST", LastName = "Student", DOB = new DateTime(2010, 1, 1),
                    IdNumber = "TEST-STUDENT-001", IsBoarding = true, IsActive = true,
                    CurrentGrade = "10", GradeLevel = 10, StudentNumber = "TEST-001",
                    EnrollmentDate = DateTime.Today, ParentId = parent.ParentId, UserId = studentUser.UserId
                };
                if (student.StudentId == 0) db.Students.Add(student);

                if (!db.Teachers.Any(x => x.UserId == teacherUser.UserId)) db.Teachers.Add(new Teacher
                {
                    FirstName = "TEST", LastName = "Teacher", Email = teacherUser.Email,
                    Phone = "0000000000", HireDate = DateTime.Today, UserId = teacherUser.UserId
                });

                var residence = db.Residences.FirstOrDefault(x => x.Name == "TEST House") ?? new Residence
                {
                    Name = "TEST House", Gender = "Mixed", Capacity = 20, OccupiedBeds = 0,
                    GradeCategory = "Senior", IsArchived = false
                };
                if (residence.ResidenceId == 0) db.Residences.Add(residence);
                db.SaveChanges();

                var housemaster = db.HouseMasters.FirstOrDefault(x => x.ContactEmail == housemasterUser.Email) ?? new HouseMaster
                {
                    FullName = "TEST Housemaster", ContactEmail = housemasterUser.Email,
                    ContactPhone = "0000000000", ResidenceId = residence.ResidenceId, IsArchived = false
                };
                if (housemaster.HouseMasterId == 0) db.HouseMasters.Add(housemaster);

                var driver = db.Drivers.FirstOrDefault(x => x.Email == driverUser.Email) ?? new Driver
                {
                    FullName = "TEST Driver", IDNumber = "0000000000001", PhoneNumber = "0000000000",
                    Email = driverUser.Email, LicenceNumber = "TEST-LICENCE", LicenceExpiryDate = DateTime.Today.AddYears(2),
                    HasPDP = true, IsActive = true, DateCreated = DateTime.Now, UserId = driverUser.UserId,
                    PasswordHash = driverUser.PasswordHash
                };
                if (driver.Id == 0) db.Drivers.Add(driver);

                if (!db.Vehicles.Any(x => x.VehicleNumber == "TEST-VEH-001")) db.Vehicles.Add(new Vehicle
                {
                    VehicleNumber = "TEST-VEH-001", Model = "Test Shuttle", Type = "Van",
                    Capacity = 12, IsActive = true, DateAdded = DateTime.Today
                });

                db.SaveChanges();
                tx.Commit();

                return Content("TEST mobile data ready\n" +
                    "Student: test.student@michaelhouse.co.za / " + password + "\n" +
                    "Teacher: test.teacher@michaelhouse.co.za / " + password + "\n" +
                    "Driver: test.driver@michaelhouse.co.za / " + password + "\n" +
                    "Housemaster: test.housemaster@michaelhouse.co.za / " + password + "\n" +
                    "Residence: TEST House\nVehicle: TEST-VEH-001", "text/plain");
            }
        }

        // ============================================================
        // UC19 demo data (local only). Creates ONE event whose RSVPs have
        // closed, with parents and staff, their guests, mixed diets and
        // cultural favourite dishes, so the Generate Feast Plan walkthrough
        // can start straight away.
        //
        // Visit  /DevelopmentSeed/FeastPlanDemo   (safe to open twice)
        // ============================================================
        [HttpGet]
        [AllowAnonymous]
        public ActionResult FeastPlanDemo(string again)
        {
            if (!Request.IsLocal) return new HttpStatusCodeResult(404);

            string eventName = "Feast Plan Demo - Founders' Day Buffet";
            const string loginNote = "Log in as cafeteria@michaelhouse.co.za / Cafeteria@123 and open /FeastPlan";
            bool another = !string.IsNullOrWhiteSpace(again);
            int dayOffset = 0;

            using (var db = new DBContextClass())
            {
                var existing = db.CafeteriaEvents.FirstOrDefault(e => e.EventName == eventName);
                if (existing != null && !another)
                {
                    return Content("The demo event already exists (event " + existing.Id + ").\n" +
                        "To create ANOTHER demo event (a fresh one to plan from the start), add  ?again=1  to the address.\n" + loginNote, "text/plain");
                }

                if (another)
                {
                    // A new, separate event: "... #2", "... #3" and so on, a day apart
                    int count = db.CafeteriaEvents.Count(e => e.EventName.StartsWith(eventName));
                    eventName = eventName + " #" + (count + 1);
                    dayOffset = count;
                }

                // The meal library needs colour and texture tags (SRS precondition)
                var analysis = new FeastAnalysisService(db);
                int tagged = analysis.ApplyStarterTags();

                // On a fresh database the Seed never creates the meal library: its calls to
                // SeedMenuItems / SeedRecipesAndIngredients sit inside a loop over the five
                // seeded students, and a fresh database has none. Create them here if missing.
                string libraryNote = "Meal library already had meals with recipes.";
                if (db.MenuItems.Count(m => m.IsActive && m.RecipeId != null) < 6)
                {
                    libraryNote = EnsureMealLibrary(db);
                }

                // The buffet is built from the meal library: meals that have a recipe.
                // (The Seed creates the event templates before any meals exist, so the
                // templates can be empty on a fresh database.)
                var libraryAll = db.MenuItems
                    .Include("Recipe.RecipeIngredients.Ingredient")
                    .Where(m => m.IsActive && m.RecipeId != null)
                    .ToList()
                    .Where(m => m.Recipe != null && m.Recipe.RecipeIngredients != null && m.Recipe.RecipeIngredients.Count > 0)
                    .OrderBy(m => m.Name)
                    .ToList();

                var standardMains = libraryAll.Where(m => FeastPlanService.DefaultCategory("Main", m) == FeastDishCategory.Standard).ToList();
                var vegetarianMains = libraryAll.Where(m => FeastPlanService.DefaultCategory("Main", m) == FeastDishCategory.Vegetarian).ToList();
                var halalMains = libraryAll.Where(m => FeastPlanService.DefaultCategory("Main", m) == FeastDishCategory.Halal).ToList();

                // Mains: 3 standard, 1 vegetarian, 1 halal (as many as the library has)
                var mains = new List<MenuItem>();
                mains.AddRange(standardMains.Take(3));
                mains.AddRange(vegetarianMains.Take(1));
                mains.AddRange(halalMains.Take(1));

                var rest = libraryAll.Where(m => !mains.Contains(m)).ToList();
                var sides = rest.Skip(1).Take(2).ToList();
                var desserts = rest.Take(1).ToList();

                if (mains.Count < 2)
                {
                    return Content("The meal library has too few meals with recipes (" + libraryAll.Count + " found). Run Update-Database so the Seed method creates them, then try again.", "text/plain");
                }

                var buffet = new List<DemoBuffetRow>();
                foreach (var m in mains) buffet.Add(new DemoBuffetRow { Meal = m, Section = "Main" });
                foreach (var m in sides) buffet.Add(new DemoBuffetRow { Meal = m, Section = "Side" });
                foreach (var m in desserts) buffet.Add(new DemoBuffetRow { Meal = m, Section = "Dessert" });

                // Make the colour balance check show something: the first three mains
                // share a colour (the coordinator can still change any tag)
                foreach (var m in mains.Take(3))
                {
                    var tag = db.MenuItemBuffetTags.FirstOrDefault(t => t.MenuItemId == m.Id);
                    if (tag != null) tag.Colour = "Brown";
                }
                db.SaveChanges();

                var venue = db.EventVenues.OrderByDescending(v => v.Capacity).FirstOrDefault();
                var manager = db.Users.FirstOrDefault(u => u.Role == "CafeteriaManager");
                var today = SchoolClock.Today;

                var evt = new CafeteriaEvent
                {
                    EventName = eventName,
                    Description = "Demo data for the Generate Feast Plan walkthrough.",
                    EventType = EventType.FoundersDay,
                    EventDate = today.AddDays(14 + dayOffset),
                    StartTime = new TimeSpan(12, 0, 0),
                    EndTime = new TimeSpan(15, 0, 0),
                    VenueId = venue == null ? (int?)null : venue.Id,
                    ExpectedHeadcount = 40,
                    Status = EventStatus.FeastPlanGenerated,
                    CreatedByUserId = manager == null ? 1 : manager.UserId,
                    AllowGuests = true,
                    MaxGuestsPerInvitee = 3,
                    InviteParents = true,
                    InviteStaff = true,
                    RsvpOpenedAt = DateTime.UtcNow.AddDays(-10),
                    RsvpDeadline = DateTime.UtcNow.AddDays(-1),
                    RsvpClosedAt = DateTime.UtcNow.AddHours(-12)
                };
                db.CafeteriaEvents.Add(evt);
                db.SaveChanges();

                int order = 1;
                foreach (var row in buffet)
                {
                    db.CafeteriaEventMenuItems.Add(new CafeteriaEventMenuItem
                    {
                        EventId = evt.Id,
                        MenuItemId = row.Meal.Id,
                        Section = row.Section,
                        QuantityPerGuest = 1.0m,
                        SortOrder = order++
                    });
                }
                db.SaveChanges();

                // Cultural favourites: one meal the kitchen has stock for, one it is short of,
                // and one dish that isn't in the meal library at all
                var onBuffet = new HashSet<int>(buffet.Select(r => r.Meal.Id));
                var library = libraryAll;
                var projected = analysis.ProjectedStock(evt.EventDate, evt.Id);

                MenuItem availableMeal = null;
                MenuItem shortMeal = null;
                foreach (var meal in library.Where(m => !onBuffet.Contains(m.Id)).OrderBy(m => m.Name))
                {
                    bool isShort = FeastAnalysisService.Requirement(meal, 4).Any(need =>
                    {
                        ProjectedStockLine line;
                        return !projected.TryGetValue(need.Key.Id, out line) || need.Value > line.Projected;
                    });

                    if (isShort) { if (shortMeal == null) shortMeal = meal; }
                    else if (availableMeal == null) availableMeal = meal;

                    if (availableMeal != null && shortMeal != null) break;
                }

                string notListed = "Koeksisters";
                if (FeastAnalysisService.MatchMeal(FeastAnalysisService.NormaliseName(notListed), library) != null)
                {
                    notListed = "Ouma's secret milk tart surprise";
                }

                string favA = availableMeal != null ? availableMeal.Name : "";
                string favB = shortMeal != null ? shortMeal.Name : "";

                // name | group | own diet | guests as "diet:favourite" (A = available, B = short, C = not listed)
                var people = new[]
                {
                    new DemoInvitee("Nomsa Dlamini", "Parent", "None", "None:A", "Vegetarian:"),
                    new DemoInvitee("Thabo Mokoena", "Parent", "Halal", "Halal:", "Halal:B"),
                    new DemoInvitee("Sarah van der Merwe", "Staff", "None", "None:A", "None:C"),
                    new DemoInvitee("James Ndlovu", "Parent", "Vegetarian", "Vegetarian:", "None:"),
                    new DemoInvitee("Priya Naidoo", "Parent", "Vegetarian", "Vegetarian:"),
                    new DemoInvitee("Ayesha Patel", "Staff", "Halal", "Halal:C"),
                    new DemoInvitee("Pieter Botha", "Parent", "None", "None:", "None:", "None:A"),
                    new DemoInvitee("Lindiwe Khumalo", "Parent", "None", "None:"),
                    new DemoInvitee("Michael Smith", "Staff", "None"),
                    new DemoInvitee("Zanele Zulu", "Parent", "Vegan", "Vegetarian:"),
                    new DemoInvitee("Ruth Fourie", "Staff", "None", "None:B")
                };

                int userId = 910000;
                foreach (var person in people)
                {
                    var rsvp = new EventRsvp
                    {
                        EventId = evt.Id,
                        RespondedByUserId = userId++,
                        ResponderName = person.Name,
                        ResponderEmail = person.Name.ToLowerInvariant().Replace(" ", ".") + "@example.com",
                        ResponderGroup = person.Group,
                        DietaryPreference = person.Diet,
                        GuestCount = person.Guests.Count,
                        TotalAttendees = 1 + person.Guests.Count,
                        ResponseStatus = EventRsvpService.ResponseAttending,
                        RespondedAt = DateTime.UtcNow.AddDays(-5)
                    };

                    foreach (var g in person.Guests)
                    {
                        rsvp.Guests.Add(new EventRsvpGuest
                        {
                            DietaryPreference = g[0],
                            CulturalFavoriteDish = g[1] == "A" ? favA : g[1] == "B" ? favB : g[1] == "C" ? notListed : null
                        });
                    }

                    db.EventRsvps.Add(rsvp);
                }

                // Someone who can't come
                db.EventRsvps.Add(new EventRsvp
                {
                    EventId = evt.Id,
                    RespondedByUserId = userId++,
                    ResponderName = "Declined Guest",
                    ResponderEmail = "declined.guest@example.com",
                    ResponderGroup = "Parent",
                    DietaryPreference = "None",
                    TotalAttendees = 0,
                    ResponseStatus = EventRsvpService.ResponseDeclined,
                    RespondedAt = DateTime.UtcNow.AddDays(-4)
                });

                db.SaveChanges();

                int attending = new EventRsvpService(db).GetAttendees(evt.Id).Count;
                evt.GuaranteedHeadcount = attending;
                db.SaveChanges();

                return Content(
                    "Feast Plan demo data ready.\n" +
                    "Event: " + eventName + " (event " + evt.Id + "), " + evt.EventDate.ToString("dd MMM yyyy") + ", RSVPs closed\n" +
                    "Attending guests: " + attending + "\n" +
                    libraryNote + "\n" +
                    "Buffet meals: " + buffet.Count + " (" + mains.Count + " mains), starter colour/texture tags added to " + tagged + " meals\n" +
                    "Favourite that should be Available: " + (favA.Length > 0 ? favA : "(none found)") + "\n" +
                    "Favourite that should be Not available: " + (favB.Length > 0 ? favB : "(none found: every meal has enough stock)") + "\n" +
                    "Favourite that should be Not listed: " + notListed + "\n\n" + loginNote,
                    "text/plain");
            }
        }

        // Runs the Seed's own meal, recipe and ingredient seeding (private methods
        // in Configuration) and the cafeteria inventory seed, then reports what it did.
        private static string EnsureMealLibrary(DBContextClass db)
        {
            var steps = new List<string>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var cfg = new global::Configuration();

            foreach (var name in new[] { "SeedMenuItems", "SeedRecipesAndIngredients" })
            {
                try
                {
                    var method = typeof(global::Configuration).GetMethod(name, flags);
                    method.Invoke(cfg, new object[] { db });
                    steps.Add(name + " ok");
                }
                catch (TargetInvocationException ex)
                {
                    var inner = ex.InnerException ?? ex;
                    steps.Add(name + " FAILED: " + inner.Message);
                }
                catch (Exception ex)
                {
                    steps.Add(name + " FAILED: " + ex.Message);
                }
            }

            try
            {
                CafeteriaInventorySeed.Run(db);
                steps.Add("CafeteriaInventorySeed ok");
            }
            catch (Exception ex)
            {
                steps.Add("CafeteriaInventorySeed FAILED: " + ex.Message);
            }

            int meals = db.MenuItems.Count(m => m.IsActive && m.RecipeId != null);
            return "Meal library created here (" + meals + " meals with recipes): " + string.Join("; ", steps) + ".";
        }

        private sealed class DemoBuffetRow
        {
            public MenuItem Meal;
            public string Section;
        }

        private sealed class DemoInvitee
        {
            public readonly string Name;
            public readonly string Group;
            public readonly string Diet;
            public readonly List<string[]> Guests = new List<string[]>();

            public DemoInvitee(string name, string group, string diet, params string[] guests)
            {
                Name = name;
                Group = group;
                Diet = diet;
                foreach (var g in guests)
                {
                    var parts = g.Split(':');
                    Guests.Add(new[] { parts[0], parts.Length > 1 ? parts[1] : "" });
                }
            }
        }

        static AppUser User(DBContextClass db, string email, string name, string role, string password)
        {
            var user = db.Users.FirstOrDefault(x => x.Email == email);
            if (user != null) return user;
            user = new AppUser { Email = email, Name = name, Role = role, PasswordHash = AccountController.HashPassword(password) };
            db.Users.Add(user);
            return user;
        }
    }
}
