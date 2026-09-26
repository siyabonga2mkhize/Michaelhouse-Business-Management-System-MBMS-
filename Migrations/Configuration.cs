using Michaelhouse.Models;
using System;
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
            // ======================================================================
            // ─── SEED ADMIN ACCOUNT ────────────────────────────────────────
            // ======================================================================
            if (!context.Users.Any(u => u.Email == "admin@michaelhouse.co.za"))
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

            // ──────────────────────────────────────────────────────────────
            // Ensure a Teacher exists before seeding trips
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
            // Seed trips, vehicles, etc.
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
            }
            catch { }

            // ──────────────────────────────────────────────────────────────
            // Seed InventoryManager and TransportManager
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

            if (!context.Users.Any(u => u.Role == "TransportManager"))
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
            // Seed Categories & Products (Store)
            // NOTE: Product.Price and Product.QuantityInStock are `double`.
            // ──────────────────────────────────────────────────────────────
            if (!context.Categories.Any())
            {
                var uniforms = new Category { Name = "Uniforms", Description = "Official school uniform items for all grades" };
                var stationery = new Category { Name = "Books & Stationery", Description = "Textbooks, exercise books and stationery" };
                context.Categories.AddOrUpdate(c => c.Name, uniforms, stationery);
                context.SaveChanges();
            }

            if (!context.Products.Any())
            {
                var uniformsCat = context.Categories.First(c => c.Name == "Uniforms");
                var stationeryCat = context.Categories.First(c => c.Name == "Books & Stationery");

                var products = new[]
                {
                    new Product { Name = "School Shirt (White) - Small",  CategoryId = uniformsCat.Id,   Price = 120.00, QuantityInStock = 50,  ReorderLevel = 10, Description = "Official white school shirt, small size.",   IsActive = true },
                    new Product { Name = "School Shirt (White) - Medium", CategoryId = uniformsCat.Id,   Price = 120.00, QuantityInStock = 80,  ReorderLevel = 15, Description = "Official white school shirt, medium size.",  IsActive = true },
                    new Product { Name = "School Shirt (White) - Large",  CategoryId = uniformsCat.Id,   Price = 120.00, QuantityInStock = 60,  ReorderLevel = 15, Description = "Official white school shirt, large size.",   IsActive = true },
                    new Product { Name = "School Trousers (Grey) - 28",   CategoryId = uniformsCat.Id,   Price = 180.00, QuantityInStock = 40,  ReorderLevel = 8,  Description = "Official grey school trousers, 28 inch waist.", IsActive = true },
                    new Product { Name = "School Trousers (Grey) - 30",   CategoryId = uniformsCat.Id,   Price = 180.00, QuantityInStock = 55,  ReorderLevel = 10, Description = "Official grey school trousers, 30 inch waist.", IsActive = true },
                    new Product { Name = "School Skirt - Size 10",        CategoryId = uniformsCat.Id,   Price = 160.00, QuantityInStock = 35,  ReorderLevel = 8,  Description = "Official school skirt, size 10.",            IsActive = true },
                    new Product { Name = "School Tie",                    CategoryId = uniformsCat.Id,   Price = 65.00,  QuantityInStock = 100, ReorderLevel = 20, Description = "Official school tie with house colours.",    IsActive = true },
                    new Product { Name = "School Blazer - Small",         CategoryId = uniformsCat.Id,   Price = 450.00, QuantityInStock = 25,  ReorderLevel = 5,  Description = "Official school blazer, small size.",        IsActive = true },
                    new Product { Name = "School Blazer - Medium",        CategoryId = uniformsCat.Id,   Price = 450.00, QuantityInStock = 30,  ReorderLevel = 5,  Description = "Official school blazer, medium size.",       IsActive = true },
                    new Product { Name = "School Sports Kit",             CategoryId = uniformsCat.Id,   Price = 280.00, QuantityInStock = 3,   ReorderLevel = 10, Description = "Official sports kit (shirt + shorts).",      IsActive = true },
                    new Product { Name = "Grade 8 Mathematics Textbook",  CategoryId = stationeryCat.Id, Price = 220.00, QuantityInStock = 30,  ReorderLevel = 5,  Description = "Approved Mathematics textbook for Grade 8.",  IsActive = true },
                    new Product { Name = "Grade 9 Mathematics Textbook",  CategoryId = stationeryCat.Id, Price = 235.00, QuantityInStock = 25,  ReorderLevel = 5,  Description = "Approved Mathematics textbook for Grade 9.",  IsActive = true },
                    new Product { Name = "Grade 10 Physical Science",     CategoryId = stationeryCat.Id, Price = 260.00, QuantityInStock = 20,  ReorderLevel = 5,  Description = "Approved Physical Science textbook for Grade 10.", IsActive = true },
                    new Product { Name = "English Literature Anthology",  CategoryId = stationeryCat.Id, Price = 195.00, QuantityInStock = 40,  ReorderLevel = 8,  Description = "Set works anthology for Grades 8-12.",        IsActive = true },
                    new Product { Name = "A4 Exercise Book (Pack of 10)", CategoryId = stationeryCat.Id, Price = 85.00,  QuantityInStock = 120, ReorderLevel = 25, Description = "Ruled A4 exercise books, 96 pages each.",      IsActive = true },
                    new Product { Name = "Geometry Set",                  CategoryId = stationeryCat.Id, Price = 55.00,  QuantityInStock = 80,  ReorderLevel = 15, Description = "Complete geometry set with compass, ruler and protractor.", IsActive = true },
                    new Product { Name = "Scientific Calculator",         CategoryId = stationeryCat.Id, Price = 320.00, QuantityInStock = 5,   ReorderLevel = 8,  Description = "Approved scientific calculator for Grades 10-12.", IsActive = true },
                    new Product { Name = "Coloured Pencils (24 pack)",    CategoryId = stationeryCat.Id, Price = 45.00,  QuantityInStock = 150, ReorderLevel = 30, Description = "24 assorted coloured pencils.",                IsActive = true }
                };

                context.Products.AddOrUpdate(p => p.Name, products);
                context.SaveChanges();
            }

            // ──────────────────────────────────────────────────────────────
            // Seed Store Suppliers and SupplierProducts
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

            uniformSupplier = context.Suppliers.First(s => s.Name == "KZN Uniform Manufacturers");
            stationerySupplier = context.Suppliers.First(s => s.Name == "EduBooks SA");

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
            // SHIFT PATTERNS
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
            // Seed Drivers
            // ──────────────────────────────────────────────────────────────
            if (!context.Users.Any(u => u.Role == "Driver"))
            {
                var driversData = new[]
                {
                    new { Name = "Sibusiso Dlamini", Email = "sibusiso", ID = "9001015009087" },
                    new { Name = "Thabo Mkhize",     Email = "thabo",    ID = "8805056009088" },
                    new { Name = "Andile Zulu",      Email = "andile",   ID = "9202027009089" },
                    new { Name = "Nkosi Khumalo",    Email = "nkosi",    ID = "8703038009090" }
                };

                foreach (var d in driversData)
                {
                    string schoolEmail = d.Email + "@michealhouse.com";

                    var user = new AppUser
                    {
                        Name = d.Name,
                        Email = schoolEmail,
                        PasswordHash = HashPassword("Driver@123"),
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

            // ======================================================================
            // CAFETERIA / SPORTS SEED
            // ======================================================================

            // 1. Reference data (meal slots, halls, allergens, dietary categories,
            //    sports, menu items, sports rules, settings) — delegated to seeder.
            try
            {
                Michaelhouse.Data.CafeteriaSeeder.Seed(context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CafeteriaSeeder failed: " + ex.Message);
            }

            // 2. Cafeteria Staff Users
            if (!context.Users.Any(u => u.Email == "dietitian@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Dr. Sarah Naidoo",
                    Email = "dietitian@michaelhouse.co.za",
                    PasswordHash = HashPassword("Diet@123"),
                    Role = "Dietitian"
                });
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "meals@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Mrs. Thandi Mkhize",
                    Email = "meals@michaelhouse.co.za",
                    PasswordHash = HashPassword("Meals@123"),
                    Role = "MealCoordinator"
                });
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "chef@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Chef Sipho Ndlovu",
                    Email = "chef@michaelhouse.co.za",
                    PasswordHash = HashPassword("Chef@123"),
                    Role = "Chef"
                });
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "cafeteria@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Mr. Bongani Zulu",
                    Email = "cafeteria@michaelhouse.co.za",
                    PasswordHash = HashPassword("Cafe@123"),
                    Role = "CafeteriaManager"
                });
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "sports@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Mr. David Pretorius",
                    Email = "sports@michaelhouse.co.za",
                    PasswordHash = HashPassword("Sports@123"),
                    Role = "SportsDirector"
                });
                context.SaveChanges();
            }

            // 3. Coach Users + Coach records
            if (!context.Users.Any(u => u.Email == "brown@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Mr. John Brown",
                    Email = "brown@michaelhouse.co.za",
                    PasswordHash = HashPassword("Coach@123"),
                    Role = "Coach"
                });
                context.SaveChanges();
            }
            if (!context.Coaches.Any(c => c.Email == "brown@michaelhouse.co.za"))
            {
                context.Coaches.Add(new Coach
                {
                    Name = "John",
                    Surname = "Brown",
                    SportName = "Rugby",
                    Team = "U16 Rugby",
                    Email = "brown@michaelhouse.co.za",
                    Phone = "082 123 4567",
                    IsActive = true
                });
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "white@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Ms. Sarah White",
                    Email = "white@michaelhouse.co.za",
                    PasswordHash = HashPassword("Coach@123"),
                    Role = "Coach"
                });
                context.SaveChanges();
            }
            if (!context.Coaches.Any(c => c.Email == "white@michaelhouse.co.za"))
            {
                context.Coaches.Add(new Coach
                {
                    Name = "Sarah",
                    Surname = "White",
                    SportName = "Hockey",
                    Team = "1st Hockey",
                    Email = "white@michaelhouse.co.za",
                    Phone = "082 987 6543",
                    IsActive = true
                });
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "green@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Mr. David Green",
                    Email = "green@michaelhouse.co.za",
                    PasswordHash = HashPassword("Coach@123"),
                    Role = "Coach"
                });
                context.SaveChanges();
            }
            if (!context.Coaches.Any(c => c.Email == "green@michaelhouse.co.za"))
            {
                context.Coaches.Add(new Coach
                {
                    Name = "David",
                    Surname = "Green",
                    SportName = "Cricket",
                    Team = "1st Cricket",
                    Email = "green@michaelhouse.co.za",
                    Phone = "082 555 1234",
                    IsActive = true
                });
                context.SaveChanges();
            }

            if (!context.Users.Any(u => u.Email == "black@michaelhouse.co.za"))
            {
                context.Users.Add(new AppUser
                {
                    Name = "Mrs. Jane Black",
                    Email = "black@michaelhouse.co.za",
                    PasswordHash = HashPassword("Coach@123"),
                    Role = "Coach"
                });
                context.SaveChanges();
            }
            if (!context.Coaches.Any(c => c.Email == "black@michaelhouse.co.za"))
            {
                context.Coaches.Add(new Coach
                {
                    Name = "Jane",
                    Surname = "Black",
                    SportName = "Swimming",
                    Team = "Swimming Squad",
                    Email = "black@michaelhouse.co.za",
                    Phone = "082 444 5678",
                    IsActive = true
                });
                context.SaveChanges();
            }

            // 4. Teams
            var brownCoach = context.Coaches.FirstOrDefault(c => c.Email == "brown@michaelhouse.co.za");
            var whiteCoach = context.Coaches.FirstOrDefault(c => c.Email == "white@michaelhouse.co.za");
            var greenCoach = context.Coaches.FirstOrDefault(c => c.Email == "green@michaelhouse.co.za");
            var blackCoach = context.Coaches.FirstOrDefault(c => c.Email == "black@michaelhouse.co.za");

            if (brownCoach != null && !context.Teams.Any(t => t.Name == "U16 Rugby"))
            {
                context.Teams.Add(new Team { Name = "U16 Rugby", SportName = "Rugby", CoachID = brownCoach.CoachID, Players = "", IsActive = true });
                context.SaveChanges();
            }
            if (whiteCoach != null && !context.Teams.Any(t => t.Name == "1st Hockey"))
            {
                context.Teams.Add(new Team { Name = "1st Hockey", SportName = "Hockey", CoachID = whiteCoach.CoachID, Players = "", IsActive = true });
                context.SaveChanges();
            }
            if (greenCoach != null && !context.Teams.Any(t => t.Name == "1st Cricket"))
            {
                context.Teams.Add(new Team { Name = "1st Cricket", SportName = "Cricket", CoachID = greenCoach.CoachID, Players = "", IsActive = true });
                context.SaveChanges();
            }
            if (blackCoach != null && !context.Teams.Any(t => t.Name == "Swimming Squad"))
            {
                context.Teams.Add(new Team { Name = "Swimming Squad", SportName = "Swimming", CoachID = blackCoach.CoachID, Players = "", IsActive = true });
                context.SaveChanges();
            }

            // 5. Recipes (NEW relational shape — name + yield only)
            if (!context.Recipes.Any())
            {
                var recipeNames = new[]
                {
                    "Chicken & Rice", "Beef Stew", "Fish & Chips", "Vegetarian Curry",
                    "Pasta & Meatballs", "Pizza", "Braai (BBQ)", "Roast Chicken",
                    "Chicken Schnitzel", "Lamb Curry", "Beef Burger & Chips", "Vegetable Stir-fry",
                    "Eggs & Toast", "Cereal & Milk", "Oats & Fruit", "Pancakes", "Omelette",
                    "Fruit Salad", "Ice Cream", "Malva Pudding", "Trifle",
                    "Cheese & Crackers", "Yogurt & Granola", "Protein Shake", "Fruit & Nuts"
                };

                foreach (var name in recipeNames)
                {
                    context.Recipes.Add(new Recipe
                    {
                        Name = name,
                        YieldUnit = "portions",
                        ServingYield = 1,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                }
                context.SaveChanges();
            }

            // 6. Cafeteria Suppliers
            if (!context.Suppliers.Any(s => s.Name == "FreshDirect Foods"))
            {
                context.Suppliers.Add(new Supplier
                {
                    Name = "FreshDirect Foods",
                    ContactPerson = "John Doe",
                    Email = "john@freshdirect.co.za",
                    Phone = "031 123 4567",
                    Address = "123 Food Street, Durban",
                    Products = "Chicken, Beef, Fish, Vegetables, Rice, Dairy",
                    IsActive = true
                });
                context.SaveChanges();
            }

            if (!context.Suppliers.Any(s => s.Name == "Global Meats"))
            {
                context.Suppliers.Add(new Supplier
                {
                    Name = "Global Meats",
                    ContactPerson = "Peter Smith",
                    Email = "peter@globalmeats.co.za",
                    Phone = "031 987 6543",
                    Address = "456 Meat Avenue, Durban",
                    Products = "Beef, Chicken, Lamb, Sausages",
                    IsActive = true
                });
                context.SaveChanges();
            }

            if (!context.Suppliers.Any(s => s.Name == "Fresh Veggies"))
            {
                context.Suppliers.Add(new Supplier
                {
                    Name = "Fresh Veggies",
                    ContactPerson = "Mary Jones",
                    Email = "mary@freshveggies.co.za",
                    Phone = "031 555 1234",
                    Address = "789 Veg Street, Durban",
                    Products = "Vegetables, Fruits, Herbs, Salads",
                    IsActive = true
                });
                context.SaveChanges();
            }

            // 7. Cafeteria Inventory (NEW shape)
            if (!context.InventoryItems.Any(i => i.Category == "Food" || i.Category == "Beverage" || i.Category == "Kitchen Supply"))
            {
                var invItems = new[]
                {
                    new { Name = "Chicken Breast",    Cat = "Food",           Unit = "kg",     Stock = 100m, Reorder = 30m,  Cost = 85.00m  },
                    new { Name = "Beef (Stewing)",    Cat = "Food",           Unit = "kg",     Stock = 60m,  Reorder = 20m,  Cost = 120.00m },
                    new { Name = "Fish Fillets",      Cat = "Food",           Unit = "kg",     Stock = 30m,  Reorder = 15m,  Cost = 95.00m  },
                    new { Name = "Lamb",              Cat = "Food",           Unit = "kg",     Stock = 25m,  Reorder = 10m,  Cost = 140.00m },
                    new { Name = "Sausages",          Cat = "Food",           Unit = "kg",     Stock = 40m,  Reorder = 15m,  Cost = 70.00m  },
                    new { Name = "White Rice",        Cat = "Food",           Unit = "kg",     Stock = 50m,  Reorder = 60m,  Cost = 25.00m  },
                    new { Name = "Brown Rice",        Cat = "Food",           Unit = "kg",     Stock = 30m,  Reorder = 15m,  Cost = 32.00m  },
                    new { Name = "Pasta",             Cat = "Food",           Unit = "kg",     Stock = 45m,  Reorder = 20m,  Cost = 28.00m  },
                    new { Name = "Potatoes",          Cat = "Food",           Unit = "kg",     Stock = 80m,  Reorder = 25m,  Cost = 18.00m  },
                    new { Name = "Bread",             Cat = "Food",           Unit = "loaves", Stock = 30m,  Reorder = 10m,  Cost = 15.00m  },
                    new { Name = "Mixed Vegetables",  Cat = "Food",           Unit = "kg",     Stock = 40m,  Reorder = 20m,  Cost = 22.00m  },
                    new { Name = "Carrots",           Cat = "Food",           Unit = "kg",     Stock = 35m,  Reorder = 15m,  Cost = 14.00m  },
                    new { Name = "Onions",            Cat = "Food",           Unit = "kg",     Stock = 50m,  Reorder = 20m,  Cost = 12.00m  },
                    new { Name = "Fruit (Mixed)",     Cat = "Food",           Unit = "kg",     Stock = 45m,  Reorder = 20m,  Cost = 30.00m  },
                    new { Name = "Bananas",           Cat = "Food",           Unit = "kg",     Stock = 40m,  Reorder = 15m,  Cost = 16.00m  },
                    new { Name = "Milk",              Cat = "Food",           Unit = "L",      Stock = 100m, Reorder = 30m,  Cost = 12.00m  },
                    new { Name = "Cheese",            Cat = "Food",           Unit = "kg",     Stock = 40m,  Reorder = 15m,  Cost = 85.00m  },
                    new { Name = "Yogurt",            Cat = "Food",           Unit = "L",      Stock = 50m,  Reorder = 20m,  Cost = 22.00m  },
                    new { Name = "Eggs",              Cat = "Food",           Unit = "pieces", Stock = 200m, Reorder = 60m,  Cost = 3.50m   },
                    new { Name = "Flour",             Cat = "Food",           Unit = "kg",     Stock = 50m,  Reorder = 15m,  Cost = 20.00m  },
                    new { Name = "Sugar",             Cat = "Food",           Unit = "kg",     Stock = 30m,  Reorder = 10m,  Cost = 22.00m  },
                    new { Name = "Cereal",            Cat = "Food",           Unit = "kg",     Stock = 40m,  Reorder = 15m,  Cost = 45.00m  },
                    new { Name = "Oats",              Cat = "Food",           Unit = "kg",     Stock = 35m,  Reorder = 12m,  Cost = 30.00m  },
                    new { Name = "Cooking Oil",       Cat = "Food",           Unit = "L",      Stock = 25m,  Reorder = 10m,  Cost = 55.00m  },
                    new { Name = "Tomato Sauce",      Cat = "Food",           Unit = "L",      Stock = 20m,  Reorder = 8m,   Cost = 35.00m  },
                    new { Name = "Spices (Mixed)",    Cat = "Food",           Unit = "kg",     Stock = 15m,  Reorder = 5m,   Cost = 65.00m  },
                    new { Name = "Fruit Juice",       Cat = "Beverage",       Unit = "L",      Stock = 60m,  Reorder = 20m,  Cost = 25.00m  },
                    new { Name = "Sports Drinks",     Cat = "Beverage",       Unit = "L",      Stock = 80m,  Reorder = 30m,  Cost = 18.00m  },
                    new { Name = "Aluminium Foil",    Cat = "Kitchen Supply", Unit = "rolls",  Stock = 20m,  Reorder = 5m,   Cost = 45.00m  },
                    new { Name = "Cling Wrap",        Cat = "Kitchen Supply", Unit = "rolls",  Stock = 20m,  Reorder = 5m,   Cost = 38.00m  },
                    new { Name = "Disposable Gloves", Cat = "Kitchen Supply", Unit = "boxes",  Stock = 10m,  Reorder = 3m,   Cost = 120.00m },
                    new { Name = "Paper Towels",      Cat = "Kitchen Supply", Unit = "packs",  Stock = 25m,  Reorder = 8m,   Cost = 55.00m  }
                };

                foreach (var i in invItems)
                {
                    context.InventoryItems.Add(new InventoryItem
                    {
                        Name = i.Name,
                        Category = i.Cat,
                        Unit = i.Unit,
                        CurrentStock = i.Stock,
                        ReservedStock = 0m,
                        ReorderLevel = i.Reorder,
                        UnitCost = i.Cost,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                }
                context.SaveChanges();
            }

            // 8. Link Coaches and Teams to Sport entities
            var allSports = context.Sports.ToList();

            foreach (var team in context.Teams.ToList())
            {
                if (team.SportId == null && !string.IsNullOrEmpty(team.SportName))
                {
                    var s = allSports.FirstOrDefault(x => x.Name.Equals(team.SportName, StringComparison.OrdinalIgnoreCase));
                    if (s != null) team.SportId = s.SportId;
                }
            }

            foreach (var coach in context.Coaches.ToList())
            {
                if (coach.SportId == null && !string.IsNullOrEmpty(coach.SportName))
                {
                    var s = allSports.FirstOrDefault(x => x.Name.Equals(coach.SportName, StringComparison.OrdinalIgnoreCase));
                    if (s != null) coach.SportId = s.SportId;
                }
            }

            context.SaveChanges();
        }

        // ══════════════════════════════════════════════════════════════════
        // Helper
        // ══════════════════════════════════════════════════════════════════
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