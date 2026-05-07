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

            }
            // ── Categories ────────────────────────────────────────────────────────
            var uniformsCat = new Category
            {
                Name = "Uniforms",
                Description = "Official Michaelhouse school uniforms and sports wear"
            };
            var stationeryCat = new Category
            {
                Name = "Books & Stationery",
                Description = "Textbooks, exercise books and school stationery"
            };

            context.Categories.AddOrUpdate(c => c.Name, uniformsCat, stationeryCat);
            context.SaveChanges();

            // Re-fetch to get IDs
            uniformsCat = context.Categories.First(c => c.Name == "Uniforms");
            stationeryCat = context.Categories.First(c => c.Name == "Books & Stationery");

            // ── Products ──────────────────────────────────────────────────────────
            var products = new[]
            {
                // Uniforms
                new Product { Name = "School Shirt (White) - Small",   CategoryId = uniformsCat.Id,   Price = 120.00, QuantityInStock = 50,  ReorderLevel = 10, Description = "Official white school shirt, small size.",             IsActive = true },
                new Product { Name = "School Shirt (White) - Medium",  CategoryId = uniformsCat.Id,   Price = 120.00, QuantityInStock = 80,  ReorderLevel = 15, Description = "Official white school shirt, medium size.",            IsActive = true },
                new Product { Name = "School Shirt (White) - Large",   CategoryId = uniformsCat.Id,   Price = 120.00, QuantityInStock = 60,  ReorderLevel = 15, Description = "Official white school shirt, large size.",             IsActive = true },
                new Product { Name = "School Trousers (Grey) - 28",   CategoryId = uniformsCat.Id,   Price = 180.00, QuantityInStock = 40,  ReorderLevel = 8,  Description = "Official grey school trousers, 28 inch waist.",        IsActive = true },
                new Product { Name = "School Trousers (Grey) - 30",   CategoryId = uniformsCat.Id,   Price = 180.00, QuantityInStock = 55,  ReorderLevel = 10, Description = "Official grey school trousers, 30 inch waist.",        IsActive = true },
                new Product { Name = "School Skirt - Size 10",         CategoryId = uniformsCat.Id,   Price = 160.00, QuantityInStock = 35,  ReorderLevel = 8,  Description = "Official school skirt, size 10.",                     IsActive = true },
                new Product { Name = "School Tie",                     CategoryId = uniformsCat.Id,   Price = 65.00,  QuantityInStock = 100, ReorderLevel = 20, Description = "Official school tie with house colours.",              IsActive = true },
                new Product { Name = "School Blazer - Small",          CategoryId = uniformsCat.Id,   Price = 450.00, QuantityInStock = 25,  ReorderLevel = 5,  Description = "Official school blazer, small size.",                 IsActive = true },
                new Product { Name = "School Blazer - Medium",         CategoryId = uniformsCat.Id,   Price = 450.00, QuantityInStock = 30,  ReorderLevel = 5,  Description = "Official school blazer, medium size.",                IsActive = true },
                new Product { Name = "School Sports Kit",              CategoryId = uniformsCat.Id,   Price = 280.00, QuantityInStock = 3,   ReorderLevel = 10, Description = "Official sports kit (shirt + shorts).",               IsActive = true },
 
                // Books & Stationery
                new Product { Name = "Grade 8 Mathematics Textbook",   CategoryId = stationeryCat.Id, Price = 220.00, QuantityInStock = 30,  ReorderLevel = 5,  Description = "Approved Mathematics textbook for Grade 8.",          IsActive = true },
                new Product { Name = "Grade 9 Mathematics Textbook",   CategoryId = stationeryCat.Id, Price = 235.00, QuantityInStock = 25,  ReorderLevel = 5,  Description = "Approved Mathematics textbook for Grade 9.",          IsActive = true },
                new Product { Name = "Grade 10 Physical Science",      CategoryId = stationeryCat.Id, Price = 260.00, QuantityInStock = 20,  ReorderLevel = 5,  Description = "Approved Physical Science textbook for Grade 10.",    IsActive = true },
                new Product { Name = "English Literature Anthology",   CategoryId = stationeryCat.Id, Price = 195.00, QuantityInStock = 40,  ReorderLevel = 8,  Description = "Set works anthology for Grades 8-12.",                IsActive = true },
                new Product { Name = "A4 Exercise Book (Pack of 10)",  CategoryId = stationeryCat.Id, Price = 85.00,  QuantityInStock = 120, ReorderLevel = 25, Description = "Ruled A4 exercise books, 96 pages each.",             IsActive = true },
                new Product { Name = "Geometry Set",                   CategoryId = stationeryCat.Id, Price = 55.00,  QuantityInStock = 80,  ReorderLevel = 15, Description = "Complete geometry set with compass, ruler and protractor.", IsActive = true },
                new Product { Name = "Scientific Calculator",          CategoryId = stationeryCat.Id, Price = 320.00, QuantityInStock = 5,   ReorderLevel = 8,  Description = "Approved scientific calculator for Grades 10-12.",    IsActive = true },
                new Product { Name = "Coloured Pencils (24 pack)",     CategoryId = stationeryCat.Id, Price = 45.00,  QuantityInStock = 150, ReorderLevel = 30, Description = "24 assorted coloured pencils.",                       IsActive = true }
            };

            foreach (var p in products)
                context.Products.AddOrUpdate(pr => pr.Name, p);

            context.SaveChanges();

            // ── Record initial stock movements for seeded products ────────────────
            foreach (var p in products)
            {
                var product = context.Products.First(pr => pr.Name == p.Name);

                bool hasMovement = context.StockMovements.Any(sm =>
                    sm.ProductId == product.Id &&
                    sm.Reference == "SEED");

                if (!hasMovement)
                {
                    context.StockMovements.Add(new StockMovement
                    {
                        ProductId = product.Id,
                        MovementType = StockMovementType.Adjustment,
                        Quantity = product.QuantityInStock,
                        StockAfter = product.QuantityInStock,
                        Reference = "SEED",
                        Notes = "Initial stock — system setup",
                        CreatedAt = System.DateTime.Now
                    });
                }

            }
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

            // ── Link Seeded Products to Suppliers (SupplierProducts) ──────────────

            // Grab a few products to link
            var smallShirt = context.Products.First(p => p.Name == "School Shirt (White) - Small");
            var greyTrousers = context.Products.First(p => p.Name == "School Trousers (Grey) - 28");
            var mathTextbook = context.Products.First(p => p.Name == "Grade 8 Mathematics Textbook");
            var pencils = context.Products.First(p => p.Name == "Coloured Pencils (24 pack)");

            var supplierLinks = new[]
            {
                new SupplierProduct {
                    SupplierId = uniformSupplier.SupplierId,
                    ProductId = smallShirt.Id,
                    UnitCost = 85.00m, // Supplier wholesale price
                    SupplierSku = "SH-WHT-S",
                    MinOrderQty = 10,
                    LeadTimeDays = 7,
                    IsPreferred = true
                },
                new SupplierProduct {
                    SupplierId = uniformSupplier.SupplierId,
                    ProductId = greyTrousers.Id,
                    UnitCost = 130.00m,
                    SupplierSku = "TR-GRY-28",
                    MinOrderQty = 5,
                    LeadTimeDays = 7,
                    IsPreferred = true
                },
                new SupplierProduct {
                    SupplierId = stationerySupplier.SupplierId,
                    ProductId = mathTextbook.Id,
                    UnitCost = 180.00m,
                    SupplierSku = "MATH-GR8-01",
                    MinOrderQty = 20,
                    LeadTimeDays = 14,
                    IsPreferred = true
                },
                new SupplierProduct {
                    SupplierId = stationerySupplier.SupplierId,
                    ProductId = pencils.Id,
                    UnitCost = 25.00m,
                    SupplierSku = "PENC-24",
                    MinOrderQty = 50,
                    LeadTimeDays = 3,
                    IsPreferred = true
                }
            };

            foreach (var link in supplierLinks)
            {
                // Ensure we don't create duplicate links if Update-Database is run multiple times
                if (!context.SupplierProducts.Any(sp => sp.SupplierId == link.SupplierId && sp.ProductId == link.ProductId))
                {
                    context.SupplierProducts.Add(link);
                }
            }
            

            context.SaveChanges();
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