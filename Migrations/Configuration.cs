namespace Michaelhouse.Migrations
{
    using Michaelhouse.Models;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<Michaelhouse.Models.DBContextClass>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        
       
        protected override void Seed(DBContextClass context)
        {
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

                    // 1. Create User (Login)
                    var user = new AppUser
                    {
                        Name = d.Name,
                        Email = schoolEmail,
                        PasswordHash = HashPassword(password),
                        Role = "Driver"
                    };

                    context.Users.Add(user);
                    context.SaveChanges(); // needed to get UserId

                    // 2. Create Driver (Profile)
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
