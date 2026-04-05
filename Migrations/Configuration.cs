namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<Michaelhouse.Models.DBContextClass>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
        }

        protected override void Seed(Michaelhouse.Models.DBContextClass context)
        {
            // Seed default admin account
            // Login: admin@michaelhouse.co.za / Admin@123
            //if (!context.Users.Any(u => u.Role == "Admin"))
            //{
            //    context.Users.Add(new Michaelhouse.Models.AppUser
            //    {
            //        Name = "System Admin",
            //        Email = "admin@michaelhouse.co.za",
            //        PasswordHash = Michaelhouse.Controllers.AccountController.HashPassword("Admin@123"),
            //        Role = "Admin"
            //    });
            //    context.SaveChanges();
            //}

        }
    }
}
