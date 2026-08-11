using Michaelhouse.Models;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Michaelhouse
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            try
            {
                var stripeKey = System.Configuration.ConfigurationManager.AppSettings["StripeApiKey"];
                if (!string.IsNullOrWhiteSpace(stripeKey))
                {
                    StripeConfiguration.ApiKey = stripeKey;
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning("StripeApiKey not found in AppSettings; Stripe calls will fail until configured.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Stripe initialization failed: " + ex);
            }

            // 🌱 SEED THE DATABASE ONCE WHEN THE APP STARTS
            SeedDrivers();
            SeedMichaelhouseSystem();
        }

        // --- YOUR EXISTING DRIVER SEEDING ---
        private void SeedDrivers()
        {
            using (var db = new DBContextClass())
            {
                // Prevent duplicate seeding
                if (db.Drivers.Any())
                    return;

                var drivers = new List<Driver>
                {
                    new Driver
                    {
                        FullName = "Sibusiso Mthembu",
                        IDNumber = "9001015009087",
                        PhoneNumber = "0721111111",
                        Email = "sibusiso@michaelhouse.com",
                        LicenceNumber = "LIC1001",
                        LicenceExpiryDate = DateTime.Now.AddYears(3),
                        HasPDP = true,
                        IsActive = true,
                        DateCreated = DateTime.Now,
                        PasswordHash = "123456"
                    },
                    new Driver
                    {
                        FullName = "Thabo Khumalo",
                        IDNumber = "9202025009088",
                        PhoneNumber = "0722222222",
                        Email = "thabo@michaelhouse.com",
                        LicenceNumber = "LIC1002",
                        LicenceExpiryDate = DateTime.Now.AddYears(2),
                        HasPDP = true,
                        IsActive = true,
                        DateCreated = DateTime.Now,
                        PasswordHash = "123456"
                    },
                    new Driver
                    {
                        FullName = "Mandla Dlamini",
                        IDNumber = "9303035009089",
                        PhoneNumber = "0723333333",
                        Email = "mandla@michaelhouse.com",
                        LicenceNumber = "LIC1003",
                        LicenceExpiryDate = DateTime.Now.AddYears(4),
                        HasPDP = true,
                        IsActive = true,
                        DateCreated = DateTime.Now,
                        PasswordHash = "123456"
                    },
                    new Driver
                    {
                        FullName = "Sipho Zulu",
                        IDNumber = "9404045009090",
                        PhoneNumber = "0724444444",
                        Email = "sipho@michaelhouse.com",
                        LicenceNumber = "LIC1004",
                        LicenceExpiryDate = DateTime.Now.AddYears(1),
                        HasPDP = true,
                        IsActive = true,
                        DateCreated = DateTime.Now,
                        PasswordHash = "123456"
                    }
                };

                db.Drivers.AddRange(drivers);
                db.SaveChanges();
            }
        }

        // --- 🏫 SIMPLIFIED MICHAELHOUSE SEEDING (With Existing Student Lookup) ---
        private void SeedMichaelhouseSystem()
        {
            using (var db = new DBContextClass())
            {
                try
                {
                    // --- LOOK UP ANY EXISTING STUDENT (No email guessing required) ---
                    // This finds the first student in your database, regardless of email.
                    var existingStudent = db.Students.FirstOrDefault();

                    if (existingStudent == null)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: No existing students found in the database. Please register at least one student manually first.");
                        return; // Stop seeding silently to avoid crashing
                    }

                    int studentId = existingStudent.StudentId;

                    // --- 1. SEED RESIDENCE ---
                    var allResidences = db.Residences.ToList();
                    var residence = allResidences.FirstOrDefault(r => r.ResidenceName == "Founders House");

                    if (residence == null)
                    {
                        residence = new Residence
                        {
                            ResidenceName = "Founders House"
                        };
                        db.Residences.Add(residence);
                        db.SaveChanges();
                    }

                    // --- 2. SEED ROOM ---
                    var room = db.Rooms.FirstOrDefault(r => r.RoomNumber == "F1" && r.ResidenceId == residence.ResidenceId);
                    if (room == null)
                    {
                        room = new Room
                        {
                            ResidenceId = residence.ResidenceId,
                            RoomNumber = "F1",
                            Capacity = 2
                        };
                        db.Rooms.Add(room);
                        db.SaveChanges();
                    }

                    // --- 3. SEED BED ---
                    var bed = db.Beds.FirstOrDefault(b => b.BedNumber == "F1-A" && b.RoomId == room.RoomId);
                    if (bed == null)
                    {
                        bed = new Bed
                        {
                            RoomId = room.RoomId,
                            BedNumber = "F1-A",
                            IsOccupied = false
                        };
                        db.Beds.Add(bed);
                        db.SaveChanges();
                    }

                    // --- 4. ASSIGN EXISTING STUDENT TO BED using ResidenceAllocation ---
                    var allocation = db.ResidenceAllocations
                        .FirstOrDefault(a => a.StudentId == studentId && a.IsActive == true);

                    if (allocation == null)
                    {
                        allocation = new ResidenceAllocation
                        {
                            StudentId = studentId,
                            ResidenceId = residence.ResidenceId,
                            RoomId = room.RoomId,
                            BedId = bed.BedId,
                            IsActive = true,
                            AllocatedAt = DateTime.Now,
                            CreatedBy = "Seeder"
                        };
                        db.ResidenceAllocations.Add(allocation);
                        db.SaveChanges();
                    }

                    // --- 5. SEED TERM CALENDAR ---
                    if (!db.TermCalendars.Any(c => c.EventTitle == "Michaelhouse Test Closed Weekend"))
                    {
                        db.TermCalendars.Add(new TermCalendar
                        {
                            EventTitle = "Michaelhouse Test Closed Weekend",
                            StartDate = new DateTime(2026, 8, 22),
                            EndDate = new DateTime(2026, 8, 23),
                            IsClosedWeekend = true
                        });

                        db.TermCalendars.Add(new TermCalendar
                        {
                            EventTitle = "Michaelhouse Demo Closed Weekend (Sept)",
                            StartDate = new DateTime(2026, 9, 12),
                            EndDate = new DateTime(2026, 9, 13),
                            IsClosedWeekend = true
                        });

                        db.SaveChanges();
                    }
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException ex)
                {
                    // Catch validation errors and print them to the Output window
                    string errorMessage = "Validation errors:\n";
                    foreach (var validationErrors in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            errorMessage += $"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}\n";
                        }
                    }
                    System.Diagnostics.Debug.WriteLine(errorMessage);
                    throw new Exception(errorMessage);
                }
            }
        }
    }
}