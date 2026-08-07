using Michaelhouse.Models;
using System;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;

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
                    Name = "System Admin",
                    Email = "admin@michaelhouse.co.za",
                    PasswordHash = HashPassword("Admin@123"),
                    Role = "Admin"
                });
                context.SaveChanges();
            }

            SeedBoardingHouseTestData(context);
            BoardingManagementSeeder.Seed(context);
            // ----------------------------
            // Seed additional vehicles and multiple trips for drivers
            // Creates one upcoming and one completed trip per seeded driver
            // ----------------------------
            try
            {
                // Ensure we have some vehicles available
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
                    // upcoming trip (in 3..6 days)
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

                        // assign up to 5 students
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

                    // past completed trip (7..10 days ago)
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

                        // assign up to 5 students and mark present before and after
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
                    // create some sample parents + students if none exist
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

                    // pick a scheduled date 3 days from now
                    var scheduledDate = DateTime.Today.AddDays(3);

                    bool exists = context.TripSchedules.Any(ts => ts.DriverId == sibusiso.Id && DbFunctions.TruncateTime(ts.ScheduledDate) == DbFunctions.TruncateTime(scheduledDate));
                    if (!exists)
                    {
                        // create a trip request
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

                        // create or reuse a vehicle and assign it to this trip
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

                        // link vehicle to schedule and create vehicle assignment
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

                        // assign first 5 students to the trip
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

                        // mark first 3 students as present before departure to test 'Done' badges
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

            if (!context.Users.Any(u => u.Role == "InventoryManager"))
            {
                context.Users.Add(new Michaelhouse.Models.AppUser
                {
                    Name = "System Inventory Manager",
                    Email = "inventory@michaelhouse.co.za",
                    PasswordHash = HashPassword("Stock@123"),
                    Role = "InventoryManager"
                });

                context.SaveChanges();
            }

            // 3. Transport Manager (NEW FIX ADDED)
            if (!context.Users.Any(u => u.Role == "Transport Manager"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "System Transport Manager",
                    Email = "transport@michaelhouse.co.za",
                    PasswordHash = HashPassword("Transport@123"),
                    Role = "TransportManager"
                });

                context.SaveChanges();
            }

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

                // Ensure there is a corresponding Teacher record so role-based pages
                // that rely on Session["TeacherId"] won't redirect back to Login.
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

            // 2. Seed Categories (only if empty)
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

            // 3. Seed Products (only if empty)
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

            // 4. Seed Suppliers
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


            // 5. Link Seeded Products to Suppliers (SupplierProducts)

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
      // -- Uniform Supplier Links --
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

      // -- Stationery Supplier Links --
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


            // 4. Seed Drivers (including their AppUser accounts)
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
                    context.SaveChanges(); // needed to get UserId

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
