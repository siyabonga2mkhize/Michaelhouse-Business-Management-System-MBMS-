using System;
using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Models;

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
