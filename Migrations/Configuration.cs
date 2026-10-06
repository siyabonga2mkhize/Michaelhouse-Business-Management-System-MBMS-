using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.IO;
using System.Linq;
using System.Reflection;
using Michaelhouse.Models.Cafeteria;

internal sealed class Configuration : DbMigrationsConfiguration<Michaelhouse.Models.DBContextClass>
{
    public Configuration()
    {
        AutomaticMigrationsEnabled = false;
        AutomaticMigrationDataLossAllowed = false;
    }

    protected override void Seed(Michaelhouse.Models.DBContextClass context)
    {        // =============================================================
        // UC17: Seed Event Venues and Menu Templates
        // Placed at the top so it always runs regardless of what
        // else is already seeded.
        // =============================================================
        if (!context.EventVenues.Any())
        {
            context.EventVenues.AddRange(new[]
            {
                new EventVenue { Name = "Centenary Hall",        Capacity = 400, Location = "Main Quad",          Notes = "Formal dinners, valedictory, large gatherings" },
                new EventVenue { Name = "Dining Hall",           Capacity = 300, Location = "Main Building",      Notes = "Daily meals, house dinners" },
                new EventVenue { Name = "Founders House Garden", Capacity = 150, Location = "Founders House",     Notes = "Outdoor lunches, braais, teas" },
                new EventVenue { Name = "Chapel Lawn",           Capacity = 250, Location = "Next to Chapel",     Notes = "Sunday roast, outdoor services" },
                new EventVenue { Name = "Sports Pavilion",       Capacity = 180, Location = "Main Sports Field", Notes = "Match-day teas, braais, prize-givings" },
                new EventVenue { Name = "Headmaster's Garden",   Capacity = 80,  Location = "Headmaster's House", Notes = "Intimate functions, staff teas" },
                new EventVenue { Name = "Old Boys' Club",        Capacity = 120, Location = "East Wing",          Notes = "Reunions, informal dinners" },
                new EventVenue { Name = "Tatham House Common",   Capacity = 60,  Location = "Tatham House",       Notes = "House dinners, small buffets" },
                new EventVenue { Name = "Baines House Common",   Capacity = 60,  Location = "Baines House",       Notes = "House dinners, small buffets" }
            });
            context.SaveChanges();
        }

        if (!context.EventMenuTemplates.Any())
        {
            Func<string, MenuItem> findItem = name =>
                context.MenuItems.FirstOrDefault(m => m.Name == name);

            // ── 1. Sports Day Braai ────────────────────────────────
            var braai = new EventMenuTemplate
            {
                Name = "Sports Day Braai",
                Description = "Classic South African braai for match days and sports events.",
                DefaultEventType = EventType.SportsDay,
                DefaultHeadcount = 200,
                MinGuests = 20,
                MaxGuests = 500,
                IsActive = true
            };
            foreach (var row in new[]
            {
                new { Name = "Beef & Vegetable Stir-Fry", Qty = 1.20m, Section = "Main",    Order = 1 },
                new { Name = "Chicken & Brown Rice Bowl", Qty = 1.00m, Section = "Main",    Order = 2 },
                new { Name = "Chickpea & Rice Bowl",      Qty = 0.60m, Section = "Main",    Order = 3 },
                new { Name = "Peanut Butter Toast & Fruit", Qty = 0.40m, Section = "Dessert", Order = 4 }
            })
            {
                var item = findItem(row.Name);
                if (item == null) continue;
                braai.Items.Add(new EventMenuTemplateItem
                {
                    MenuItemId = item.Id,
                    QuantityPerGuest = row.Qty,
                    Section = row.Section,
                    SortOrder = row.Order
                });
            }
            context.EventMenuTemplates.Add(braai);
            context.SaveChanges();

            // ── 2. Chapel Sunday Roast ─────────────────────────────
            var roast = new EventMenuTemplate
            {
                Name = "Chapel Sunday Roast",
                Description = "Traditional Sunday roast after Chapel service.",
                DefaultEventType = EventType.ChapelSundayRoast,
                DefaultHeadcount = 120,
                MinGuests = 20,
                MaxGuests = 300,
                IsActive = true
            };
            foreach (var row in new[]
            {
                new { Name = "Beef Lasagne & Garden Salad", Qty = 1.00m, Section = "Main",    Order = 1 },
                new { Name = "Chicken & Brown Rice Bowl",   Qty = 0.80m, Section = "Main",    Order = 2 },
                new { Name = "Vegetable Omelette & Toast",  Qty = 0.30m, Section = "Main",    Order = 3 },
                new { Name = "French Toast & Fresh Fruit",  Qty = 0.50m, Section = "Dessert", Order = 4 }
            })
            {
                var item = findItem(row.Name);
                if (item == null) continue;
                roast.Items.Add(new EventMenuTemplateItem
                {
                    MenuItemId = item.Id,
                    QuantityPerGuest = row.Qty,
                    Section = row.Section,
                    SortOrder = row.Order
                });
            }
            context.EventMenuTemplates.Add(roast);
            context.SaveChanges();

            // ── 3. Formal Dinner ───────────────────────────────────
            var formal = new EventMenuTemplate
            {
                Name = "Formal Dinner",
                Description = "Plated three-course dinner for Founders' Day, Valedictory, and formal functions.",
                DefaultEventType = EventType.FormalDinner,
                DefaultHeadcount = 80,
                MinGuests = 20,
                MaxGuests = 300,
                IsActive = true
            };
            foreach (var row in new[]
            {
                new { Name = "Vegetable Omelette & Toast",  Qty = 0.20m, Section = "Starter", Order = 1 },
                new { Name = "Beef Lasagne & Garden Salad", Qty = 1.00m, Section = "Main",    Order = 2 },
                new { Name = "Chicken & Brown Rice Bowl",   Qty = 0.70m, Section = "Main",    Order = 3 },
                new { Name = "Chickpea & Rice Bowl",        Qty = 0.30m, Section = "Main",    Order = 4 },
                new { Name = "French Toast & Fresh Fruit",  Qty = 0.80m, Section = "Dessert", Order = 5 }
            })
            {
                var item = findItem(row.Name);
                if (item == null) continue;
                formal.Items.Add(new EventMenuTemplateItem
                {
                    MenuItemId = item.Id,
                    QuantityPerGuest = row.Qty,
                    Section = row.Section,
                    SortOrder = row.Order
                });
            }
            context.EventMenuTemplates.Add(formal);
            context.SaveChanges();

            // ── 4. Parents' Tea ────────────────────────────────────
            var tea = new EventMenuTemplate
            {
                Name = "Parents' Tea",
                Description = "Light tea with sandwiches and scones — for visiting parents' weekends.",
                DefaultEventType = EventType.ParentsWeekend,
                DefaultHeadcount = 60,
                MinGuests = 10,
                MaxGuests = 150,
                IsActive = true
            };
            foreach (var row in new[]
            {
                new { Name = "Peanut Butter Toast & Fruit", Qty = 1.00m, Section = "Savoury", Order = 1 },
                new { Name = "Breakfast Wrap",              Qty = 0.80m, Section = "Savoury", Order = 2 },
                new { Name = "French Toast & Fresh Fruit",  Qty = 0.60m, Section = "Sweet",   Order = 3 },
                new { Name = "Weet-Bix, Banana & Milk",     Qty = 0.30m, Section = "Sweet",   Order = 4 }
            })
            {
                var item = findItem(row.Name);
                if (item == null) continue;
                tea.Items.Add(new EventMenuTemplateItem
                {
                    MenuItemId = item.Id,
                    QuantityPerGuest = row.Qty,
                    Section = row.Section,
                    SortOrder = row.Order
                });
            }
            context.EventMenuTemplates.Add(tea);
            context.SaveChanges();

            // ── 5. Founders' Day Lunch ─────────────────────────────
            var founders = new EventMenuTemplate
            {
                Name = "Founders' Day Lunch",
                Description = "Buffet lunch for Founders' Day — the school's flagship event.",
                DefaultEventType = EventType.FoundersDay,
                DefaultHeadcount = 150,
                MinGuests = 50,
                MaxGuests = 400,
                IsActive = true
            };
            foreach (var row in new[]
            {
                new { Name = "Beef Lasagne & Garden Salad", Qty = 0.80m, Section = "Main",    Order = 1 },
                new { Name = "Chicken & Brown Rice Bowl",   Qty = 0.90m, Section = "Main",    Order = 2 },
                new { Name = "Chickpea & Rice Bowl",        Qty = 0.50m, Section = "Main",    Order = 3 },
                new { Name = "Vegetable Omelette & Toast",  Qty = 0.30m, Section = "Main",    Order = 4 },
                new { Name = "French Toast & Fresh Fruit",  Qty = 0.70m, Section = "Dessert", Order = 5 }
            })
            {
                var item = findItem(row.Name);
                if (item == null) continue;
                founders.Items.Add(new EventMenuTemplateItem
                {
                    MenuItemId = item.Id,
                    QuantityPerGuest = row.Qty,
                    Section = row.Section,
                    SortOrder = row.Order
                });
            }
            context.EventMenuTemplates.Add(founders);
            context.SaveChanges();
        }

        //  This method will be called after migrating to the latest version.

        // ──────────────────────────────────────────────────────────────
        // Ensure a Teacher exists before seeding trips (from HEAD)
        // ──────────────────────────────────────────────────────────────
        if (!context.Users.Any(u => u.Role == "Teacher"))
        {
            var teacherUser = new AppUser
            {
                Name = "System Teacher",
                Email = "teacher@michaelhouse.co.za",
                PasswordHash = HashPassword("Teacher@123"),
                Role = "Teacher"
            };
            context.Users.Add(teacherUser);
            context.SaveChanges();

            if (!context.Teachers.Any(t => t.UserId == teacherUser.UserId))
            {
                context.Teachers.Add(new Teacher
                {
                    FirstName = "System",
                    LastName = "Teacher",
                    Email = teacherUser.Email,
                    HireDate = DateTime.Now,
                    UserId = teacherUser.UserId
                });
                context.SaveChanges();
            }
        }

        if (!context.Teachers.Any())
        {
            var teacherUser = context.Users.FirstOrDefault(u => u.Role == "Teacher");
            if (teacherUser == null)
            {
                teacherUser = new AppUser
                {
                    Name = "Default Teacher",
                    Email = "default.teacher@michaelhouse.co.za",
                    PasswordHash = HashPassword("Teacher@123"),
                    Role = "Teacher"
                };
                context.Users.Add(teacherUser);
                context.SaveChanges();
            }

            context.Teachers.Add(new Teacher
            {
                FirstName = "Default",
                LastName = "Teacher",
                Email = teacherUser.Email,
                HireDate = DateTime.Now,
                UserId = teacherUser.UserId
            });
            context.SaveChanges();
        }

        // ──────────────────────────────────────────────────────────────
        // Seed trips, vehicles, etc. (from HEAD)
        // ──────────────────────────────────────────────────────────────
        try
        {
            var vehicleNumbers = new[] { "MH-TRIP-001", "MH-TRIP-002", "MH-TRIP-003" };
            foreach (var vn in vehicleNumbers)
            {
                if (!context.Vehicles.Any(v => v.VehicleNumber == vn))
                {
                    context.Vehicles.Add(new Vehicle
                    {
                        VehicleNumber = vn,
                        Model = "Mercedes Sprinter",
                        Type = "Bus",
                        Capacity = 22,
                        IsActive = true,
                        DateAdded = DateTime.Now
                    });
                }
            }
            context.SaveChanges();

            var drivers = context.Drivers.ToList();
            var students = context.Students.Take(10).ToList();
            var teacherId = context.Teachers.Select(t => t.TeacherId).FirstOrDefault();
            var vehicles = context.Vehicles.ToList();

            int vIndex = 0;
            foreach (var drv in drivers)
            {
                // upcoming trip
                var upDate = DateTime.Today.AddDays(3 + vIndex);
                bool upExists = context.TripSchedules.Any(ts => ts.DriverId == drv.Id && DbFunctions.TruncateTime(ts.ScheduledDate) == DbFunctions.TruncateTime(upDate));
                if (!upExists)
                {
                    var req = new TripRequest
                    {
                        TeacherId = teacherId,
                        Title = $"Excursion - Park Visit ({drv.FullName.Split(' ')[0]})",
                        Description = "Seeded upcoming trip",
                        DepartureTime = upDate.AddHours(8),
                        ReturnTime = upDate.AddHours(14),
                        Destination = "City Park",
                        MaxStudents = 20,
                        Status = "Scheduled",
                        RequestedAt = DateTime.Now
                    };
                    context.TripRequests.Add(req);
                    context.SaveChanges();

                    var sched = new TripSchedule
                    {
                        TripRequestId = req.Id,
                        TeacherId = teacherId,
                        ScheduledDate = upDate,
                        Status = "Confirmed",
                        DriverId = drv.Id,
                        VehicleId = vehicles[vIndex % vehicles.Count].Id,
                        Notes = "Seeded upcoming trip"
                    };
                    context.TripSchedules.Add(sched);
                    context.SaveChanges();

                    context.TripVehicleAssignments.Add(new TripVehicleAssignment
                    {
                        TripScheduleId = sched.Id,
                        VehicleId = sched.VehicleId.Value,
                        DriverId = drv.Id,
                        AllocatedSeats = Math.Min(20, vehicles[vIndex % vehicles.Count].Capacity),
                        CreatedAt = DateTime.Now
                    });
                    context.SaveChanges();

                    foreach (var st in students.Take(5))
                    {
                        context.TripStudents.Add(new TripStudent
                        {
                            TripScheduleId = sched.Id,
                            StudentId = st.StudentId,
                            IsPresentBefore = null,
                            IsPresentAfter = null
                        });
                    }
                    context.SaveChanges();
                }

                // past completed trip
                var pastDate = DateTime.Today.AddDays(-7 - vIndex);
                bool pastExists = context.TripSchedules.Any(ts => ts.DriverId == drv.Id && DbFunctions.TruncateTime(ts.ScheduledDate) == DbFunctions.TruncateTime(pastDate));
                if (!pastExists)
                {
                    var req2 = new TripRequest
                    {
                        TeacherId = teacherId,
                        Title = $"Completed Trip - Museum ({drv.FullName.Split(' ')[0]})",
                        Description = "Seeded completed trip",
                        DepartureTime = pastDate.AddHours(9),
                        ReturnTime = pastDate.AddHours(16),
                        Destination = "National Museum",
                        MaxStudents = 25,
                        Status = "Scheduled",
                        RequestedAt = DateTime.Now.AddDays(-10)
                    };
                    context.TripRequests.Add(req2);
                    context.SaveChanges();

                    var sched2 = new TripSchedule
                    {
                        TripRequestId = req2.Id,
                        TeacherId = teacherId,
                        ScheduledDate = pastDate,
                        Status = "Completed",
                        DriverId = drv.Id,
                        VehicleId = vehicles[(vIndex + 1) % vehicles.Count].Id,
                        Notes = "Seeded completed trip"
                    };
                    context.TripSchedules.Add(sched2);
                    context.SaveChanges();

                    context.TripVehicleAssignments.Add(new TripVehicleAssignment
                    {
                        TripScheduleId = sched2.Id,
                        VehicleId = sched2.VehicleId.Value,
                        DriverId = drv.Id,
                        AllocatedSeats = Math.Min(20, vehicles[(vIndex + 1) % vehicles.Count].Capacity),
                        CreatedAt = DateTime.Now.AddDays(-7)
                    });
                    context.SaveChanges();

                    var assigned = students.Skip(vIndex).Take(5).ToList();
                    foreach (var st in assigned)
                    {
                        var ts = new TripStudent
                        {
                            TripScheduleId = sched2.Id,
                            StudentId = st.StudentId,
                            IsPresentBefore = true,
                            IsPresentAfter = true,
                            MarkedBeforeAt = pastDate.AddHours(8).AddMinutes(10),
                            MarkedAfterAt = pastDate.AddHours(16).AddMinutes(5),
                            MarkedBeforeBy = drv.UserId?.ToString(),
                            MarkedAfterBy = drv.UserId?.ToString()
                        };
                        context.TripStudents.Add(ts);
                    }
                    context.SaveChanges();
                }

                vIndex++;
            }
        }
        catch { }

        // ----------------------------
        // Seed a sample trip + manifest for Sibusiso (driver)
        // ----------------------------
        try
        {
            var sibusiso = context.Drivers.FirstOrDefault(d => d.FullName == "Sibusiso Dlamini" || d.Email.StartsWith("sibusiso@"));
            if (sibusiso != null)
            {
                if (!context.Students.Any())
                {
                    var sampleParents = new[]
                    {
                            new Parent { Name = "Amina Khan", Contact = "amina.parent@example.local", CellPhone = "0711000001", Relationship = "Mother", EmergencyContactName = "Mrs Khan", EmergencyContactPhone = "0712000001" },
                            new Parent { Name = "Thabo Ntuli", Contact = "thabo.parent@example.local", CellPhone = "0711000002", Relationship = "Father", EmergencyContactName = "Mr Ntuli", EmergencyContactPhone = "0712000002" },
                            new Parent { Name = "Lindiwe Mthembu", Contact = "lindiwe.parent@example.local", CellPhone = "0711000003", Relationship = "Mother", EmergencyContactName = "Mrs Mthembu", EmergencyContactPhone = "0712000003" },
                            new Parent { Name = "Sipho Zulu", Contact = "sipho.parent@example.local", CellPhone = "0711000004", Relationship = "Father", EmergencyContactName = "Mr Zulu", EmergencyContactPhone = "0712000004" },
                            new Parent { Name = "Nomsa Dlamini", Contact = "nomsa.parent@example.local", CellPhone = "0711000005", Relationship = "Mother", EmergencyContactName = "Mrs Dlamini", EmergencyContactPhone = "0712000005" }
                        };
                    context.Parents.AddRange(sampleParents);
                    context.SaveChanges();

                    var parentsList = context.Parents.OrderBy(p => p.ParentId).Take(5).ToList();

                    var sampleStudents = new[]
                    {
                            new Student { FirstName = "Amina", LastName = "Khan", DOB = DateTime.Today.AddYears(-14), CurrentGrade = "Grade 8", ParentId = parentsList[0].ParentId, StudentNumber = "S0001", GradeLevel = 8, EnrollmentDate = DateTime.Now },
                            new Student { FirstName = "Thabo", LastName = "Ntuli", DOB = DateTime.Today.AddYears(-15), CurrentGrade = "Grade 9", ParentId = parentsList[1].ParentId, StudentNumber = "S0002", GradeLevel = 9, EnrollmentDate = DateTime.Now },
                            new Student { FirstName = "Lindiwe", LastName = "Mthembu", DOB = DateTime.Today.AddYears(-16), CurrentGrade = "Grade 10", ParentId = parentsList[2].ParentId, StudentNumber = "S0003", GradeLevel = 10, EnrollmentDate = DateTime.Now },
                            new Student { FirstName = "Sipho", LastName = "Zulu", DOB = DateTime.Today.AddYears(-13), CurrentGrade = "Grade 8", ParentId = parentsList[3].ParentId, StudentNumber = "S0004", GradeLevel = 8, EnrollmentDate = DateTime.Now },
                            new Student { FirstName = "Nomsa", LastName = "Dlamini", DOB = DateTime.Today.AddYears(-17), CurrentGrade = "Grade 11", ParentId = parentsList[4].ParentId, StudentNumber = "S0005", GradeLevel = 11, EnrollmentDate = DateTime.Now }
                        };
                    context.Students.AddRange(sampleStudents);
                    context.SaveChanges();
                }

                var scheduledDate = DateTime.Today.AddDays(3);

                bool exists = context.TripSchedules.Any(ts => ts.DriverId == sibusiso.Id && DbFunctions.TruncateTime(ts.ScheduledDate) == DbFunctions.TruncateTime(scheduledDate));
                if (!exists)
                {
                    var tripReq = new TripRequest
                    {
                        TeacherId = context.Teachers.Select(t => t.TeacherId).FirstOrDefault(),
                        Title = "School Excursion - Historical Museum",
                        Description = "Educational trip to the Historical Museum",
                        DepartureTime = scheduledDate.AddHours(9),
                        ReturnTime = scheduledDate.AddHours(16),
                        Destination = "Historical Museum",
                        MaxStudents = 30,
                        Status = "Scheduled",
                        RequestedAt = DateTime.Now
                    };
                    context.TripRequests.Add(tripReq);
                    context.SaveChanges();

                    var tripSchedule = new TripSchedule
                    {
                        TripRequestId = tripReq.Id,
                        TeacherId = tripReq.TeacherId,
                        ScheduledDate = scheduledDate,
                        Status = "Confirmed",
                        DriverId = sibusiso.Id,
                        Notes = "Seeded trip for driver Sibusiso"
                    };
                    context.TripSchedules.Add(tripSchedule);
                    context.SaveChanges();

                    var vehicle = context.Vehicles.FirstOrDefault(v => v.VehicleNumber == "MH-TRIP-001");
                    if (vehicle == null)
                    {
                        vehicle = new Vehicle
                        {
                            VehicleNumber = "MH-TRIP-001",
                            Model = "Mercedes Sprinter",
                            Type = "Bus",
                            Capacity = 22,
                            IsActive = true,
                            DateAdded = DateTime.Now
                        };
                        context.Vehicles.Add(vehicle);
                        context.SaveChanges();
                    }

                    tripSchedule.VehicleId = vehicle.Id;
                    context.Entry(tripSchedule).State = EntityState.Modified;
                    context.SaveChanges();

                    var tva = new TripVehicleAssignment
                    {
                        TripScheduleId = tripSchedule.Id,
                        VehicleId = vehicle.Id,
                        DriverId = sibusiso.Id,
                        AllocatedSeats = 20,
                        CreatedAt = DateTime.Now
                    };
                    context.TripVehicleAssignments.Add(tva);
                    context.SaveChanges();

                    var students = context.Students.Take(5).ToList();
                    foreach (var st in students)
                    {
                        var ts = new TripStudent
                        {
                            TripScheduleId = tripSchedule.Id,
                            StudentId = st.StudentId,
                            IsPresentBefore = null,
                            IsPresentAfter = null
                        };
                        context.TripStudents.Add(ts);
                    }
                    context.SaveChanges();

                    var tripStudents = context.TripStudents.Where(x => x.TripScheduleId == tripSchedule.Id).OrderBy(x => x.Id).ToList();
                    for (int i = 0; i < Math.Min(3, tripStudents.Count); i++)
                    {
                        tripStudents[i].IsPresentBefore = true;
                        tripStudents[i].MarkedBeforeAt = DateTime.Now;
                        tripStudents[i].MarkedBeforeBy = sibusiso.UserId?.ToString();
                        context.Entry(tripStudents[i]).State = EntityState.Modified;
                    }
                    context.SaveChanges();
                }
            }
        }
        catch { }

        // ──────────────────────────────────────────────────────────────
        // Seed InventoryManager and TransportManager (from HEAD)
        // ──────────────────────────────────────────────────────────────
        if (!context.Users.Any(u => u.Role == "InventoryManager"))
        {
            context.Users.Add(new AppUser
            {
                Name = "Andre Van Kok",
                Email = "inventory@michaelhouse.co.za",
                PasswordHash = HashPassword("Stock@123"),
                Role = "InventoryManager"
            });
            context.SaveChanges();
        }

        if (!context.Users.Any(u => u.Role == "Transport Manager"))
        {
            context.Users.Add(new AppUser
            {
                Name = "Greg Johnson",
                Email = "transport@michaelhouse.co.za",
                PasswordHash = HashPassword("Transport@123"),
                Role = "TransportManager"
            });
            context.SaveChanges();
        }

        // ──────────────────────────────────────────────────────────────
        // Step 8: Seed CafeteriaManager
        // Uses the existing MBMS AppUser/Role authentication pattern.
        // ──────────────────────────────────────────────────────────────
        if (!context.Users.Any(u => u.Role == "CafeteriaManager"))
        {
            context.Users.Add(new AppUser
            {
                Name = "Cafeteria Manager",
                Email = "cafeteria@michaelhouse.co.za",
                PasswordHash = HashPassword("Cafeteria@123"),
                Role = "CafeteriaManager"
            });
            context.SaveChanges();

        }

        if (!context.Users.Any(u => u.Role == "Chef"))
        {
            context.Users.Add(new AppUser
            {
                Name = "Head Chef",
                Email = "chef@michaelhouse.co.za",
                PasswordHash = HashPassword("Chef@123"),
                Role = "Chef"
            });
            context.SaveChanges();
        }
        if (!context.Users.Any(u => u.Role == "Coach"))
        {
            context.Users.Add(new AppUser
            {
                Name = "Sports Coach",
                Email = "coach@michaelhouse.co.za",
                PasswordHash = HashPassword("Coach@123"),
                Role = "Coach"
            });
            context.SaveChanges();
        }

        if (!context.Users.Any(u => u.Role == "Dietitian"))
        {
            context.Users.Add(new AppUser
            {
                Name = "School Dietitian",
                Email = "dietitian@michaelhouse.co.za",
                PasswordHash = HashPassword("Dietitian@123"),
                Role = "Dietitian"
            });
            context.SaveChanges();
        }

        // ──────────────────────────────────────────────────────────────
        // UC13: Seed Student logins + profiles
        // ──────────────────────────────────────────────────────────────
        var studentSeeds = new[]
               {
            new { Id = 1, Email = "amina.khan@michaelhouse.co.za",      Allergies = "Nuts",   Medical = "",         Sport = "Rugby" },
            new { Id = 2, Email = "thabo.ntuli@michaelhouse.co.za",     Allergies = "",       Medical = "",         Sport = "Rugby, Cricket" },
            new { Id = 3, Email = "lindiwe.mthembu@michaelhouse.co.za", Allergies = "Dairy",  Medical = "",         Sport = "Water Polo" },
            new { Id = 4, Email = "sipho.zulu@michaelhouse.co.za",      Allergies = "",       Medical = "Diabetes", Sport = "Cricket, Squash" },
            new { Id = 5, Email = "nomsa.dlamini@michaelhouse.co.za",   Allergies = "Gluten", Medical = "",         Sport = "Rugby" }
        };

        foreach (var seed in studentSeeds)
        {
            var student = context.Students.FirstOrDefault(s => s.StudentId == seed.Id);
            if (student == null) continue;

            if (student.UserId == null)
            {
                var existing = context.Users.FirstOrDefault(u => u.Email == seed.Email);
                if (existing == null)
                {
                    existing = new AppUser
                    {
                        Name = student.FirstName + " " + student.LastName,
                        Email = seed.Email,
                        PasswordHash = HashPassword("Student@123"),
                        Role = "Student"
                    };
                    context.Users.Add(existing);
                    context.SaveChanges();
                }

                student.UserId = existing.UserId;
                context.SaveChanges();
            }

            var profile = context.StudentProfiles.FirstOrDefault(p => p.StudentId == student.StudentId);
            if (profile == null)
            {
                profile = new StudentProfile
                {
                    StudentId = student.StudentId,
                    Allergies = seed.Allergies,
                    MedicalConditions = seed.Medical,
                    Sports = seed.Sport,
                    IsActive = true
                };
                context.StudentProfiles.Add(profile);
            }
            else
            {
                profile.Allergies = seed.Allergies;
                profile.MedicalConditions = seed.Medical;
                profile.Sports = seed.Sport;
            }
            context.SaveChanges();

            // =============================================================
            // Students' squads (StudentSportStatus) follow the sports on
            // their profiles — adds missing sports, removes dropped ones,
            // keeps the Coach's availability flags. Safe to run every time.
            // =============================================================

            new Michaelhouse.Services.StudentSportService(context).SyncAll();

            {
                            
                              
                if (!context.EventMenuTemplates.Any())
                {
                    // Helper — finds MenuItems by name. If a name doesn't exist
                    // in the catalogue, that line is silently skipped so the
                    // seed doesn't crash on a partial dataset.
                    Func<string, MenuItem> findItem = name =>
                        context.MenuItems.FirstOrDefault(m => m.Name == name);

                    // ── 1. Sports Day Braai ────────────────────────────────
                    var braai = new EventMenuTemplate
                    {
                        Name = "Sports Day Braai",
                        Description = "Classic South African braai for match days and sports events.",
                        DefaultEventType = EventType.SportsDay,
                        DefaultHeadcount = 200,
                        MinGuests = 20,
                        MaxGuests = 500,
                        IsActive = true
                    };

                    var braaiItems = new[]
                    {
                new { Name = "Beef & Vegetable Stir-Fry", Qty = 1.20m, Section = "Main",  Order = 1 },
                new { Name = "Chicken & Brown Rice Bowl", Qty = 1.00m, Section = "Main",  Order = 2 },
                new { Name = "Chickpea & Rice Bowl",      Qty = 0.60m, Section = "Main",  Order = 3 },
                new { Name = "Peanut Butter Toast & Fruit", Qty = 0.40m, Section = "Dessert", Order = 4 }
            };

                    foreach (var row in braaiItems)
                    {
                        var item = findItem(row.Name);
                        if (item == null) continue;

                        braai.Items.Add(new EventMenuTemplateItem
                        {
                            MenuItemId = item.Id,
                            QuantityPerGuest = row.Qty,
                            Section = row.Section,
                            SortOrder = row.Order
                        });
                    }

                    context.EventMenuTemplates.Add(braai);
                    context.SaveChanges();

                    // ── 2. Chapel Sunday Roast ─────────────────────────────
                    var roast = new EventMenuTemplate
                    {
                        Name = "Chapel Sunday Roast",
                        Description = "Traditional Sunday roast after Chapel service.",
                        DefaultEventType = EventType.ChapelSundayRoast,
                        DefaultHeadcount = 120,
                        MinGuests = 20,
                        MaxGuests = 300,
                        IsActive = true
                    };

                    var roastItems = new[]
                    {
                new { Name = "Beef Lasagne & Garden Salad", Qty = 1.00m, Section = "Main",  Order = 1 },
                new { Name = "Chicken & Brown Rice Bowl",   Qty = 0.80m, Section = "Main",  Order = 2 },
                new { Name = "Vegetable Omelette & Toast",  Qty = 0.30m, Section = "Main",  Order = 3 },
                new { Name = "French Toast & Fresh Fruit",  Qty = 0.50m, Section = "Dessert", Order = 4 }
            };

                    foreach (var row in roastItems)
                    {
                        var item = findItem(row.Name);
                        if (item == null) continue;

                        roast.Items.Add(new EventMenuTemplateItem
                        {
                            MenuItemId = item.Id,
                            QuantityPerGuest = row.Qty,
                            Section = row.Section,
                            SortOrder = row.Order
                        });
                    }

                    context.EventMenuTemplates.Add(roast);
                    context.SaveChanges();

                    // ── 3. Formal Dinner ───────────────────────────────────
                    var formal = new EventMenuTemplate
                    {
                        Name = "Formal Dinner",
                        Description = "Plated three-course dinner for Founders' Day, Valedictory, and formal functions.",
                        DefaultEventType = EventType.FormalDinner,
                        DefaultHeadcount = 80,
                        MinGuests = 20,
                        MaxGuests = 300,
                        IsActive = true
                    };

                    var formalItems = new[]
                    {
                new { Name = "Vegetable Omelette & Toast", Qty = 0.20m, Section = "Starter", Order = 1 },
                new { Name = "Beef Lasagne & Garden Salad", Qty = 1.00m, Section = "Main", Order = 2 },
                new { Name = "Chicken & Brown Rice Bowl",  Qty = 0.70m, Section = "Main", Order = 3 },
                new { Name = "Chickpea & Rice Bowl",       Qty = 0.30m, Section = "Main", Order = 4 },
                new { Name = "French Toast & Fresh Fruit", Qty = 0.80m, Section = "Dessert", Order = 5 }
            };

                    foreach (var row in formalItems)
                    {
                        var item = findItem(row.Name);
                        if (item == null) continue;

                        formal.Items.Add(new EventMenuTemplateItem
                        {
                            MenuItemId = item.Id,
                            QuantityPerGuest = row.Qty,
                            Section = row.Section,
                            SortOrder = row.Order
                        });
                    }

                    context.EventMenuTemplates.Add(formal);
                    context.SaveChanges();

                    // ── 4. Parents' Tea ────────────────────────────────────
                    var tea = new EventMenuTemplate
                    {
                        Name = "Parents' Tea",
                        Description = "Light tea with sandwiches and scones — for visiting parents' weekends.",
                        DefaultEventType = EventType.ParentsWeekend,
                        DefaultHeadcount = 60,
                        MinGuests = 10,
                        MaxGuests = 150,
                        IsActive = true
                    };

                    var teaItems = new[]
                    {
                new { Name = "Peanut Butter Toast & Fruit",  Qty = 1.00m, Section = "Savoury", Order = 1 },
                new { Name = "Breakfast Wrap",               Qty = 0.80m, Section = "Savoury", Order = 2 },
                new { Name = "French Toast & Fresh Fruit",   Qty = 0.60m, Section = "Sweet",   Order = 3 },
                new { Name = "Weet-Bix, Banana & Milk",      Qty = 0.30m, Section = "Sweet",   Order = 4 }
            };

                    foreach (var row in teaItems)
                    {
                        var item = findItem(row.Name);
                        if (item == null) continue;

                        tea.Items.Add(new EventMenuTemplateItem
                        {
                            MenuItemId = item.Id,
                            QuantityPerGuest = row.Qty,
                            Section = row.Section,
                            SortOrder = row.Order
                        });
                    }

                    context.EventMenuTemplates.Add(tea);
                    context.SaveChanges();

                    // ── 5. Founders' Day Lunch ─────────────────────────────
                    var founders = new EventMenuTemplate
                    {
                        Name = "Founders' Day Lunch",
                        Description = "Buffet lunch for Founders' Day — the school's flagship event.",
                        DefaultEventType = EventType.FoundersDay,
                        DefaultHeadcount = 150,
                        MinGuests = 50,
                        MaxGuests = 400,
                        IsActive = true
                    };

                    var foundersItems = new[]
                    {
                new { Name = "Beef Lasagne & Garden Salad", Qty = 0.80m, Section = "Main",   Order = 1 },
                new { Name = "Chicken & Brown Rice Bowl",   Qty = 0.90m, Section = "Main",   Order = 2 },
                new { Name = "Chickpea & Rice Bowl",        Qty = 0.50m, Section = "Main",   Order = 3 },
                new { Name = "Vegetable Omelette & Toast",  Qty = 0.30m, Section = "Main",   Order = 4 },
                new { Name = "French Toast & Fresh Fruit",  Qty = 0.70m, Section = "Dessert", Order = 5 }
            };

                    foreach (var row in foundersItems)
                    {
                        var item = findItem(row.Name);
                        if (item == null) continue;

                        founders.Items.Add(new EventMenuTemplateItem
                        {
                            MenuItemId = item.Id,
                            QuantityPerGuest = row.Qty,
                            Section = row.Section,
                            SortOrder = row.Order
                        });
                    }

                    context.EventMenuTemplates.Add(founders);
                    context.SaveChanges();
                }
            }
            SeedMenuItems(context);
            SeedRecipesAndIngredients(context);


            // ──────────────────────────────────────────────────────────────
            // Seed Categories & Products (from HEAD)
            // ──────────────────────────────────────────────────────────────
            if (!context.Categories.Any())
            {
                var uniforms = new Category
                {
                    Name = "Uniforms",
                    Description = "Official school uniform items for all grades"
                };
                var stationery = new Category
                {
                    Name = "Books & Stationery",
                    Description = "Textbooks, exercise books and stationery"
                };
                context.Categories.AddOrUpdate(c => c.Name, uniforms, stationery);
                context.SaveChanges();
            }

            if (!context.Products.Any())
            {
                var uniformsCat = context.Categories.First(c => c.Name == "Uniforms");
                var stationeryCat = context.Categories.First(c => c.Name == "Books & Stationery");

                var products = new[]
                {
                    // Uniforms
                    new Product { Name = "School Shirt (White) - Small", CategoryId = uniformsCat.Id, Price = 120.00, QuantityInStock = 50, ReorderLevel = 10, Description = "Official white school shirt, small size.", IsActive = true },
                    new Product { Name = "School Shirt (White) - Medium", CategoryId = uniformsCat.Id, Price = 120.00, QuantityInStock = 80, ReorderLevel = 15, Description = "Official white school shirt, medium size.", IsActive = true },
                    new Product { Name = "School Shirt (White) - Large", CategoryId = uniformsCat.Id, Price = 120.00, QuantityInStock = 60, ReorderLevel = 15, Description = "Official white school shirt, large size.", IsActive = true },
                    new Product { Name = "School Trousers (Grey) - 28", CategoryId = uniformsCat.Id, Price = 180.00, QuantityInStock = 40, ReorderLevel = 8, Description = "Official grey school trousers, 28 inch waist.", IsActive = true },
                    new Product { Name = "School Trousers (Grey) - 30", CategoryId = uniformsCat.Id, Price = 180.00, QuantityInStock = 55, ReorderLevel = 10, Description = "Official grey school trousers, 30 inch waist.", IsActive = true },
                    new Product { Name = "School Skirt - Size 10", CategoryId = uniformsCat.Id, Price = 160.00, QuantityInStock = 35, ReorderLevel = 8, Description = "Official school skirt, size 10.", IsActive = true },
                    new Product { Name = "School Tie", CategoryId = uniformsCat.Id, Price = 65.00, QuantityInStock = 100, ReorderLevel = 20, Description = "Official school tie with house colours.", IsActive = true },
                    new Product { Name = "School Blazer - Small", CategoryId = uniformsCat.Id, Price = 450.00, QuantityInStock = 25, ReorderLevel = 5, Description = "Official school blazer, small size.", IsActive = true },
                    new Product { Name = "School Blazer - Medium", CategoryId = uniformsCat.Id, Price = 450.00, QuantityInStock = 30, ReorderLevel = 5, Description = "Official school blazer, medium size.", IsActive = true },
                    new Product { Name = "School Sports Kit", CategoryId = uniformsCat.Id, Price = 280.00, QuantityInStock = 3, ReorderLevel = 10, Description = "Official sports kit (shirt + shorts).", IsActive = true },

                    // Books & Stationery
                    new Product { Name = "Grade 8 Mathematics Textbook", CategoryId = stationeryCat.Id, Price = 220.00, QuantityInStock = 30, ReorderLevel = 5, Description = "Approved Mathematics textbook for Grade 8.", IsActive = true },
                    new Product { Name = "Grade 9 Mathematics Textbook", CategoryId = stationeryCat.Id, Price = 235.00, QuantityInStock = 25, ReorderLevel = 5, Description = "Approved Mathematics textbook for Grade 9.", IsActive = true },
                    new Product { Name = "Grade 10 Physical Science", CategoryId = stationeryCat.Id, Price = 260.00, QuantityInStock = 20, ReorderLevel = 5, Description = "Approved Physical Science textbook for Grade 10.", IsActive = true },
                    new Product { Name = "English Literature Anthology", CategoryId = stationeryCat.Id, Price = 195.00, QuantityInStock = 40, ReorderLevel = 8, Description = "Set works anthology for Grades 8-12.", IsActive = true },
                    new Product { Name = "A4 Exercise Book (Pack of 10)", CategoryId = stationeryCat.Id, Price = 85.00, QuantityInStock = 120, ReorderLevel = 25, Description = "Ruled A4 exercise books, 96 pages each.", IsActive = true },
                    new Product { Name = "Geometry Set", CategoryId = stationeryCat.Id, Price = 55.00, QuantityInStock = 80, ReorderLevel = 15, Description = "Complete geometry set with compass, ruler and protractor.", IsActive = true },
                    new Product { Name = "Scientific Calculator", CategoryId = stationeryCat.Id, Price = 320.00, QuantityInStock = 5, ReorderLevel = 8, Description = "Approved scientific calculator for Grades 10-12.", IsActive = true },
                    new Product { Name = "Coloured Pencils (24 pack)", CategoryId = stationeryCat.Id, Price = 45.00, QuantityInStock = 150, ReorderLevel = 30, Description = "24 assorted coloured pencils.", IsActive = true }
                };

                context.Products.AddOrUpdate(p => p.Name, products);
                context.SaveChanges();
            }

            // ──────────────────────────────────────────────────────────────
            // Seed Suppliers and SupplierProducts (from other branch)
            // ──────────────────────────────────────────────────────────────
            var uniformSupplier = new Supplier
            {
                Name = "KZN Uniform Manufacturers",
                ContactPerson = "Sarah Ndlovu",
                Email = "sales@kznuniforms.co.za",
                Phone = "0311234567",
                Address = "100 West St, Durban",
                IsActive = true
            };

            var stationerySupplier = new Supplier
            {
                Name = "EduBooks SA",
                ContactPerson = "David Smith",
                Email = "orders@edubooks.co.za",
                Phone = "0119876543",
                Address = "50 Nelson Mandela Dr, Johannesburg",
                IsActive = true
            };

            context.Suppliers.AddOrUpdate(s => s.Name, uniformSupplier, stationerySupplier);
            context.SaveChanges();

            // Re-fetch to get the newly generated Supplier IDs
            uniformSupplier = context.Suppliers.First(s => s.Name == "KZN Uniform Manufacturers");
            stationerySupplier = context.Suppliers.First(s => s.Name == "EduBooks SA");

            // Grab Uniform products
            var smallShirt = context.Products.First(p => p.Name == "School Shirt (White) - Small");
            var medShirt = context.Products.First(p => p.Name == "School Shirt (White) - Medium");
            var lrgShirt = context.Products.First(p => p.Name == "School Shirt (White) - Large");
            var greyTrousers28 = context.Products.First(p => p.Name == "School Trousers (Grey) - 28");
            var greyTrousers30 = context.Products.First(p => p.Name == "School Trousers (Grey) - 30");
            var skirt = context.Products.First(p => p.Name == "School Skirt - Size 10");
            var tie = context.Products.First(p => p.Name == "School Tie");
            var blazerS = context.Products.First(p => p.Name == "School Blazer - Small");
            var blazerM = context.Products.First(p => p.Name == "School Blazer - Medium");
            var sportsKit = context.Products.First(p => p.Name == "School Sports Kit");

            // Grab Books & Stationery products
            var math8 = context.Products.First(p => p.Name == "Grade 8 Mathematics Textbook");
            var math9 = context.Products.First(p => p.Name == "Grade 9 Mathematics Textbook");
            var physSci = context.Products.First(p => p.Name == "Grade 10 Physical Science");
            var engLit = context.Products.First(p => p.Name == "English Literature Anthology");
            var exerciseBooks = context.Products.First(p => p.Name == "A4 Exercise Book (Pack of 10)");
            var geometrySet = context.Products.First(p => p.Name == "Geometry Set");
            var calculator = context.Products.First(p => p.Name == "Scientific Calculator");
            var pencils = context.Products.First(p => p.Name == "Coloured Pencils (24 pack)");

            var supplierLinks = new[]
            {
                // Uniform Supplier Links
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = smallShirt.Id, UnitCost = 85.00m, SupplierSku = "SH-WHT-S", MinOrderQty = 10, LeadTimeDays = 7, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = medShirt.Id, UnitCost = 85.00m, SupplierSku = "SH-WHT-M", MinOrderQty = 10, LeadTimeDays = 7, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = lrgShirt.Id, UnitCost = 85.00m, SupplierSku = "SH-WHT-L", MinOrderQty = 10, LeadTimeDays = 7, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = greyTrousers28.Id, UnitCost = 130.00m, SupplierSku = "TR-GRY-28", MinOrderQty = 5, LeadTimeDays = 7, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = greyTrousers30.Id, UnitCost = 130.00m, SupplierSku = "TR-GRY-30", MinOrderQty = 5, LeadTimeDays = 7, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = skirt.Id, UnitCost = 110.00m, SupplierSku = "SK-BLU-10", MinOrderQty = 5, LeadTimeDays = 7, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = tie.Id, UnitCost = 40.00m, SupplierSku = "TIE-STD", MinOrderQty = 20, LeadTimeDays = 14, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = blazerS.Id, UnitCost = 310.00m, SupplierSku = "BLZ-GRN-S", MinOrderQty = 5, LeadTimeDays = 21, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = blazerM.Id, UnitCost = 310.00m, SupplierSku = "BLZ-GRN-M", MinOrderQty = 5, LeadTimeDays = 21, IsPreferred = true },
                new SupplierProduct { SupplierId = uniformSupplier.SupplierId, ProductId = sportsKit.Id, UnitCost = 190.00m, SupplierSku = "SPT-KIT-ALL", MinOrderQty = 10, LeadTimeDays = 14, IsPreferred = true },

                // Stationery Supplier Links
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = math8.Id, UnitCost = 180.00m, SupplierSku = "MATH-GR8-01", MinOrderQty = 20, LeadTimeDays = 14, IsPreferred = true },
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = math9.Id, UnitCost = 190.00m, SupplierSku = "MATH-GR9-01", MinOrderQty = 20, LeadTimeDays = 14, IsPreferred = true },
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = physSci.Id, UnitCost = 210.00m, SupplierSku = "SCI-GR10-01", MinOrderQty = 15, LeadTimeDays = 14, IsPreferred = true },
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = engLit.Id, UnitCost = 150.00m, SupplierSku = "ENG-LIT-ANTH", MinOrderQty = 20, LeadTimeDays = 10, IsPreferred = true },
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = exerciseBooks.Id, UnitCost = 50.00m, SupplierSku = "EX-BK-A4-10", MinOrderQty = 50, LeadTimeDays = 5, IsPreferred = true },
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = geometrySet.Id, UnitCost = 35.00m, SupplierSku = "GEO-SET-01", MinOrderQty = 30, LeadTimeDays = 7, IsPreferred = true },
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = calculator.Id, UnitCost = 250.00m, SupplierSku = "CALC-SCI-CAS", MinOrderQty = 10, LeadTimeDays = 10, IsPreferred = true },
                new SupplierProduct { SupplierId = stationerySupplier.SupplierId, ProductId = pencils.Id, UnitCost = 25.00m, SupplierSku = "PENC-24", MinOrderQty = 50, LeadTimeDays = 3, IsPreferred = true }
            };

            foreach (var link in supplierLinks)
            {
                if (!context.SupplierProducts.Any(sp => sp.SupplierId == link.SupplierId && sp.ProductId == link.ProductId))
                {
                    context.SupplierProducts.Add(link);
                }
            }
            context.SaveChanges();

            // ──────────────────────────────────────────────────────────────
            // SHIFT PATTERNS (from HEAD)
            // ──────────────────────────────────────────────────────────────
            if (!context.ShiftPatterns.Any())
            {
                context.ShiftPatterns.AddOrUpdate(sp => sp.Name,
                    new ShiftPattern { Name = "Morning", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0), Description = "06:00 – 14:00" },
                    new ShiftPattern { Name = "Afternoon", StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(22, 0, 0), Description = "14:00 – 22:00" },
                    new ShiftPattern { Name = "Night", StartTime = new TimeSpan(22, 0, 0), EndTime = new TimeSpan(6, 0, 0), Description = "22:00 – 06:00" }
                );
                context.SaveChanges();
            }

            // ──────────────────────────────────────────────────────────────
            // MAINTENANCE SEEDING (from HEAD) – Staff, Users, Inventory,
            // Assets, Job Cards, Safety Net
            // ──────────────────────────────────────────────────────────────
            if (!context.MaintenanceStaff.Any())
            {
                context.MaintenanceStaff.AddOrUpdate(s => s.StaffNumber,
                    new MaintenanceStaff
                    {
                        FullName = "Sipho Dlamini",
                        StaffNumber = "MH-MAINT-001",
                        Email = "s.dlamini@michaelhouse.org",
                        Phone = "0731001001",
                        SkillType = "Plumbing",
                        ShiftStart = new TimeSpan(6, 0, 0),
                        ShiftEnd = new TimeSpan(14, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2020, 1, 15)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Bongani Nkosi",
                        StaffNumber = "MH-MAINT-002",
                        Email = "b.nkosi@michaelhouse.org",
                        Phone = "0731001002",
                        SkillType = "Plumbing",
                        ShiftStart = new TimeSpan(14, 0, 0),
                        ShiftEnd = new TimeSpan(22, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2019, 3, 10)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Eric Mthembu",
                        StaffNumber = "MH-MAINT-003",
                        Email = "e.mthembu@michaelhouse.org",
                        Phone = "0731001003",
                        SkillType = "Electrical",
                        ShiftStart = new TimeSpan(6, 0, 0),
                        ShiftEnd = new TimeSpan(14, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2018, 6, 1)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Thabo Zulu",
                        StaffNumber = "MH-MAINT-004",
                        Email = "t.zulu@michaelhouse.org",
                        Phone = "0731001004",
                        SkillType = "Electrical",
                        ShiftStart = new TimeSpan(14, 0, 0),
                        ShiftEnd = new TimeSpan(22, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2021, 2, 20)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Lungelo Mbatha",
                        StaffNumber = "MH-MAINT-005",
                        Email = "l.mbatha@michaelhouse.org",
                        Phone = "0731001005",
                        SkillType = "HVAC",
                        ShiftStart = new TimeSpan(6, 0, 0),
                        ShiftEnd = new TimeSpan(14, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2022, 7, 5)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Sifiso Khumalo",
                        StaffNumber = "MH-MAINT-006",
                        Email = "s.khumalo@michaelhouse.org",
                        Phone = "0731001006",
                        SkillType = "Grounds",
                        ShiftStart = new TimeSpan(6, 0, 0),
                        ShiftEnd = new TimeSpan(14, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2017, 4, 12)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Nhlanhla Mokoena",
                        StaffNumber = "MH-MAINT-007",
                        Email = "n.mokoena@michaelhouse.org",
                        Phone = "0731001007",
                        SkillType = "Grounds",
                        ShiftStart = new TimeSpan(6, 0, 0),
                        ShiftEnd = new TimeSpan(14, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2016, 9, 8)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Mandla Cele",
                        StaffNumber = "MH-MAINT-008",
                        Email = "m.cele@michaelhouse.org",
                        Phone = "0731001008",
                        SkillType = "Pool",
                        ShiftStart = new TimeSpan(6, 0, 0),
                        ShiftEnd = new TimeSpan(14, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2023, 1, 16)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Sandile Ntanzi",
                        StaffNumber = "MH-MAINT-009",
                        Email = "s.ntanzi@michaelhouse.org",
                        Phone = "0731001009",
                        SkillType = "General",
                        ShiftStart = new TimeSpan(6, 0, 0),
                        ShiftEnd = new TimeSpan(14, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2020, 11, 3)
                    },
                    new MaintenanceStaff
                    {
                        FullName = "Phumzile Mhlongo",
                        StaffNumber = "MH-MAINT-010",
                        Email = "p.mhlongo@michaelhouse.org",
                        Phone = "0731001010",
                        SkillType = "General",
                        ShiftStart = new TimeSpan(14, 0, 0),
                        ShiftEnd = new TimeSpan(22, 0, 0),
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = new DateTime(2021, 8, 22)
                    }
                );
                context.SaveChanges();
            }

            // ── Maintenance Users ──
            if (!context.Users.Any(u => u.Email == "j.mokoena@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Mr. James Mokoena",
                        Email = "j.mokoena@michaelhouse.org",
                        PasswordHash = HashPassword("Manager@123"),
                        Role = "MaintenanceManager"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "s.dlamini@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Sipho Dlamini",
                        Email = "s.dlamini@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "b.nkosi@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Bongani Nkosi",
                        Email = "b.nkosi@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "e.mthembu@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Eric Mthembu",
                        Email = "e.mthembu@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "t.zulu@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Thabo Zulu",
                        Email = "t.zulu@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "l.mbatha@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Lungelo Mbatha",
                        Email = "l.mbatha@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "s.khumalo@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Sifiso Khumalo",
                        Email = "s.khumalo@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "n.mokoena@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Nhlanhla Mokoena",
                        Email = "n.mokoena@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "m.cele@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Mandla Cele",
                        Email = "m.cele@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "s.ntanzi@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Sandile Ntanzi",
                        Email = "s.ntanzi@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "p.mhlongo@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Phumzile Mhlongo",
                        Email = "p.mhlongo@michaelhouse.org",
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    }
                );
                context.SaveChanges();
            }

            // Fault Reporters
            if (!context.Users.Any(u => u.Email == "d.hutchinson@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Mr. David Hutchinson",
                        Email = "d.hutchinson@michaelhouse.org",
                        PasswordHash = HashPassword("Report@123"),
                        Role = "FaultReporter"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "p.vandermerwe@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Mr. Peter van der Merwe",
                        Email = "p.vandermerwe@michaelhouse.org",
                        PasswordHash = HashPassword("Report@123"),
                        Role = "FaultReporter"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "s.ndlovu@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Mr. Simon Ndlovu",
                        Email = "s.ndlovu@michaelhouse.org",
                        PasswordHash = HashPassword("Report@123"),
                        Role = "FaultReporter"
                    }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "n.dube@michaelhouse.org"))
            {
                context.Users.AddOrUpdate(u => u.Email,
                    new AppUser
                    {
                        Name = "Ms. Nompumelelo Dube",
                        Email = "n.dube@michaelhouse.org",
                        PasswordHash = HashPassword("Report@123"),
                        Role = "FaultReporter"
                    }
                );
                context.SaveChanges();
            }

            // ── Maintenance Inventory ──
            if (!context.MaintenanceInventory.Any())
            {
                context.MaintenanceInventory.AddOrUpdate(i => i.ItemName,
                    // Plumbing
                    new MaintenanceInventory
                    {
                        ItemName = "PVC Pipe 20mm",
                        Category = "Plumbing",
                        StockLevel = 50,
                        MinimumStock = 10,
                        Unit = "metres",
                        UnitCost = 25.00m,
                        Supplier = "Build It Pietermaritzburg"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Pipe Fittings Set",
                        Category = "Plumbing",
                        StockLevel = 30,
                        MinimumStock = 10,
                        Unit = "pieces",
                        UnitCost = 45.00m,
                        Supplier = "Build It Pietermaritzburg"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Geyser Element 3kW",
                        Category = "Plumbing",
                        StockLevel = 8,
                        MinimumStock = 3,
                        Unit = "pieces",
                        UnitCost = 320.00m,
                        Supplier = "Builders Warehouse PMB"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Tap Washer Set",
                        Category = "Plumbing",
                        StockLevel = 60,
                        MinimumStock = 15,
                        Unit = "pieces",
                        UnitCost = 12.00m,
                        Supplier = "Build It Pietermaritzburg"
                    },
                    // Electrical
                    new MaintenanceInventory
                    {
                        ItemName = "LED Bulb 18W",
                        Category = "Electrical",
                        StockLevel = 100,
                        MinimumStock = 20,
                        Unit = "pieces",
                        UnitCost = 35.00m,
                        Supplier = "Builders Warehouse PMB"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Circuit Breaker 20A",
                        Category = "Electrical",
                        StockLevel = 15,
                        MinimumStock = 5,
                        Unit = "pieces",
                        UnitCost = 85.00m,
                        Supplier = "Builders Warehouse PMB"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Electrical Cable 2.5mm",
                        Category = "Electrical",
                        StockLevel = 200,
                        MinimumStock = 50,
                        Unit = "metres",
                        UnitCost = 18.00m,
                        Supplier = "Builders Warehouse PMB"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Light Switch",
                        Category = "Electrical",
                        StockLevel = 25,
                        MinimumStock = 8,
                        Unit = "pieces",
                        UnitCost = 28.00m,
                        Supplier = "Builders Warehouse PMB"
                    },
                    // HVAC
                    new MaintenanceInventory
                    {
                        ItemName = "HVAC Air Filter",
                        Category = "HVAC",
                        StockLevel = 20,
                        MinimumStock = 5,
                        Unit = "pieces",
                        UnitCost = 150.00m,
                        Supplier = "Air Tech KZN"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Refrigerant Gas R410A",
                        Category = "HVAC",
                        StockLevel = 10,
                        MinimumStock = 3,
                        Unit = "kg",
                        UnitCost = 280.00m,
                        Supplier = "Air Tech KZN"
                    },
                    // Pool
                    new MaintenanceInventory
                    {
                        ItemName = "Pool Chlorine 25kg",
                        Category = "Pool",
                        StockLevel = 10,
                        MinimumStock = 3,
                        Unit = "bags",
                        UnitCost = 450.00m,
                        Supplier = "Pool Zone PMB"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Pool pH Increaser 5kg",
                        Category = "Pool",
                        StockLevel = 8,
                        MinimumStock = 2,
                        Unit = "bags",
                        UnitCost = 220.00m,
                        Supplier = "Pool Zone PMB"
                    },
                    // Safety
                    new MaintenanceInventory
                    {
                        ItemName = "Fire Extinguisher Powder",
                        Category = "Safety",
                        StockLevel = 6,
                        MinimumStock = 2,
                        Unit = "kg",
                        UnitCost = 180.00m,
                        Supplier = "Fire Safety KZN"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Smoke Detector Battery",
                        Category = "Safety",
                        StockLevel = 40,
                        MinimumStock = 10,
                        Unit = "pieces",
                        UnitCost = 25.00m,
                        Supplier = "Fire Safety KZN"
                    },
                    // Grounds
                    new MaintenanceInventory
                    {
                        ItemName = "Grass Fertilizer 50kg",
                        Category = "Grounds",
                        StockLevel = 20,
                        MinimumStock = 5,
                        Unit = "bags",
                        UnitCost = 280.00m,
                        Supplier = "Garden World PMB"
                    },
                    // General
                    new MaintenanceInventory
                    {
                        ItemName = "General Paint 5L White",
                        Category = "General",
                        StockLevel = 15,
                        MinimumStock = 5,
                        Unit = "tins",
                        UnitCost = 185.00m,
                        Supplier = "Plascon Paint PMB"
                    },
                    new MaintenanceInventory
                    {
                        ItemName = "Wood Screws Assorted Box",
                        Category = "General",
                        StockLevel = 20,
                        MinimumStock = 5,
                        Unit = "boxes",
                        UnitCost = 45.00m,
                        Supplier = "Build It Pietermaritzburg"
                    }
                );
                context.SaveChanges();
            }

            // ── Advanced Seeds – Shifts, Assets, Job Cards ──
            if (!context.StaffShifts.Any())
            {
                var shiftPatterns = context.ShiftPatterns.ToList();
                var staffList = context.MaintenanceStaff.ToList();
                var today = DateTime.Today;
                for (int d = 0; d < 7; d++)
                {
                    var date = today.AddDays(d);
                    foreach (var staff in staffList)
                    {
                        var patternIndex = (staff.Id + d) % shiftPatterns.Count;
                        var pattern = shiftPatterns[patternIndex];
                        context.StaffShifts.Add(new StaffShift
                        {
                            StaffId = staff.Id,
                            ShiftPatternId = pattern.Id,
                            Date = date,
                            Notes = "Scheduled"
                        });
                    }
                }
                context.SaveChanges();
            }

            if (!context.Assets.Any())
            {
                var assetData = new List<dynamic>();
                // Founders House
                assetData.AddRange(new[] {
                    new { Name = "Lights - Founders (5)", Cat = "Electrical", Bld = "Founders House", Room = "All rooms", Condition = "Good" },
                    new { Name = "Power Sockets - Founders (3)", Cat = "Electrical", Bld = "Founders House", Room = "All rooms", Condition = "Good" },
                    new { Name = "Geyser - Founders", Cat = "Plumbing", Bld = "Founders House", Room = "Kitchen", Condition = "Fair" },
                    new { Name = "Toilets - Founders (2)", Cat = "Plumbing", Bld = "Founders House", Room = "Bathrooms", Condition = "Good" },
                    new { Name = "Kitchen Oven - Founders", Cat = "General", Bld = "Founders House", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Fridge - Founders", Cat = "General", Bld = "Founders House", Room = "Kitchen", Condition = "Fair" }
                });
                // East House
                assetData.AddRange(new[] {
                    new { Name = "Lights - East (5)", Cat = "Electrical", Bld = "East House", Room = "All rooms", Condition = "Good" },
                    new { Name = "Power Sockets - East (3)", Cat = "Electrical", Bld = "East House", Room = "All rooms", Condition = "Good" },
                    new { Name = "Geyser - East", Cat = "Plumbing", Bld = "East House", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Toilets - East (2)", Cat = "Plumbing", Bld = "East House", Room = "Bathrooms", Condition = "Fair" },
                    new { Name = "Kitchen Oven - East", Cat = "General", Bld = "East House", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Fridge - East", Cat = "General", Bld = "East House", Room = "Kitchen", Condition = "Good" }
                });
                // West House
                assetData.AddRange(new[] {
                    new { Name = "Lights - West (5)", Cat = "Electrical", Bld = "West House", Room = "All rooms", Condition = "Good" },
                    new { Name = "Power Sockets - West (3)", Cat = "Electrical", Bld = "West House", Room = "All rooms", Condition = "Good" },
                    new { Name = "Geyser - West", Cat = "Plumbing", Bld = "West House", Room = "Kitchen", Condition = "Fair" },
                    new { Name = "Toilets - West (2)", Cat = "Plumbing", Bld = "West House", Room = "Bathrooms", Condition = "Good" },
                    new { Name = "Kitchen Oven - West", Cat = "General", Bld = "West House", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Fridge - West", Cat = "General", Bld = "West House", Room = "Kitchen", Condition = "Good" }
                });
                // Tatham House
                assetData.AddRange(new[] {
                    new { Name = "Lights - Tatham (5)", Cat = "Electrical", Bld = "Tatham House", Room = "All rooms", Condition = "Good" },
                    new { Name = "Power Sockets - Tatham (3)", Cat = "Electrical", Bld = "Tatham House", Room = "All rooms", Condition = "Fair" },
                    new { Name = "Geyser - Tatham", Cat = "Plumbing", Bld = "Tatham House", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Toilets - Tatham (2)", Cat = "Plumbing", Bld = "Tatham House", Room = "Bathrooms", Condition = "Good" },
                    new { Name = "Kitchen Oven - Tatham", Cat = "General", Bld = "Tatham House", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Fridge - Tatham", Cat = "General", Bld = "Tatham House", Room = "Kitchen", Condition = "Good" }
                });
                // Main Kitchen
                assetData.AddRange(new[] {
                    new { Name = "Industrial Oven (Main Kitchen)", Cat = "General", Bld = "Main Kitchen", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Industrial Fridge (Main Kitchen)", Cat = "General", Bld = "Main Kitchen", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Freezer (Main Kitchen)", Cat = "General", Bld = "Main Kitchen", Room = "Kitchen", Condition = "Fair" },
                    new { Name = "Dishwasher (Main Kitchen)", Cat = "General", Bld = "Main Kitchen", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Ventilation System (Main Kitchen)", Cat = "HVAC", Bld = "Main Kitchen", Room = "Kitchen", Condition = "Good" },
                    new { Name = "Fire Extinguisher (Main Kitchen)", Cat = "Safety", Bld = "Main Kitchen", Room = "Kitchen", Condition = "Good" }
                });
                // Gym/Sports
                assetData.AddRange(new[] {
                    new { Name = "Scoreboard", Cat = "Electrical", Bld = "Gym/Sports", Room = "Main Hall", Condition = "Good" },
                    new { Name = "Showers (Gym) (2)", Cat = "Plumbing", Bld = "Gym/Sports", Room = "Change Rooms", Condition = "Good" },
                    new { Name = "Boiler (Gym)", Cat = "Plumbing", Bld = "Gym/Sports", Room = "Change Rooms", Condition = "Fair" },
                    new { Name = "Lights (Gym) (4)", Cat = "Electrical", Bld = "Gym/Sports", Room = "Main Hall", Condition = "Good" },
                    new { Name = "Sound System (Gym)", Cat = "General", Bld = "Gym/Sports", Room = "Main Hall", Condition = "Good" }
                });
                // Classrooms
                for (int i = 1; i <= 5; i++)
                {
                    assetData.AddRange(new[] {
                        new { Name = $"Projector - Room {i}", Cat = "Electrical", Bld = "Classrooms", Room = $"Room {i}", Condition = "Good" },
                        new { Name = $"Air Conditioner - Room {i}", Cat = "HVAC", Bld = "Classrooms", Room = $"Room {i}", Condition = "Good" },
                        new { Name = $"Lights - Room {i} (4)", Cat = "Electrical", Bld = "Classrooms", Room = $"Room {i}", Condition = "Good" },
                        new { Name = $"Smart Board - Room {i}", Cat = "General", Bld = "Classrooms", Room = $"Room {i}", Condition = "Good" }
                    });
                }
                // Campus-wide
                assetData.AddRange(new[] {
                    new { Name = "Pool Pump", Cat = "Pool", Bld = "Campus", Room = "Pool Area", Condition = "Good" },
                    new { Name = "Pool Filter", Cat = "Pool", Bld = "Campus", Room = "Pool Area", Condition = "Fair" },
                    new { Name = "Pool Heater", Cat = "Pool", Bld = "Campus", Room = "Pool Area", Condition = "Good" },
                    new { Name = "Chlorinator", Cat = "Pool", Bld = "Campus", Room = "Pool Area", Condition = "Good" },
                    new { Name = "Lawnmower (x2)", Cat = "Grounds", Bld = "Campus", Room = "Grounds Shed", Condition = "Good" },
                    new { Name = "Tractor", Cat = "Grounds", Bld = "Campus", Room = "Grounds Shed", Condition = "Good" },
                    new { Name = "Water Sprinkler System", Cat = "Grounds", Bld = "Campus", Room = "Grounds", Condition = "Fair" },
                    new { Name = "CCTV Camera (x12)", Cat = "Safety", Bld = "Campus", Room = "Various", Condition = "Good" },
                    new { Name = "Security Gates (x4)", Cat = "General", Bld = "Campus", Room = "Entrances", Condition = "Good" },
                    new { Name = "Electric Fence", Cat = "Safety", Bld = "Campus", Room = "Perimeter", Condition = "Good" },
                    new { Name = "Main Water Pump", Cat = "Plumbing", Bld = "Campus", Room = "Utility Room", Condition = "Good" },
                    new { Name = "Backup Generator", Cat = "General", Bld = "Campus", Room = "Utility Room", Condition = "Good" },
                    new { Name = "Solar System", Cat = "Electrical", Bld = "Campus", Room = "Roof", Condition = "Good" }
                });

                int index = 1;
                foreach (var ad in assetData)
                {
                    var asset = new Asset
                    {
                        AssetName = ad.Name,
                        Category = ad.Cat,
                        LocationBuilding = ad.Bld,
                        LocationRoom = ad.Room ?? "",
                        ConditionRating = ad.Condition,
                        Status = "Active",
                        DateRegistered = DateTime.Now.AddDays(-new Random().Next(1, 30)),
                        RegisteredById = 1,
                        QrCode = string.Format("MH-ASSET-{0:D4}", index),
                        HealthScore = ad.Condition == "Good" ? 100 : (ad.Condition == "Fair" ? 65 : 30),
                        FaultCount = new Random().Next(0, 3),
                        WarrantyExpiry = DateTime.Now.AddYears(2 + new Random().Next(-1, 3)),
                        PurchaseDate = DateTime.Now.AddYears(-3 + new Random().Next(0, 4)),
                        PurchaseCost = new Random().Next(500, 5000),
                        ModelSerial = "MOD-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                        Supplier = "Build It Pietermaritzburg"
                    };
                    context.Assets.Add(asset);
                    index++;
                    if (index % 5 == 0) context.SaveChanges();
                }
                context.SaveChanges();
            }

            if (!context.JobCards.Any())
            {
                var staff = context.MaintenanceStaff.ToList();
                var assets = context.Assets.ToList();
                var adminUser = context.Users.FirstOrDefault(u => u.Role == "Admin")?.UserId ?? 1;

                for (int i = 0; i < 3; i++)
                {
                    var asset = assets.Skip(i % assets.Count).First();
                    var worker = staff.Skip(i % staff.Count).First();
                    var job = new JobCard
                    {
                        JobReference = string.Format("MH-JOB-{0:D4}", i + 100),
                        Title = $"Open Job {i + 1}: {asset.AssetName} fault",
                        Description = $"Fault reported on {asset.AssetName} in {asset.LocationBuilding}",
                        AssetId = asset.Id,
                        AssignedToId = worker.Id,
                        Priority = i == 0 ? "Emergency" : (i == 1 ? "High" : "Medium"),
                        Status = i == 0 ? "Assigned" : "Pending",
                        DateCreated = DateTime.Now.AddDays(-i),
                        DateAssigned = i == 0 ? DateTime.Now.AddDays(-i + 1) : (DateTime?)null,
                        DueDate = DateTime.Now.AddDays(1 + i),
                        ReportedById = adminUser,
                        PhotoBefore = "",
                        JobType = "Reactive"
                    };
                    context.JobCards.Add(job);
                }

                for (int i = 0; i < 2; i++)
                {
                    var asset = assets.Skip((i + 3) % assets.Count).First();
                    var worker = staff.Skip((i + 3) % staff.Count).First();
                    var job = new JobCard
                    {
                        JobReference = string.Format("MH-JOB-{0:D4}", i + 200),
                        Title = $"Completed Job {i + 1}: {asset.AssetName} fixed",
                        Description = $"Fixed {asset.AssetName} in {asset.LocationBuilding}",
                        AssetId = asset.Id,
                        AssignedToId = worker.Id,
                        Priority = "Medium",
                        Status = "Completed",
                        DateCreated = DateTime.Now.AddDays(-5 - i),
                        DateAssigned = DateTime.Now.AddDays(-5 - i + 1),
                        DateCompleted = DateTime.Now.AddDays(-1 - i),
                        DueDate = DateTime.Now.AddDays(-2 - i),
                        ResponseTimeMinutes = 120,
                        ReportedById = adminUser,
                        PhotoBefore = "",
                        PhotoAfter = "",
                        CompletionNotes = "Fixed successfully",
                        FinalCondition = "Fixed"
                    };
                    context.JobCards.Add(job);
                }

                var overdueAsset = assets.Skip(4).First();
                var overdueWorker = staff.Skip(4).First();
                var overdueJob = new JobCard
                {
                    JobReference = "MH-JOB-0900",
                    Title = $"OVERDUE: {overdueAsset.AssetName} urgent repair",
                    Description = $"This job is overdue by 2 days",
                    AssetId = overdueAsset.Id,
                    AssignedToId = overdueWorker.Id,
                    Priority = "High",
                    Status = "Assigned",
                    DateCreated = DateTime.Now.AddDays(-5),
                    DateAssigned = DateTime.Now.AddDays(-5),
                    DueDate = DateTime.Now.AddDays(-2),
                    ReportedById = adminUser,
                    JobType = "Reactive"
                };
                context.JobCards.Add(overdueJob);
                context.SaveChanges();
            }

            // ── Safety net – ensure all staff have login accounts ──
            var allStaff = context.MaintenanceStaff.ToList();
            foreach (var staff in allStaff)
            {
                if (staff.UserId.HasValue && context.Users.Any(u => u.UserId == staff.UserId.Value))
                    continue;

                var existingUser = context.Users.FirstOrDefault(u => u.Email == staff.Email);
                if (existingUser != null)
                {
                    staff.UserId = existingUser.UserId;
                    context.Entry(staff).State = EntityState.Modified;
                    context.SaveChanges();
                    continue;
                }

                var email = staff.Email ?? staff.FullName.Replace(" ", ".").ToLower() + "@michaelhouse.org";
                var newUser = new AppUser
                {
                    Name = staff.FullName,
                    Email = email,
                    PasswordHash = HashPassword("Worker@123"),
                    Role = "MaintenanceWorker"
                };
                context.Users.Add(newUser);
                context.SaveChanges();

                staff.UserId = newUser.UserId;
                context.Entry(staff).State = EntityState.Modified;
                context.SaveChanges();
            }


            // ──────────────────────────────────────────────────────────────
            // Seed Drivers (from HEAD) plus SQL script and Boarding House
            // ──────────────────────────────────────────────────────────────
            if (!context.Users.Any(u => u.Role == "Driver"))
            {
                var driversData = new[]
                {
                    new { Name = "Sibusiso Dlamini", Email = "sibusiso", ID = "9001015009087" },
                    new { Name = "Thabo Mkhize", Email = "thabo", ID = "8805056009088" },
                    new { Name = "Andile Zulu", Email = "andile", ID = "9202027009089" },
                    new { Name = "Nkosi Khumalo", Email = "nkosi", ID = "8703038009090" }
                };

                foreach (var d in driversData)
                {
                    string schoolEmail = d.Email + "@michealhouse.com";
                    string password = "Driver@123";

                    var user = new AppUser
                    {
                        Name = d.Name,
                        Email = schoolEmail,
                        PasswordHash = HashPassword(password),
                        Role = "Driver"
                    };
                    context.Users.Add(user);
                    context.SaveChanges();

                    var driver = new Driver
                    {
                        FullName = d.Name,
                        IDNumber = d.ID,
                        PhoneNumber = "0710000000",
                        Email = schoolEmail,
                        LicenceNumber = "LIC" + new Random().Next(1000, 9999),
                        LicenceExpiryDate = DateTime.Now.AddYears(5),
                        HasPDP = true,
                        IsActive = true,
                        DateCreated = DateTime.Now,
                        UserId = user.UserId
                    };
                    context.Drivers.Add(driver);
                }

                context.SaveChanges();
            }

            // ── Execute SQL seed script (from other branch) ──
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "Michaelhouse.Scripts.SQLQuery.sql"; // adjust to your actual namespace + folder
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        throw new Exception($"Resource '{resourceName}' not found.");
                    using (var reader = new StreamReader(stream))
                    {
                        string sqlScript = reader.ReadToEnd();
                        ExecuteSqlScript(context, sqlScript);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or rethrow – since script is idempotent, we can swallow or log.
                System.Diagnostics.Debug.WriteLine("SQL Seed script error: " + ex.Message);
            }

            // ── Seed Boarding House test data (from other branch) ──
            SeedBoardingHouseTestData(context);
        }

        // More meals, their ingredients, cafeteria suppliers and opening
        // stock. Outside the student loop above so it runs once. Only
        // adds what's missing; never overwrites edits or real stock.
        // See Models/Cafeteria/CafeteriaInventorySeed.cs
        Michaelhouse.Models.Cafeteria.CafeteriaInventorySeed.Run(context);
    }

    // ──────────────────────────────────────────────────────────────
    // Helper methods (from other branch)
    // ──────────────────────────────────────────────────────────────

    private void ExecuteSqlScript(DbContext context, string sqlScript)
    {
        var batches = sqlScript.Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(b => b.Trim())
                                .Where(b => !string.IsNullOrEmpty(b))
                                .ToList();

        using (var transaction = context.Database.BeginTransaction())
        {
            try
            {
                foreach (var batch in batches)
                {
                    context.Database.ExecuteSqlCommand(batch);
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception("Error executing SQL seed script: " + ex.Message, ex);
            }
        }
    }

    private void SeedBoardingHouseTestData(DBContextClass context)
    {
        var houses = new[]
        {
                new
                {
                    ResidenceName = "Founders House",
                    HouseMasterName = "Mr James Harrington",
                    Email = "founders.hm@michaelhouse.co.za",
                    Phone = "0333301001",
                    Gender = "Male",
                    GradeCategory = "Junior",
                    NearMedical = true,
                    NearOffice = true,
                    RoomPrefix = "F",
                    Floors = new[] { 1, 1, 2, 2 },
                    Capacities = new[] { 4, 4, 3, 3 }
                },
                new
                {
                    ResidenceName = "Baines House",
                    HouseMasterName = "Ms Nomvula Dlamini",
                    Email = "baines.hm@michaelhouse.co.za",
                    Phone = "0333301002",
                    Gender = "Male",
                    GradeCategory = "Middle",
                    NearMedical = false,
                    NearOffice = true,
                    RoomPrefix = "B",
                    Floors = new[] { 1, 1, 2, 2 },
                    Capacities = new[] { 3, 3, 4, 4 }
                },
                new
                {
                    ResidenceName = "Tatham House",
                    HouseMasterName = "Mr Andrew Naidoo",
                    Email = "tatham.hm@michaelhouse.co.za",
                    Phone = "0333301003",
                    Gender = "Male",
                    GradeCategory = "Senior",
                    NearMedical = false,
                    NearOffice = false,
                    RoomPrefix = "T",
                    Floors = new[] { 1, 2, 2, 3 },
                    Capacities = new[] { 2, 3, 3, 2 }
                },
                new
                {
                    ResidenceName = "Churchill House",
                    HouseMasterName = "Ms Sarah Mokoena",
                    Email = "churchill.hm@michaelhouse.co.za",
                    Phone = "0333301004",
                    Gender = "Male",
                    GradeCategory = "Senior",
                    NearMedical = true,
                    NearOffice = false,
                    RoomPrefix = "C",
                    Floors = new[] { 1, 1, 2, 2 },
                    Capacities = new[] { 2, 2, 3, 3 }
                }
            };


        foreach (var house in houses)
        {
            var user = context.Users.FirstOrDefault(u => u.Email == house.Email);
            if (user == null)
            {
                user = new AppUser
                {
                    Name = house.HouseMasterName,
                    Email = house.Email,
                    PasswordHash = HashPassword("HouseMaster@123"),
                    Role = "HouseMaster"
                };
                context.Users.Add(user);
                context.SaveChanges();
            }

            var houseMaster = context.HouseMasters.FirstOrDefault(h => h.ContactEmail == house.Email);
            if (houseMaster == null)
            {
                houseMaster = new HouseMaster
                {
                    FullName = house.HouseMasterName,
                    ContactEmail = house.Email,
                    ContactPhone = house.Phone
                };
                context.HouseMasters.Add(houseMaster);
                context.SaveChanges();
            }

            var residence = context.Residences.FirstOrDefault(r => r.Name == house.ResidenceName);
            if (residence == null)
            {
                residence = new Residence
                {
                    Name = house.ResidenceName,
                    Gender = house.Gender,
                    GradeCategory = house.GradeCategory,
                    HouseMasterId = houseMaster.HouseMasterId,
                    NearMedicalFacility = house.NearMedical,
                    NearHouseMasterOffice = house.NearOffice,
                    OccupiedBeds = 0,
                    IsArchived = false,
                    ArchivedDate = DateTime.Now
                };
                context.Residences.Add(residence);
                context.SaveChanges();
            }
            else
            {
                residence.Gender = house.Gender;
                residence.GradeCategory = house.GradeCategory;
                residence.HouseMasterId = houseMaster.HouseMasterId;
                residence.NearMedicalFacility = house.NearMedical;
                residence.NearHouseMasterOffice = house.NearOffice;
            }

            houseMaster.ResidenceId = residence.ResidenceId;
            context.SaveChanges();

            for (var i = 0; i < house.Capacities.Length; i++)
            {
                var roomNumber = $"{house.RoomPrefix}{(i + 1):00}";
                var room = context.Rooms.FirstOrDefault(r => r.ResidenceId == residence.ResidenceId && r.RoomNumber == roomNumber);
                if (room == null)
                {
                    room = new Room
                    {
                        ResidenceId = residence.ResidenceId,
                        RoomNumber = roomNumber,
                        Capacity = house.Capacities[i],
                        OccupiedBeds = 0,
                        IsFull = false,
                        Floor = house.Floors[i],
                        IsGroundFloor = house.Floors[i] == 1,
                        IsWheelchairAccessible = house.Floors[i] == 1,
                        NearBathroom = i % 2 == 0,
                        IsQuietStudyRoom = i == 0 || i == 2,
                        NeedsMaintenance = false
                    };
                    context.Rooms.Add(room);
                    context.SaveChanges();
                }
                else
                {
                    room.Capacity = house.Capacities[i];
                    room.Floor = house.Floors[i];
                    room.IsGroundFloor = house.Floors[i] == 1;
                    room.IsWheelchairAccessible = house.Floors[i] == 1;
                    room.NearBathroom = i % 2 == 0;
                    room.IsQuietStudyRoom = i == 0 || i == 2;
                    room.NeedsMaintenance = false;
                }

                for (var bedNumber = 1; bedNumber <= house.Capacities[i]; bedNumber++)
                {
                    var bedLabel = $"{roomNumber}-{bedNumber}";
                    if (!context.Beds.Any(b => b.RoomId == room.RoomId && b.BedNumber == bedLabel))
                    {
                        context.Beds.Add(new Bed
                        {
                            RoomId = room.RoomId,
                            BedNumber = bedLabel,
                            IsOccupied = false,
                            Status = "Available"
                        });
                    }
                }
                context.SaveChanges();
            }

            residence.Capacity = context.Rooms
                .Where(r => r.ResidenceId == residence.ResidenceId && !r.IsArchived)
                .Select(r => r.Capacity)
                .DefaultIfEmpty(0)
                .Sum();

            residence.OccupiedBeds = context.ResidenceAssignments
                .Count(a => a.ResidenceId == residence.ResidenceId && a.IsActive);

            foreach (var room in context.Rooms.Where(r => r.ResidenceId == residence.ResidenceId).ToList())
            {
                room.OccupiedBeds = context.ResidenceAssignments.Count(a => a.RoomId == room.RoomId && a.IsActive);
                room.IsFull = room.OccupiedBeds >= room.Capacity;

                var occupiedBedIds = context.ResidenceAssignments
                    .Where(a => a.RoomId == room.RoomId && a.IsActive)
                    .Select(a => a.BedId)
                    .ToList();

                foreach (var bed in context.Beds.Where(b => b.RoomId == room.RoomId).ToList())
                {
                    var occupied = occupiedBedIds.Contains(bed.BedId);
                    bed.IsOccupied = occupied;
                    bed.Status = occupied ? "Occupied" : "Available";
                    if (!occupied)
                        bed.OccupiedByStudentId = null;
                }
            }

            context.SaveChanges();
        }
        // 3. Seed Campus Rules separately outside the loop
        if (!context.CampusRules.Any())
        {
            context.CampusRules.AddOrUpdate(
                cr => cr.CampusRuleId,
                new CampusRule
                {
                    CampusRuleId = 1,
                    RuleName = "Morning Prep & House Inspection",
                    StartTime = new TimeSpan(7, 30, 0),
                    EndTime = new TimeSpan(9, 0, 0),
                    IsActive = true,
                    IsStrictBlock = false
                },
                new CampusRule
                {
                    CampusRuleId = 2,
                    RuleName = "Night House Curfew",
                    StartTime = new TimeSpan(21, 0, 0),
                    EndTime = new TimeSpan(23, 59, 0),
                    IsActive = true,
                    IsStrictBlock = true
                },
                new CampusRule
                {
                    CampusRuleId = 3,
                    RuleName = "Evening Prep Window",
                    StartTime = new TimeSpan(18, 30, 0),
                    EndTime = new TimeSpan(20, 30, 0),
                    IsActive = true,
                    IsStrictBlock = false
                }
            );
            context.SaveChanges();
        }
    }
    private void SeedMenuItems(DBContextClass context)
    {
        var menuItems = new[]
        {
        // ============================================================
        // BREAKFAST — 10
        // ============================================================

        new
        {
            Name = "Oats, Fruit & Yoghurt",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 420m,
            ProteinGramsPerPortion = 18m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Scrambled Eggs & Toast",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 450m,
            ProteinGramsPerPortion = 24m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        new
        {
            Name = "Breakfast Wrap",
            DietaryClassification = "Standard",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 510m,
            ProteinGramsPerPortion = 25m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        new
        {
            Name = "Weet-Bix, Banana & Milk",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 400m,
            ProteinGramsPerPortion = 16m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        new
        {
            Name = "Peanut Butter Toast & Fruit",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 470m,
            ProteinGramsPerPortion = 17m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Vegetable Omelette & Toast",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 460m,
            ProteinGramsPerPortion = 27m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Chicken Breakfast Muffin",
            DietaryClassification = "Standard",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 490m,
            ProteinGramsPerPortion = 30m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        new
        {
            Name = "Greek Yoghurt, Granola & Berries",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 430m,
            ProteinGramsPerPortion = 21m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "French Toast & Fresh Fruit",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 480m,
            ProteinGramsPerPortion = 19m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Egg & Cheese Breakfast Bowl",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = true,
            IsLunchItem = false,
            IsDinnerItem = false,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 520m,
            ProteinGramsPerPortion = 29m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        // ============================================================
        // LUNCH — 10
        // ============================================================

        new
        {
            Name = "Farm Fresh Chicken & Vegetables",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 620m,
            ProteinGramsPerPortion = 42m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Beef Pasta",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 680m,
            ProteinGramsPerPortion = 38m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        new
        {
            Name = "Vegetable Curry",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 510m,
            ProteinGramsPerPortion = 18m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Grilled Fish & Rice",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 590m,
            ProteinGramsPerPortion = 45m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        new
        {
            Name = "Chicken & Brown Rice Bowl",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 640m,
            ProteinGramsPerPortion = 44m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Beef & Vegetable Stir-Fry",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 650m,
            ProteinGramsPerPortion = 40m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Lentil & Vegetable Stew",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 530m,
            ProteinGramsPerPortion = 23m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Chicken Pasta Primavera",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 660m,
            ProteinGramsPerPortion = 43m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Chickpea & Rice Bowl",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 560m,
            ProteinGramsPerPortion = 20m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Turkey & Couscous Bowl",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = true,
            IsDinnerItem = true,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 610m,
            ProteinGramsPerPortion = 42m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        // ============================================================
        // DINNER — 10
        // ============================================================

        new
        {
            Name = "Chicken & Sweet Potato",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 630m,
            ProteinGramsPerPortion = 44m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Beef & Vegetable Casserole",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 670m,
            ProteinGramsPerPortion = 41m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Chickpea & Vegetable Curry",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 540m,
            ProteinGramsPerPortion = 21m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Roast Chicken, Potatoes & Vegetables",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 690m,
            ProteinGramsPerPortion = 46m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Baked Hake & Potato Wedges",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = false,
            CaloriesPerPortion = 610m,
            ProteinGramsPerPortion = 43m,
            FarmAvailablePortions = 0,
            ExternalAvailablePortions = 150
        },

        new
        {
            Name = "Chicken & Vegetable Noodles",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 650m,
            ProteinGramsPerPortion = 40m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Beef Lasagne & Garden Salad",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 720m,
            ProteinGramsPerPortion = 39m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Vegetable & Bean Chilli",
            DietaryClassification = "Vegetarian",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 570m,
            ProteinGramsPerPortion = 24m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Herb Chicken, Rice & Broccoli",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 640m,
            ProteinGramsPerPortion = 45m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        },

        new
        {
            Name = "Fish, Rice & Mixed Vegetables",
            DietaryClassification = "Standard",
            IsBreakfastItem = false,
            IsLunchItem = false,
            IsDinnerItem = true,
            IsFarmGrownProduce = true,
            CaloriesPerPortion = 600m,
            ProteinGramsPerPortion = 42m,
            FarmAvailablePortions = 150,
            ExternalAvailablePortions = 100
        }
    };

        foreach (var item in menuItems)
        {
            MenuItem existingItem = context.MenuItems
                .FirstOrDefault(x => x.Name == item.Name);

            if (existingItem == null)
            {
                context.MenuItems.Add(new MenuItem
                {
                    Name = item.Name,
                    DietaryClassification = item.DietaryClassification,

                    IsBreakfastItem = item.IsBreakfastItem,
                    IsLunchItem = item.IsLunchItem,
                    IsDinnerItem = item.IsDinnerItem,

                    IsFarmGrownProduce = item.IsFarmGrownProduce,

                    CaloriesPerPortion = item.CaloriesPerPortion,
                    ProteinGramsPerPortion = item.ProteinGramsPerPortion,

                    FarmAvailablePortions = item.FarmAvailablePortions,
                    ExternalAvailablePortions = item.ExternalAvailablePortions,

                    IsActive = true
                });
            }
            else
            {
                existingItem.DietaryClassification =
                    item.DietaryClassification;

                existingItem.IsBreakfastItem =
                    item.IsBreakfastItem;

                existingItem.IsLunchItem =
                    item.IsLunchItem;

                existingItem.IsDinnerItem =
                    item.IsDinnerItem;

                existingItem.IsFarmGrownProduce =
                    item.IsFarmGrownProduce;

                existingItem.CaloriesPerPortion =
                    item.CaloriesPerPortion;

                existingItem.ProteinGramsPerPortion =
                    item.ProteinGramsPerPortion;

                existingItem.FarmAvailablePortions =
                    item.FarmAvailablePortions;

                existingItem.ExternalAvailablePortions =
                    item.ExternalAvailablePortions;

                existingItem.IsActive = true;
            }
        }

        context.SaveChanges();
    }
    // =============================================================
    // Recipe + Ingredient Foundation
    // Links the 30 seeded MenuItems to reusable recipes and ingredients
    // Used by Kitchen Requirements / Bill of Materials calculation
    // =============================================================
    private void SeedRecipesAndIngredients(DBContextClass context)
    {
        // =============================================================
        // INGREDIENTS
        // =============================================================

        var ingredients = new[]
        {
        // Proteins
        new
        {
            Name = "Chicken Breast",
            Unit = "g",
            CaloriesPerUnit = 1.65m,
            ProteinGramsPerUnit = 0.31m,
            CarbohydrateGramsPerUnit = 0m,
            FatGramsPerUnit = 0.036m
        },

        new
        {
            Name = "Lean Beef",
            Unit = "g",
            CaloriesPerUnit = 2.50m,
            ProteinGramsPerUnit = 0.26m,
            CarbohydrateGramsPerUnit = 0m,
            FatGramsPerUnit = 0.17m
        },

        new
        {
            Name = "Hake Fillet",
            Unit = "g",
            CaloriesPerUnit = 0.90m,
            ProteinGramsPerUnit = 0.19m,
            CarbohydrateGramsPerUnit = 0m,
            FatGramsPerUnit = 0.01m
        },

        new
        {
            Name = "Turkey Breast",
            Unit = "g",
            CaloriesPerUnit = 1.35m,
            ProteinGramsPerUnit = 0.29m,
            CarbohydrateGramsPerUnit = 0m,
            FatGramsPerUnit = 0.015m
        },

        // Grains / starches
        new
        {
            Name = "White Rice",
            Unit = "g",
            CaloriesPerUnit = 1.30m,
            ProteinGramsPerUnit = 0.027m,
            CarbohydrateGramsPerUnit = 0.28m,
            FatGramsPerUnit = 0.003m
        },

        new
        {
            Name = "Brown Rice",
            Unit = "g",
            CaloriesPerUnit = 1.23m,
            ProteinGramsPerUnit = 0.027m,
            CarbohydrateGramsPerUnit = 0.255m,
            FatGramsPerUnit = 0.01m
        },

        new
        {
            Name = "Pasta",
            Unit = "g",
            CaloriesPerUnit = 1.31m,
            ProteinGramsPerUnit = 0.05m,
            CarbohydrateGramsPerUnit = 0.25m,
            FatGramsPerUnit = 0.01m
        },

        new
        {
            Name = "Couscous",
            Unit = "g",
            CaloriesPerUnit = 1.12m,
            ProteinGramsPerUnit = 0.038m,
            CarbohydrateGramsPerUnit = 0.23m,
            FatGramsPerUnit = 0.002m
        },

        new
        {
            Name = "Potato",
            Unit = "g",
            CaloriesPerUnit = 0.77m,
            ProteinGramsPerUnit = 0.02m,
            CarbohydrateGramsPerUnit = 0.17m,
            FatGramsPerUnit = 0.001m
        },

        new
        {
            Name = "Sweet Potato",
            Unit = "g",
            CaloriesPerUnit = 0.86m,
            ProteinGramsPerUnit = 0.016m,
            CarbohydrateGramsPerUnit = 0.20m,
            FatGramsPerUnit = 0.001m
        },

        new
        {
            Name = "Oats",
            Unit = "g",
            CaloriesPerUnit = 3.89m,
            ProteinGramsPerUnit = 0.17m,
            CarbohydrateGramsPerUnit = 0.66m,
            FatGramsPerUnit = 0.07m
        },

        new
        {
            Name = "Granola",
            Unit = "g",
            CaloriesPerUnit = 4.50m,
            ProteinGramsPerUnit = 0.10m,
            CarbohydrateGramsPerUnit = 0.64m,
            FatGramsPerUnit = 0.16m
        },

        // Dairy / eggs
        new
        {
            Name = "Egg",
            Unit = "g",
            CaloriesPerUnit = 1.43m,
            ProteinGramsPerUnit = 0.126m,
            CarbohydrateGramsPerUnit = 0.007m,
            FatGramsPerUnit = 0.095m
        },

        new
        {
            Name = "Milk",
            Unit = "ml",
            CaloriesPerUnit = 0.60m,
            ProteinGramsPerUnit = 0.033m,
            CarbohydrateGramsPerUnit = 0.048m,
            FatGramsPerUnit = 0.032m
        },

        new
        {
            Name = "Greek Yoghurt",
            Unit = "g",
            CaloriesPerUnit = 0.73m,
            ProteinGramsPerUnit = 0.10m,
            CarbohydrateGramsPerUnit = 0.04m,
            FatGramsPerUnit = 0.02m
        },

        new
        {
            Name = "Cheese",
            Unit = "g",
            CaloriesPerUnit = 4.00m,
            ProteinGramsPerUnit = 0.25m,
            CarbohydrateGramsPerUnit = 0.01m,
            FatGramsPerUnit = 0.33m
        },

        // Bread / breakfast
        new
        {
            Name = "Whole Wheat Bread",
            Unit = "g",
            CaloriesPerUnit = 2.50m,
            ProteinGramsPerUnit = 0.13m,
            CarbohydrateGramsPerUnit = 0.43m,
            FatGramsPerUnit = 0.04m
        },

        new
        {
            Name = "Breakfast Wrap",
            Unit = "g",
            CaloriesPerUnit = 3.10m,
            ProteinGramsPerUnit = 0.09m,
            CarbohydrateGramsPerUnit = 0.52m,
            FatGramsPerUnit = 0.08m
        },

        new
        {
            Name = "Peanut Butter",
            Unit = "g",
            CaloriesPerUnit = 5.88m,
            ProteinGramsPerUnit = 0.25m,
            CarbohydrateGramsPerUnit = 0.20m,
            FatGramsPerUnit = 0.50m
        },

        new
        {
            Name = "Weet-Bix",
            Unit = "g",
            CaloriesPerUnit = 3.50m,
            ProteinGramsPerUnit = 0.12m,
            CarbohydrateGramsPerUnit = 0.69m,
            FatGramsPerUnit = 0.03m
        },

        // Legumes
        new
        {
            Name = "Chickpeas",
            Unit = "g",
            CaloriesPerUnit = 1.64m,
            ProteinGramsPerUnit = 0.089m,
            CarbohydrateGramsPerUnit = 0.27m,
            FatGramsPerUnit = 0.026m
        },

        new
        {
            Name = "Lentils",
            Unit = "g",
            CaloriesPerUnit = 1.16m,
            ProteinGramsPerUnit = 0.09m,
            CarbohydrateGramsPerUnit = 0.20m,
            FatGramsPerUnit = 0.004m
        },

        new
        {
            Name = "Kidney Beans",
            Unit = "g",
            CaloriesPerUnit = 1.27m,
            ProteinGramsPerUnit = 0.089m,
            CarbohydrateGramsPerUnit = 0.23m,
            FatGramsPerUnit = 0.005m
        },

        // Vegetables
        new
        {
            Name = "Carrots",
            Unit = "g",
            CaloriesPerUnit = 0.41m,
            ProteinGramsPerUnit = 0.009m,
            CarbohydrateGramsPerUnit = 0.096m,
            FatGramsPerUnit = 0.002m
        },

        new
        {
            Name = "Peas",
            Unit = "g",
            CaloriesPerUnit = 0.81m,
            ProteinGramsPerUnit = 0.054m,
            CarbohydrateGramsPerUnit = 0.145m,
            FatGramsPerUnit = 0.004m
        },

        new
        {
            Name = "Broccoli",
            Unit = "g",
            CaloriesPerUnit = 0.34m,
            ProteinGramsPerUnit = 0.028m,
            CarbohydrateGramsPerUnit = 0.07m,
            FatGramsPerUnit = 0.004m
        },

        new
        {
            Name = "Mixed Vegetables",
            Unit = "g",
            CaloriesPerUnit = 0.45m,
            ProteinGramsPerUnit = 0.02m,
            CarbohydrateGramsPerUnit = 0.08m,
            FatGramsPerUnit = 0.005m
        },

        new
        {
            Name = "Spinach",
            Unit = "g",
            CaloriesPerUnit = 0.23m,
            ProteinGramsPerUnit = 0.029m,
            CarbohydrateGramsPerUnit = 0.036m,
            FatGramsPerUnit = 0.004m
        },

        new
        {
            Name = "Tomato",
            Unit = "g",
            CaloriesPerUnit = 0.18m,
            ProteinGramsPerUnit = 0.009m,
            CarbohydrateGramsPerUnit = 0.039m,
            FatGramsPerUnit = 0.002m
        },

        new
        {
            Name = "Onion",
            Unit = "g",
            CaloriesPerUnit = 0.40m,
            ProteinGramsPerUnit = 0.011m,
            CarbohydrateGramsPerUnit = 0.093m,
            FatGramsPerUnit = 0.001m
        },

        // Fruit
        new
        {
            Name = "Banana",
            Unit = "g",
            CaloriesPerUnit = 0.89m,
            ProteinGramsPerUnit = 0.011m,
            CarbohydrateGramsPerUnit = 0.23m,
            FatGramsPerUnit = 0.003m
        },

        new
        {
            Name = "Fresh Fruit",
            Unit = "g",
            CaloriesPerUnit = 0.60m,
            ProteinGramsPerUnit = 0.006m,
            CarbohydrateGramsPerUnit = 0.15m,
            FatGramsPerUnit = 0.002m
        },

        new
        {
            Name = "Berries",
            Unit = "g",
            CaloriesPerUnit = 0.50m,
            ProteinGramsPerUnit = 0.007m,
            CarbohydrateGramsPerUnit = 0.12m,
            FatGramsPerUnit = 0.003m
        },

        // Cooking ingredients
        new
        {
            Name = "Cooking Oil",
            Unit = "ml",
            CaloriesPerUnit = 8.00m,
            ProteinGramsPerUnit = 0m,
            CarbohydrateGramsPerUnit = 0m,
            FatGramsPerUnit = 0.90m
        },

        new
        {
            Name = "Curry Spice Mix",
            Unit = "g",
            CaloriesPerUnit = 3.00m,
            ProteinGramsPerUnit = 0.10m,
            CarbohydrateGramsPerUnit = 0.50m,
            FatGramsPerUnit = 0.10m
        },

        new
        {
            Name = "Seasoning",
            Unit = "g",
            CaloriesPerUnit = 1.00m,
            ProteinGramsPerUnit = 0m,
            CarbohydrateGramsPerUnit = 0.20m,
            FatGramsPerUnit = 0m
        },

        new
        {
            Name = "Tomato Sauce",
            Unit = "g",
            CaloriesPerUnit = 0.80m,
            ProteinGramsPerUnit = 0.02m,
            CarbohydrateGramsPerUnit = 0.15m,
            FatGramsPerUnit = 0.01m
        }
    };

        // Create/update ingredients.
        foreach (var item in ingredients)
        {
            var ingredient = context.Ingredients
                .FirstOrDefault(x => x.Name == item.Name);

            if (ingredient == null)
            {
                ingredient = new Ingredient
                {
                    Name = item.Name,
                    Unit = item.Unit,
                    CaloriesPerUnit = item.CaloriesPerUnit,
                    ProteinGramsPerUnit = item.ProteinGramsPerUnit,
                    CarbohydrateGramsPerUnit = item.CarbohydrateGramsPerUnit,
                    FatGramsPerUnit = item.FatGramsPerUnit,
                    IsActive = true
                };

                context.Ingredients.Add(ingredient);
            }
            else
            {
                ingredient.Unit = item.Unit;
                ingredient.CaloriesPerUnit = item.CaloriesPerUnit;
                ingredient.ProteinGramsPerUnit = item.ProteinGramsPerUnit;
                ingredient.CarbohydrateGramsPerUnit = item.CarbohydrateGramsPerUnit;
                ingredient.FatGramsPerUnit = item.FatGramsPerUnit;
                ingredient.IsActive = true;
            }
        }

        context.SaveChanges();

        // =============================================================
        // ALLERGEN TAGS
        // =============================================================

        var allergenMap = new Dictionary<string, string>
        {
            { "Milk",              "Dairy" },
            { "Greek Yoghurt",     "Dairy" },
            { "Cheese",            "Dairy" },
            { "Egg",               "Eggs" },
            { "Whole Wheat Bread", "Gluten" },
            { "Breakfast Wrap",    "Gluten" },
            { "Pasta",             "Gluten" },
            { "Couscous",          "Gluten" },
            { "Weet-Bix",          "Gluten" },
            { "Granola",           "Gluten" },
            { "Peanut Butter",     "Peanuts" },
            { "Hake Fillet",       "Fish" }
        };

        foreach (var kvp in allergenMap)
        {
            var ing = context.Ingredients
                .FirstOrDefault(x => x.Name == kvp.Key);

            if (ing != null)
            {
                ing.Allergens = kvp.Value;
            }
        }

        context.SaveChanges();

        // =============================================================
        // DIETARY TAGS (vegetarian / vegan / halal checks)
        // Fish, egg and dairy come from the allergen tags above.
        // No ingredient is tagged "Halal" — only add that once the
        // kitchen has confirmed the supplier is halal-certified.
        // =============================================================

        var dietaryTagMap = new Dictionary<string, string>
        {
            { "Chicken Breast", "Poultry" },
            { "Turkey Breast",  "Poultry" },
            { "Lean Beef",      "Meat" }
        };

        foreach (var kvp in dietaryTagMap)
        {
            var ing = context.Ingredients
                .FirstOrDefault(x => x.Name == kvp.Key);

            if (ing != null)
            {
                ing.DietaryTags = kvp.Value;
            }
        }

        context.SaveChanges();

        // =============================================================
        // RECIPES
        // =============================================================

        var recipes = new[]
        {
        // =========================================================
        // BREAKFAST
        // =========================================================

        new
        {
            Name = "Oats, Fruit & Yoghurt",
            PreparationNotes = "Prepare oats and serve with yoghurt and fresh fruit.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Oats", Quantity = 70m, Notes = "Cooked with water." },
                new { Name = "Greek Yoghurt", Quantity = 150m, Notes = "Serve chilled." },
                new { Name = "Fresh Fruit", Quantity = 100m, Notes = "Seasonal fruit." }
            }
        },

        new
        {
            Name = "Scrambled Eggs & Toast",
            PreparationNotes = "Scramble eggs and serve with toasted whole wheat bread.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Egg", Quantity = 180m, Notes = "Scrambled." },
                new { Name = "Whole Wheat Bread", Quantity = 80m, Notes = "Toasted." },
                new { Name = "Cooking Oil", Quantity = 5m, Notes = "For cooking eggs." }
            }
        },

        new
        {
            Name = "Breakfast Wrap",
            PreparationNotes = "Prepare egg and vegetable filling and serve in a breakfast wrap.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Breakfast Wrap", Quantity = 90m, Notes = "One wrap." },
                new { Name = "Egg", Quantity = 100m, Notes = "Scrambled." },
                new { Name = "Cheese", Quantity = 30m, Notes = "Grated." },
                new { Name = "Tomato", Quantity = 40m, Notes = "Diced." },
                new { Name = "Spinach", Quantity = 30m, Notes = "Fresh." },
                new { Name = "Cooking Oil", Quantity = 5m, Notes = "For cooking." }
            }
        },

        new
        {
            Name = "Weet-Bix, Banana & Milk",
            PreparationNotes = "Serve Weet-Bix with milk and sliced banana.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Weet-Bix", Quantity = 60m, Notes = "Two to three pieces." },
                new { Name = "Milk", Quantity = 250m, Notes = "Cold." },
                new { Name = "Banana", Quantity = 100m, Notes = "Sliced." }
            }
        },

        new
        {
            Name = "Peanut Butter Toast & Fruit",
            PreparationNotes = "Toast bread, spread with peanut butter and serve with fresh fruit.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Whole Wheat Bread", Quantity = 100m, Notes = "Toasted." },
                new { Name = "Peanut Butter", Quantity = 30m, Notes = "Spread evenly." },
                new { Name = "Fresh Fruit", Quantity = 120m, Notes = "Seasonal fruit." }
            }
        },

        new
        {
            Name = "Vegetable Omelette & Toast",
            PreparationNotes = "Prepare vegetable omelette and serve with toasted bread.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Egg", Quantity = 180m, Notes = "Omelette base." },
                new { Name = "Tomato", Quantity = 40m, Notes = "Diced." },
                new { Name = "Spinach", Quantity = 30m, Notes = "Fresh." },
                new { Name = "Onion", Quantity = 20m, Notes = "Diced." },
                new { Name = "Whole Wheat Bread", Quantity = 80m, Notes = "Toasted." },
                new { Name = "Cooking Oil", Quantity = 5m, Notes = "For cooking." }
            }
        },

        new
        {
            Name = "Chicken Breakfast Muffin",
            PreparationNotes = "Prepare chicken and egg filling in a toasted breakfast muffin.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 90m, Notes = "Cooked and sliced." },
                new { Name = "Egg", Quantity = 60m, Notes = "Cooked." },
                new { Name = "Whole Wheat Bread", Quantity = 80m, Notes = "Used as muffin/bread portion." },
                new { Name = "Cheese", Quantity = 20m, Notes = "Sliced." }
            }
        },

        new
        {
            Name = "Greek Yoghurt, Granola & Berries",
            PreparationNotes = "Layer yoghurt with granola and berries.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Greek Yoghurt", Quantity = 200m, Notes = "Chilled." },
                new { Name = "Granola", Quantity = 60m, Notes = "Serve dry." },
                new { Name = "Berries", Quantity = 100m, Notes = "Fresh or frozen." }
            }
        },

        new
        {
            Name = "French Toast & Fresh Fruit",
            PreparationNotes = "Prepare French toast and serve with fresh fruit.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Whole Wheat Bread", Quantity = 100m, Notes = "Soaked in egg mixture." },
                new { Name = "Egg", Quantity = 60m, Notes = "For French toast." },
                new { Name = "Milk", Quantity = 50m, Notes = "For egg mixture." },
                new { Name = "Fresh Fruit", Quantity = 120m, Notes = "Seasonal fruit." },
                new { Name = "Cooking Oil", Quantity = 5m, Notes = "For frying." }
            }
        },

        new
        {
            Name = "Egg & Cheese Breakfast Bowl",
            PreparationNotes = "Prepare scrambled eggs with cheese and vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Egg", Quantity = 180m, Notes = "Scrambled." },
                new { Name = "Cheese", Quantity = 30m, Notes = "Grated." },
                new { Name = "Tomato", Quantity = 40m, Notes = "Diced." },
                new { Name = "Spinach", Quantity = 30m, Notes = "Fresh." },
                new { Name = "Potato", Quantity = 100m, Notes = "Cooked cubes." },
                new { Name = "Cooking Oil", Quantity = 5m, Notes = "For cooking." }
            }
        },


        // =========================================================
        // LUNCH / SHARED LUNCH-DINNER RECIPES
        // =========================================================

        new
        {
            Name = "Farm Fresh Chicken & Vegetables",
            PreparationNotes = "Grill chicken and serve with seasonal vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 180m, Notes = "Grilled." },
                new { Name = "Mixed Vegetables", Quantity = 180m, Notes = "Steamed." },
                new { Name = "Brown Rice", Quantity = 150m, Notes = "Cooked." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Beef Pasta",
            PreparationNotes = "Prepare lean beef pasta with tomato sauce.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Lean Beef", Quantity = 150m, Notes = "Minced." },
                new { Name = "Pasta", Quantity = 180m, Notes = "Cooked portion." },
                new { Name = "Tomato Sauce", Quantity = 120m, Notes = "Sauce base." },
                new { Name = "Onion", Quantity = 30m, Notes = "Diced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Vegetable Curry",
            PreparationNotes = "Cook mixed vegetables in a mild curry sauce and serve with rice.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Mixed Vegetables", Quantity = 250m, Notes = "Seasonal vegetables." },
                new { Name = "White Rice", Quantity = 180m, Notes = "Cooked." },
                new { Name = "Onion", Quantity = 40m, Notes = "Diced." },
                new { Name = "Tomato", Quantity = 60m, Notes = "Diced." },
                new { Name = "Curry Spice Mix", Quantity = 8m, Notes = "Mild curry." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." }
            }
        },

        new
        {
            Name = "Grilled Fish & Rice",
            PreparationNotes = "Grill hake and serve with rice and vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Hake Fillet", Quantity = 180m, Notes = "Grilled." },
                new { Name = "White Rice", Quantity = 200m, Notes = "Cooked." },
                new { Name = "Mixed Vegetables", Quantity = 120m, Notes = "Steamed." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Chicken & Brown Rice Bowl",
            PreparationNotes = "Serve grilled chicken with brown rice and vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 180m, Notes = "Grilled." },
                new { Name = "Brown Rice", Quantity = 200m, Notes = "Cooked." },
                new { Name = "Carrots", Quantity = 80m, Notes = "Steamed." },
                new { Name = "Peas", Quantity = 60m, Notes = "Cooked." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Beef & Vegetable Stir-Fry",
            PreparationNotes = "Stir-fry beef with seasonal vegetables and serve with rice.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Lean Beef", Quantity = 170m, Notes = "Thinly sliced." },
                new { Name = "Mixed Vegetables", Quantity = 200m, Notes = "Stir-fry vegetables." },
                new { Name = "Brown Rice", Quantity = 180m, Notes = "Cooked." },
                new { Name = "Onion", Quantity = 30m, Notes = "Sliced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For stir-frying." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Lentil & Vegetable Stew",
            PreparationNotes = "Slow cook lentils with vegetables and seasoning.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Lentils", Quantity = 180m, Notes = "Cooked." },
                new { Name = "Mixed Vegetables", Quantity = 180m, Notes = "Diced." },
                new { Name = "Potato", Quantity = 100m, Notes = "Cubed." },
                new { Name = "Tomato", Quantity = 80m, Notes = "Diced." },
                new { Name = "Onion", Quantity = 40m, Notes = "Diced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Chicken Pasta Primavera",
            PreparationNotes = "Combine grilled chicken, pasta and seasonal vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 170m, Notes = "Grilled." },
                new { Name = "Pasta", Quantity = 190m, Notes = "Cooked." },
                new { Name = "Mixed Vegetables", Quantity = 150m, Notes = "Seasonal." },
                new { Name = "Tomato", Quantity = 50m, Notes = "Diced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Chickpea & Rice Bowl",
            PreparationNotes = "Serve chickpeas with rice and mixed vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chickpeas", Quantity = 180m, Notes = "Cooked." },
                new { Name = "White Rice", Quantity = 200m, Notes = "Cooked." },
                new { Name = "Carrots", Quantity = 70m, Notes = "Steamed." },
                new { Name = "Peas", Quantity = 60m, Notes = "Cooked." },
                new { Name = "Tomato", Quantity = 50m, Notes = "Diced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Turkey & Couscous Bowl",
            PreparationNotes = "Serve grilled turkey with couscous and vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Turkey Breast", Quantity = 180m, Notes = "Grilled." },
                new { Name = "Couscous", Quantity = 180m, Notes = "Prepared." },
                new { Name = "Mixed Vegetables", Quantity = 150m, Notes = "Steamed." },
                new { Name = "Carrots", Quantity = 60m, Notes = "Steamed." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },


        // =========================================================
        // DINNER
        // =========================================================

        new
        {
            Name = "Chicken & Sweet Potato",
            PreparationNotes = "Roast chicken and sweet potato and serve with vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 180m, Notes = "Roasted." },
                new { Name = "Sweet Potato", Quantity = 220m, Notes = "Roasted." },
                new { Name = "Mixed Vegetables", Quantity = 150m, Notes = "Steamed." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For roasting." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Beef & Vegetable Casserole",
            PreparationNotes = "Slow cook beef with vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Lean Beef", Quantity = 180m, Notes = "Cubed." },
                new { Name = "Potato", Quantity = 150m, Notes = "Cubed." },
                new { Name = "Carrots", Quantity = 80m, Notes = "Sliced." },
                new { Name = "Peas", Quantity = 60m, Notes = "Cooked." },
                new { Name = "Tomato Sauce", Quantity = 100m, Notes = "Casserole base." },
                new { Name = "Onion", Quantity = 40m, Notes = "Diced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Chickpea & Vegetable Curry",
            PreparationNotes = "Cook chickpeas and vegetables in a mild curry sauce.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chickpeas", Quantity = 200m, Notes = "Cooked." },
                new { Name = "Mixed Vegetables", Quantity = 180m, Notes = "Seasonal." },
                new { Name = "White Rice", Quantity = 180m, Notes = "Cooked." },
                new { Name = "Onion", Quantity = 40m, Notes = "Diced." },
                new { Name = "Tomato", Quantity = 60m, Notes = "Diced." },
                new { Name = "Curry Spice Mix", Quantity = 8m, Notes = "Mild curry." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." }
            }
        },

        new
        {
            Name = "Roast Chicken, Potatoes & Vegetables",
            PreparationNotes = "Roast chicken and potatoes and serve with vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 190m, Notes = "Roasted." },
                new { Name = "Potato", Quantity = 220m, Notes = "Roasted." },
                new { Name = "Carrots", Quantity = 80m, Notes = "Roasted." },
                new { Name = "Peas", Quantity = 60m, Notes = "Cooked." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For roasting." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Baked Hake & Potato Wedges",
            PreparationNotes = "Bake hake and potato wedges and serve with vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Hake Fillet", Quantity = 180m, Notes = "Baked." },
                new { Name = "Potato", Quantity = 220m, Notes = "Wedges." },
                new { Name = "Mixed Vegetables", Quantity = 120m, Notes = "Steamed." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For baking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Chicken & Vegetable Noodles",
            PreparationNotes = "Stir-fry chicken and vegetables with noodles.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 170m, Notes = "Sliced." },
                new { Name = "Pasta", Quantity = 190m, Notes = "Noodle substitute." },
                new { Name = "Mixed Vegetables", Quantity = 180m, Notes = "Stir-fry vegetables." },
                new { Name = "Carrots", Quantity = 50m, Notes = "Sliced." },
                new { Name = "Onion", Quantity = 30m, Notes = "Sliced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For stir-frying." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Beef Lasagne & Garden Salad",
            PreparationNotes = "Prepare beef lasagne and serve with garden salad.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Lean Beef", Quantity = 150m, Notes = "Minced." },
                new { Name = "Pasta", Quantity = 180m, Notes = "Lasagne sheets." },
                new { Name = "Tomato Sauce", Quantity = 120m, Notes = "Sauce." },
                new { Name = "Cheese", Quantity = 40m, Notes = "Grated." },
                new { Name = "Mixed Vegetables", Quantity = 100m, Notes = "Salad vegetables." },
                new { Name = "Onion", Quantity = 30m, Notes = "Diced." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." }
            }
        },

        new
        {
            Name = "Vegetable & Bean Chilli",
            PreparationNotes = "Cook beans and vegetables in a mild chilli sauce.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Kidney Beans", Quantity = 180m, Notes = "Cooked." },
                new { Name = "Mixed Vegetables", Quantity = 180m, Notes = "Diced." },
                new { Name = "Tomato", Quantity = 100m, Notes = "Diced." },
                new { Name = "Onion", Quantity = 40m, Notes = "Diced." },
                new { Name = "White Rice", Quantity = 160m, Notes = "Cooked." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        },

        new
        {
            Name = "Herb Chicken, Rice & Broccoli",
            PreparationNotes = "Serve herb-seasoned chicken with rice and broccoli.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Chicken Breast", Quantity = 180m, Notes = "Herb grilled." },
                new { Name = "White Rice", Quantity = 200m, Notes = "Cooked." },
                new { Name = "Broccoli", Quantity = 120m, Notes = "Steamed." },
                new { Name = "Carrots", Quantity = 60m, Notes = "Steamed." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "Herb seasoning." }
            }
        },

        new
        {
            Name = "Fish, Rice & Mixed Vegetables",
            PreparationNotes = "Serve baked fish with rice and mixed vegetables.",
            StandardPortionCount = 1,
            Ingredients = new[]
            {
                new { Name = "Hake Fillet", Quantity = 180m, Notes = "Baked." },
                new { Name = "White Rice", Quantity = 200m, Notes = "Cooked." },
                new { Name = "Mixed Vegetables", Quantity = 180m, Notes = "Steamed." },
                new { Name = "Cooking Oil", Quantity = 10m, Notes = "For cooking." },
                new { Name = "Seasoning", Quantity = 5m, Notes = "To taste." }
            }
        }
    };

        // =============================================================
        // CREATE / UPDATE RECIPES AND LINK MENU ITEMS
        // =============================================================

        foreach (var recipeData in recipes)
        {
            Recipe recipe = context.Recipes
                .FirstOrDefault(r => r.Name == recipeData.Name);

            if (recipe == null)
            {
                recipe = new Recipe
                {
                    Name = recipeData.Name,
                    PreparationNotes = recipeData.PreparationNotes,
                    StandardPortionCount = recipeData.StandardPortionCount,
                    IsActive = true
                };

                context.Recipes.Add(recipe);
                context.SaveChanges();
            }
            else
            {
                recipe.PreparationNotes = recipeData.PreparationNotes;
                recipe.StandardPortionCount = recipeData.StandardPortionCount;
                recipe.IsActive = true;
            }


            // Find the corresponding MenuItem.
            MenuItem menuItem = context.MenuItems
                .FirstOrDefault(m => m.Name == recipeData.Name);

            if (menuItem != null)
            {
                menuItem.RecipeId = recipe.Id;
            }


            // =========================================================
            // RECIPE INGREDIENTS
            // =========================================================

            foreach (var ingredientData in recipeData.Ingredients)
            {
                Ingredient ingredient = context.Ingredients
                    .FirstOrDefault(i => i.Name == ingredientData.Name);

                if (ingredient == null)
                    continue;

                RecipeIngredient recipeIngredient =
                    context.RecipeIngredients.FirstOrDefault(
                        ri => ri.RecipeId == recipe.Id &&
                              ri.IngredientId == ingredient.Id);

                if (recipeIngredient == null)
                {
                    recipeIngredient = new RecipeIngredient
                    {
                        RecipeId = recipe.Id,
                        IngredientId = ingredient.Id,
                        QuantityPerStandardPortion = ingredientData.Quantity,
                        PreparationNotes = ingredientData.Notes
                    };

                    context.RecipeIngredients.Add(recipeIngredient);
                }
                else
                {
                    recipeIngredient.QuantityPerStandardPortion =
                        ingredientData.Quantity;

                    recipeIngredient.PreparationNotes =
                        ingredientData.Notes;
                }
            }
        }

        // =============================================================
        // UC15: KITCHEN TIMING + STATION
        // =============================================================

        var kitchenTiming = new Dictionary<string, int[]>
        {
            { "Oats, Fruit & Yoghurt",               new[] { 15, 10 } },
            { "Scrambled Eggs & Toast",              new[] {  5, 10 } },
            { "Breakfast Wrap",                      new[] { 10,  5 } },
            { "Weet-Bix, Banana & Milk",             new[] {  5,  0 } },
            { "Peanut Butter Toast & Fruit",         new[] {  5,  3 } },
            { "Vegetable Omelette & Toast",          new[] { 10, 10 } },
            { "Chicken Breakfast Muffin",            new[] { 15, 10 } },
            { "Greek Yoghurt, Granola & Berries",    new[] {  5,  0 } },
            { "French Toast & Fresh Fruit",          new[] { 10, 10 } },
            { "Egg & Cheese Breakfast Bowl",         new[] { 10, 10 } },

            { "Farm Fresh Chicken & Vegetables",     new[] { 20, 30 } },
            { "Beef Pasta",                          new[] { 20, 45 } },
            { "Vegetable Curry",                     new[] { 15, 30 } },
            { "Grilled Fish & Rice",                 new[] { 15, 25 } },
            { "Chicken & Brown Rice Bowl",           new[] { 15, 30 } },
            { "Beef & Vegetable Stir-Fry",           new[] { 20, 20 } },
            { "Lentil & Vegetable Stew",             new[] { 15, 60 } },
            { "Chicken Pasta Primavera",             new[] { 20, 30 } },
            { "Chickpea & Rice Bowl",                new[] { 15, 25 } },
            { "Turkey & Couscous Bowl",              new[] { 15, 20 } },

            { "Chicken & Sweet Potato",              new[] { 15, 45 } },
            { "Beef & Vegetable Casserole",          new[] { 20, 90 } },
            { "Chickpea & Vegetable Curry",          new[] { 15, 30 } },
            { "Roast Chicken, Potatoes & Vegetables",new[] { 20, 75 } },
            { "Baked Hake & Potato Wedges",          new[] { 15, 35 } },
            { "Chicken & Vegetable Noodles",         new[] { 15, 20 } },
            { "Beef Lasagne & Garden Salad",         new[] { 30, 45 } },
            { "Vegetable & Bean Chilli",             new[] { 15, 45 } },
            { "Herb Chicken, Rice & Broccoli",       new[] { 15, 30 } },
            { "Fish, Rice & Mixed Vegetables",       new[] { 15, 30 } }
        };

        var stationMap = new Dictionary<string, string>
        {
            { "Oats, Fruit & Yoghurt",               "Cold Prep" },
            { "Scrambled Eggs & Toast",              "Stove" },
            { "Breakfast Wrap",                      "Stove" },
            { "Weet-Bix, Banana & Milk",             "Cold Prep" },
            { "Peanut Butter Toast & Fruit",         "Cold Prep" },
            { "Vegetable Omelette & Toast",          "Stove" },
            { "Chicken Breakfast Muffin",            "Stove" },
            { "Greek Yoghurt, Granola & Berries",    "Cold Prep" },
            { "French Toast & Fresh Fruit",          "Stove" },
            { "Egg & Cheese Breakfast Bowl",         "Stove" },

            { "Farm Fresh Chicken & Vegetables",     "Grill" },
            { "Beef Pasta",                          "Stove" },
            { "Vegetable Curry",                     "Stove" },
            { "Grilled Fish & Rice",                 "Grill" },
            { "Chicken & Brown Rice Bowl",           "Grill" },
            { "Beef & Vegetable Stir-Fry",           "Stove" },
            { "Lentil & Vegetable Stew",             "Stove" },
            { "Chicken Pasta Primavera",             "Stove" },
            { "Chickpea & Rice Bowl",                "Stove" },
            { "Turkey & Couscous Bowl",              "Grill" },

            { "Chicken & Sweet Potato",              "Oven" },
            { "Beef & Vegetable Casserole",          "Oven" },
            { "Chickpea & Vegetable Curry",          "Stove" },
            { "Roast Chicken, Potatoes & Vegetables","Oven" },
            { "Baked Hake & Potato Wedges",          "Oven" },
            { "Chicken & Vegetable Noodles",         "Stove" },
            { "Beef Lasagne & Garden Salad",         "Oven" },
            { "Vegetable & Bean Chilli",             "Stove" },
            { "Herb Chicken, Rice & Broccoli",       "Grill" },
            { "Fish, Rice & Mixed Vegetables",       "Oven" }
        };

        foreach (var kvp in kitchenTiming)
        {
            var r = context.Recipes.FirstOrDefault(x => x.Name == kvp.Key);
            if (r == null) continue;

            r.PrepTimeMinutes = kvp.Value[0];
            r.CookTimeMinutes = kvp.Value[1];

            string station;
            if (stationMap.TryGetValue(kvp.Key, out station))
            {
                r.Station = station;
            }
        }

        context.SaveChanges();

        // =============================================================
        // NUTRITION — calculated from each recipe's ingredients so the
        // per-portion values can't contradict the recipe. Replaces the
        // hand-entered calories / protein from SeedMenuItems.
        // =============================================================

        var itemsWithRecipes = context.MenuItems
            .Include("Recipe.RecipeIngredients.Ingredient")
            .Where(m => m.RecipeId != null)
            .ToList();

        foreach (var menuItem in itemsWithRecipes)
        {
            if (menuItem.Recipe == null || !menuItem.Recipe.RecipeIngredients.Any()) continue;

            Michaelhouse.Services.MealLibraryService.ApplyNutrition(
                menuItem,
                Michaelhouse.Services.MealLibraryService.CalculateNutrition(menuItem.Recipe.RecipeIngredients));
        }

        context.SaveChanges();
    }



    // =============================================================
    // Password hashing helper
    // IMPORTANT: This is OUTSIDE SeedMenuItems()
    // =============================================================
    private string HashPassword(string password)
    {
        using (var sha256 =
            System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes =
                System.Text.Encoding.UTF8.GetBytes(password);

            byte[] hash =
                sha256.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
    }

    private void SeedSchoolCalendar(Michaelhouse.Models.DBContextClass context)
    {
        var events = new[]
        {
        // ============================================================
        // AUGUST 2026 - EVENTS FROM YOUR EXAMPLE CALENDAR
        // ============================================================

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Bloemfontein PIE - Prospective Parent Information",
            Description = "Prospective parent event.",
            StartDate = new DateTime(2026, 7, 28),
            EndDate = new DateTime(2026, 7, 28),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Day at Michaelhouse - Option 1",
            Description = "Prospective student day at Michaelhouse.",
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 8, 10),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Nottingham Road PIE - Prospective Parent Information",
            Description = "Prospective parent event.",
            StartDate = new DateTime(2026, 8, 11),
            EndDate = new DateTime(2026, 8, 11),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Hilton PIE - Prospective Parent Information",
            Description = "Prospective parent event.",
            StartDate = new DateTime(2026, 8, 13),
            EndDate = new DateTime(2026, 8, 13),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Half Term",
            Description = "Half term school holiday.",
            StartDate = new DateTime(2026, 8, 20),
            EndDate = new DateTime(2026, 8, 20),
            Category = Schoolcalendarevent.CalendarEventCategory.Holiday,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "M&M - M&M's at Michaelhouse",
            Description = "School event.",
            StartDate = new DateTime(2026, 8, 22),
            EndDate = new DateTime(2026, 8, 22),
            Category = Schoolcalendarevent.CalendarEventCategory.Cultural,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "North Coast PIE - Prospective Parent Information",
            Description = "Prospective parent event.",
            StartDate = new DateTime(2026, 8, 26),
            EndDate = new DateTime(2026, 8, 26),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Durban North PIE - Prospective Parent Information",
            Description = "Prospective parent event.",
            StartDate = new DateTime(2026, 8, 27),
            EndDate = new DateTime(2026, 8, 27),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Highway PIE - Prospective Parent Information",
            Description = "Prospective parent event.",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 1),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Day at Michaelhouse - Option 2",
            Description = "Prospective student day at Michaelhouse.",
            StartDate = new DateTime(2026, 9, 3),
            EndDate = new DateTime(2026, 9, 3),
            Category = Schoolcalendarevent.CalendarEventCategory.Other,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },


        // ============================================================
        // ACADEMIC EVENTS - USEFUL FOR TESTING LEAVE RECOMMENDATIONS
        // ============================================================

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Term 3 Test Week",
            Description = "Important academic assessments for students.",
            StartDate = new DateTime(2026, 9, 7),
            EndDate = new DateTime(2026, 9, 11),
            Category = Schoolcalendarevent.CalendarEventCategory.TestWeek,
            DiscourageLeave = true,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Mathematics Test",
            Description = "Mathematics assessment.",
            StartDate = new DateTime(2026, 9, 9),
            EndDate = new DateTime(2026, 9, 9),
            Category = Schoolcalendarevent.CalendarEventCategory.Exam,
            DiscourageLeave = true,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Science Test",
            Description = "Science assessment.",
            StartDate = new DateTime(2026, 9, 15),
            EndDate = new DateTime(2026, 9, 15),
            Category = Schoolcalendarevent.CalendarEventCategory.Exam,
            DiscourageLeave = true,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Term 3 Examinations",
            Description = "End-of-term examinations.",
            StartDate = new DateTime(2026, 10, 19),
            EndDate = new DateTime(2026, 10, 30),
            Category = Schoolcalendarevent.CalendarEventCategory.Exam,
            DiscourageLeave = true,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },


        // ============================================================
        // SPORTS
        // ============================================================

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Inter-House Rugby",
            Description = "Inter-house rugby fixtures.",
            StartDate = new DateTime(2026, 9, 5),
            EndDate = new DateTime(2026, 9, 5),
            Category = Schoolcalendarevent.CalendarEventCategory.Sports,
            DiscourageLeave = true,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Sports Day",
            Description = "Annual school sports day.",
            StartDate = new DateTime(2026, 9, 19),
            EndDate = new DateTime(2026, 9, 19),
            Category = Schoolcalendarevent.CalendarEventCategory.Sports,
            DiscourageLeave = true,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Saturday Sports Fixtures",
            Description = "School sports fixtures.",
            StartDate = new DateTime(2026, 10, 3),
            EndDate = new DateTime(2026, 10, 3),
            Category = Schoolcalendarevent.CalendarEventCategory.Sports,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },


        // ============================================================
        // CULTURAL / SCHOOL EVENTS
        // ============================================================

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Cultural Evening",
            Description = "School cultural evening.",
            StartDate = new DateTime(2026, 9, 25),
            EndDate = new DateTime(2026, 9, 25),
            Category = Schoolcalendarevent.CalendarEventCategory.Cultural,
            DiscourageLeave = true,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "Music Festival",
            Description = "Annual school music festival.",
            StartDate = new DateTime(2026, 10, 10),
            EndDate = new DateTime(2026, 10, 10),
            Category = Schoolcalendarevent.CalendarEventCategory.Cultural,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false
        },


        // ============================================================
        // HOLIDAYS
        // ============================================================

        new Schoolcalendarevent.SchoolCalendarEvent
        {
            Title = "School Holiday",
            Description = "School holiday.",
            StartDate = new DateTime(2026, 9, 24),
            EndDate = new DateTime(2026, 9, 24),
            Category = Schoolcalendarevent.CalendarEventCategory.Holiday,
            DiscourageLeave = false,
            CreatedBy = "System",
            CreatedAt = DateTime.Now,
            IsArchived = false

        }
    };

        foreach (var calendarEvent in events)
        {
            var exists = context.SchoolCalendarEvents.Any(e =>
                e.Title == calendarEvent.Title &&
                e.StartDate == calendarEvent.StartDate);

            if (!exists)
            {
                context.SchoolCalendarEvents.Add(calendarEvent);
            }
        }

        context.SaveChanges();
    }
    
  }

