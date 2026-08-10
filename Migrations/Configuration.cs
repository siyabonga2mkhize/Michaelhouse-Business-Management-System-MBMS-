using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Michaelhouse.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<Michaelhouse.Models.DBContextClass>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(Michaelhouse.Models.DBContextClass context)
        {
            // 1. Seed default admin account
            if (!context.Users.Any(u => u.Role == "Admin"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Linda Smith",
                    Email = "admin@michaelhouse.co.za",
                    PasswordHash = HashPassword("Admin@123"),
                    Role = "Admin"
                });
                context.SaveChanges();
            }

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
        }

        private string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }
}