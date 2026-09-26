using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Security;

namespace Michaelhouse.Controllers
{
    public class AccountController : BaseController
    {

        // ─── Login ────────────────────────────────────────────────────────────────

        public ActionResult Login()
        {
            if (Session["UserId"] != null)
            {
                // Ensure role-specific session keys are populated to avoid redirect loops.
                string role = Session["UserRole"]?.ToString();
                if (role == "Teacher" && Session["TeacherId"] == null)
                {
                    try
                    {
                        using (var db = new DBContextClass())
                        {
                            int userId = (int)(Session["UserId"] ?? 0);
                            if (userId > 0)
                            {
                                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == userId);
                                if (teacher == null)
                                {
                                    // create minimal teacher record so dashboard can resolve
                                    var user = db.Users.FirstOrDefault(u => u.UserId == userId);
                                    var names = (user?.Name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    var first = names.Length > 0 ? names[0] : user?.Name ?? "";
                                    var last = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "";
                                    teacher = new Teacher
                                    {
                                        FirstName = first,
                                        LastName = last,
                                        Email = user?.Email ?? "",
                                        HireDate = DateTime.Now,
                                        UserId = userId
                                    };
                                    db.Teachers.Add(teacher);
                                    db.SaveChanges();
                                }

                                Session["TeacherId"] = teacher.TeacherId;
                            }
                        }
                    }
                    catch { }
                }
                else if ((role == "HouseMaster" || role == "Housemaster") && Session["HouseMasterId"] == null)
                {
                    try
                    {
                        using (var db = new DBContextClass())
                        {
                            int userId = (int)(Session["UserId"] ?? 0);
                            var user = db.Users.FirstOrDefault(u => u.UserId == userId);
                            if (user != null)
                            {
                                var houseMaster = db.HouseMasters.FirstOrDefault(h =>
                                    h.ContactEmail == user.Email || h.FullName == user.Name);
                                if (houseMaster != null)
                                {
                                    Session["HouseMasterId"] = houseMaster.HouseMasterId;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // ── Maintenance Management: ensure MaintenanceStaffId is populated ──
                if (role == "MaintenanceWorker" && Session["MaintenanceStaffId"] == null)
                {
                    try
                    {
                        using (var db = new DBContextClass())
                        {
                            int userId = (int)(Session["UserId"] ?? 0);
                            if (userId > 0)
                            {
                                var staff = db.MaintenanceStaff.FirstOrDefault(s => s.UserId == userId);
                                if (staff != null)
                                {
                                    Session["MaintenanceStaffId"] = staff.Id;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // ── Coach: ensure CoachId is populated ──
                if (role == "Coach" && Session["CoachId"] == null)
                {
                    try
                    {
                        using (var db = new DBContextClass())
                        {
                            int userId = (int)(Session["UserId"] ?? 0);
                            var user = db.Users.FirstOrDefault(u => u.UserId == userId);
                            if (user != null)
                            {
                                var coach = db.Coaches.FirstOrDefault(c => c.Email == user.Email);
                                if (coach != null)
                                {
                                    Session["CoachId"] = coach.CoachID;
                                }
                            }
                        }
                    }
                    catch { }
                }

                return RedirectByRole(role);
            }

            // Pass an empty model to the view
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            using (var db = new DBContextClass())
            {
                var hash = HashPassword(vm.Password);

                // 1. Async user lookup
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email && u.PasswordHash == hash);

                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(vm);
                }

                // 2. Set session (synchronous, but fast)
                Session["UserId"] = user.UserId;
                Session["UserName"] = user.Name;
                Session["UserRole"] = user.Role;
                FormsAuthentication.SetAuthCookie(user.Email, false);

                // 3. Role-specific logic (with async queries)
                if (user.Role == "Parent")
                {
                    var parent = await db.Parents.FirstOrDefaultAsync(p => p.UserId == user.UserId);
                    if (parent != null) Session["ParentId"] = parent.ParentId;
                }
                else if (user.Role == "Student")
                {
                    var student = await db.Students.FirstOrDefaultAsync(s => s.UserId == user.UserId);
                    if (student == null)
                    {
                        // create minimal student
                        var names = (user.Name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var first = names.Length > 0 ? names[0] : user.Name ?? "Student";
                        var last = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "";
                        student = new Student
                        {
                            FirstName = first,
                            LastName = last,
                            UserId = user.UserId,
                            ParentId = 2, // adjust if needed
                            IsActive = true,
                            IsBoarding = true,
                            GradeLevel = 8
                        };
                        db.Students.Add(student);
                        await db.SaveChangesAsync();
                    }
                    Session["StudentId"] = student.StudentId;
                }
                else if (user.Role == "Teacher")
                {
                    var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.UserId == user.UserId);
                    if (teacher == null)
                    {
                        var names = (user.Name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var first = names.Length > 0 ? names[0] : user.Name ?? "";
                        var last = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "";
                        teacher = new Teacher
                        {
                            FirstName = first,
                            LastName = last,
                            Email = user.Email,
                            HireDate = DateTime.Now,
                            UserId = user.UserId
                        };
                        db.Teachers.Add(teacher);
                        await db.SaveChangesAsync();
                    }
                    Session["TeacherId"] = teacher.TeacherId;
                }
                else if (user.Role == "Driver")
                {
                    var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == user.UserId);
                    if (driver == null)
                    {
                        driver = new Driver
                        {
                            FullName = user.Name,
                            Email = user.Email,
                            UserId = user.UserId,
                            IsActive = true,
                            DateCreated = DateTime.Now
                        };
                        db.Drivers.Add(driver);
                        await db.SaveChangesAsync();
                    }
                    Session["DriverId"] = driver.Id;
                }
                else if (user.Role == "HouseMaster" || user.Role == "Housemaster")
                {
                    var houseMaster = await db.HouseMasters.FirstOrDefaultAsync(h =>
                        h.ContactEmail == user.Email || h.FullName == user.Name);
                    if (houseMaster != null) Session["HouseMasterId"] = houseMaster.HouseMasterId;
                }
                else if (user.Role == "MaintenanceWorker")
                {
                    var staff = await db.MaintenanceStaff.FirstOrDefaultAsync(s => s.UserId == user.UserId);
                    if (staff != null) Session["MaintenanceStaffId"] = staff.Id;
                }
                // ─── Coach role (NEW) ──────────────────────────────────────────
                else if (user.Role == "Coach")
                {
                    var coach = await db.Coaches.FirstOrDefaultAsync(c => c.Email == user.Email);
                    if (coach != null) Session["CoachId"] = coach.CoachID;
                }

                // 4. Emergency alert check (only for Students)
                if (user.Role == "Student")
                {
                    var student = await db.Students.FirstOrDefaultAsync(s => s.UserId == user.UserId);
                    if (student != null)
                    {
                        var activeAlert = await db.EmergencyAlerts
                            .Where(a => a.Status == AlertStatus.Active)
                            .OrderByDescending(a => a.AlertTime)
                            .FirstOrDefaultAsync();
                        if (activeAlert != null)
                        {
                            bool alreadyConfirmed = await db.StudentSafetyConfirmations
                                .AnyAsync(c => c.AlertId == activeAlert.AlertId && c.StudentId == student.StudentId);
                            if (!alreadyConfirmed)
                            {
                                return RedirectToAction("ConfirmSafety", "Emergency");
                            }
                        }
                    }
                }

                return RedirectByRole(user.Role);
            }
        }

        // ─── Register ─────────────────────────────────────────────────────────────

        public ActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            using (var db = new DBContextClass())
            {
                if (db.Users.Any(u => u.Email == vm.Email))
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(vm);
                }

                var user = new AppUser
                {
                    Name = vm.Name,
                    Email = vm.Email,
                    PasswordHash = HashPassword(vm.Password),
                    Role = "Parent" // Default registration role
                };
                db.Users.Add(user);
                db.SaveChanges();

                var parent = new Parent
                {
                    Name = vm.Name,
                    Contact = vm.Email,
                    CellPhone = vm.CellPhone,
                    Relationship = vm.Relationship,
                    UserId = user.UserId
                };
                db.Parents.Add(parent);
                db.SaveChanges();

                TempData["Success"] = "Account created successfully! Please log in.";
                return RedirectToAction("Login");
            }
        }

        // ─── Logout ───────────────────────────────────────────────────────────────

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();

            Session.Clear();
            Session.Abandon();

            TempData["Success"] = "You have been logged out.";
            return RedirectToAction("Login");
        }
        // ─── Edit Profile ─────────────────────────────────────────────────────────

        [RequireLogin]
        public new ActionResult Profile()
        {
            using (var db = new DBContextClass())
            {
                int userId = (int)Session["UserId"];
                var parent = db.Parents.FirstOrDefault(p => p.UserId == userId);

                if (parent == null) return HttpNotFound();

                var vm = new ParentProfileViewModel
                {
                    Name = parent.Name,
                    Contact = parent.Contact,
                    CellPhone = parent.CellPhone,
                    WorkPhone = parent.WorkPhone,
                    HomePhone = parent.HomePhone,
                    PhysicalAddress = parent.PhysicalAddress,
                    PostalAddress = parent.PostalAddress,
                    Relationship = parent.Relationship,
                    Occupation = parent.Occupation,
                    Employer = parent.Employer,
                    EmergencyContactName = parent.EmergencyContactName,
                    EmergencyContactPhone = parent.EmergencyContactPhone
                };

                return View(vm);
            }
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public new ActionResult Profile(ParentProfileViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            using (var db = new DBContextClass())
            {
                int userId = (int)Session["UserId"];
                var parent = db.Parents.FirstOrDefault(p => p.UserId == userId);

                if (parent == null) return HttpNotFound();

                parent.Name = vm.Name;
                parent.Contact = vm.Contact;
                parent.CellPhone = vm.CellPhone;
                parent.WorkPhone = vm.WorkPhone;
                parent.HomePhone = vm.HomePhone;
                parent.PhysicalAddress = vm.PhysicalAddress;
                parent.PostalAddress = vm.PostalAddress;
                parent.Relationship = vm.Relationship;
                parent.Occupation = vm.Occupation;
                parent.Employer = vm.Employer;
                parent.EmergencyContactName = vm.EmergencyContactName;
                parent.EmergencyContactPhone = vm.EmergencyContactPhone;

                Session["UserName"] = vm.Name;
                db.SaveChanges();

                TempData["Success"] = "Profile updated successfully.";
                return RedirectToAction("Profile");
            }
        }

        // ─── Seed Admin ───────────────────────────────────────────────────────────

        public ActionResult SeedAdmin()
        {
            using (var db = new DBContextClass())
            {
                if (db.Users.Any(u => u.Role == "Admin"))
                {
                    TempData["Info"] = "Admin account already exists.";
                    return RedirectToAction("Login");
                }

                db.Users.Add(new AppUser
                {
                    Name = "System Admin",
                    Email = "admin@michaelhouse.co.za",
                    PasswordHash = HashPassword("Admin@123"),
                    Role = "Admin"
                });
                db.SaveChanges();

                TempData["Success"] = "Admin created. Email: admin@michaelhouse.co.za | Password: Admin@123";
                return RedirectToAction("Login");
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private ActionResult RedirectByRole(string role)
        {
            switch (role)
            {
                case "Admin":
                    return RedirectToAction("Dashboard", "Admin");
                case "Parent":
                    return RedirectToAction("Dashboard", "Parents");
                case "Student":
                    using (var db = new DBContextClass())
                    {
                        int userId = (int)Session["UserId"];
                        var student = db.Students.FirstOrDefault(s => s.UserId == userId);
                        if (student == null)
                        {
                            var user = db.Users.Find(userId);
                            var names = (user?.Name ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            var first = names.Length > 0 ? names[0] : user?.Name ?? "Student";
                            var last = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "";
                            student = new Student
                            {
                                FirstName = first,
                                LastName = last,
                                UserId = userId,
                                ParentId = 2,
                                IsActive = true,
                                IsBoarding = true,
                                GradeLevel = 8
                            };
                            db.Students.Add(student);
                            db.SaveChanges();
                            Session["StudentId"] = student.StudentId;
                        }

                        var activeAlert = db.EmergencyAlerts
                            .Where(a => a.Status == AlertStatus.Active)
                            .OrderByDescending(a => a.AlertTime)
                            .FirstOrDefault();

                        if (activeAlert != null)
                        {
                            bool alreadyConfirmed = db.StudentSafetyConfirmations
                                .Any(c => c.AlertId == activeAlert.AlertId && c.StudentId == student.StudentId);
                            if (!alreadyConfirmed)
                                return RedirectToAction("ConfirmSafety", "Emergency");
                        }
                    }
                    return RedirectToAction("Dashboard", "Students");
                case "Teacher":
                    return RedirectToAction("Index", "TeacherDashboard");
                case "TransportManager":
                    return RedirectToAction("Dashboard", "Transport");
                case "Driver":
                    return RedirectToAction("Index", "Driver");
                case "HouseMaster":
                case "Housemaster":
                    return RedirectToAction("Dashboard", "HouseMaster");
                // ── Maintenance Management roles ──
                case "MaintenanceManager":
                    return RedirectToAction("Index", "Maintenance");
                case "MaintenanceWorker":
                    return RedirectToAction("MyJobs", "Worker");
                case "MaintenanceReporter":   // backwards compatibility
                case "FaultReporter":         // matches your seed
                    return RedirectToAction("ReportFault", "Reporter");
                case "Transport Manager":     // (if you need this variant)
                    return RedirectToAction("Dashboard", "Transport");

                // ── Cafeteria & Sports Roles (NEW) ──
                case "Dietitian":
                    return RedirectToAction("MealPlans", "Cafeteria");
                case "MealCoordinator":
                    return RedirectToAction("Index", "Cafeteria");
                case "Chef":
                    return RedirectToAction("KitchenDashboard", "Cafeteria");
                case "CafeteriaManager":
                    return RedirectToAction("Inventory", "Cafeteria");
                case "SportsDirector":
                    return RedirectToAction("Coaches", "Cafeteria");
                case "Coach":
                    return RedirectToAction("MyTeam", "Coach");

                default:
                    return RedirectToAction("Index", "Home");
            }
        }

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        //Get Password 
        //https://localhost:port/YourControllerName/GetHash?pwd=YourPassword123
        public ActionResult GetHash(string pwd)
        {
            return Content(HashPassword(pwd));
        }

        // Temporary diagnostic endpoint to inspect session keys in the browser.
        // Visit /Account/SessionInfo while logged in to see current session values.
        public ActionResult SessionInfo()
        {
            try
            {
                var info = new
                {
                    UserId = Session["UserId"],
                    UserName = Session["UserName"],
                    UserRole = Session["UserRole"],
                    TeacherId = Session["TeacherId"],
                    ParentId = Session["ParentId"],
                    StudentId = Session["StudentId"],
                    MaintenanceStaffId = Session["MaintenanceStaffId"],
                    CoachId = Session["CoachId"]
                };

                return Json(info, JsonRequestBehavior.AllowGet);
            }
            catch (System.Exception ex)
            {
                return Content("Error reading session: " + ex.Message);
            }
        }

        // navigate to /Account/SeedTransportManager to seed 
        public ActionResult SeedTransportManager()
        {
            using (var db = new DBContextClass())
            {
                // Check if a Transport Manager already exists
                if (db.Users.Any(u => u.Role == "TransportManager"))
                {
                    TempData["Info"] = "Transport Manager account already exists.";
                    return RedirectToAction("Login");
                }

                // Password "Transport@123" hashed (use your existing HashPassword method)
                string hashedPassword = AccountController.HashPassword("Transport@123");

                // Create the Transport Manager
                var transportManager = new AppUser
                {
                    Name = "Transport Manager",
                    Email = "transport@michaelhouse.co.za",
                    PasswordHash = hashedPassword,
                    Role = "TransportManager"   // exact spelling as used in your app
                };

                db.Users.Add(transportManager);
                db.SaveChanges();

                TempData["Success"] = "Transport Manager created. Email: transport@michaelhouse.co.za | Password: Transport@123";
                return RedirectToAction("Login");
            }
        }

        // ─── Seed Maintenance Management ───────────────────────────────────────────
        public ActionResult SeedMaintenance()
        {
            using (var db = new DBContextClass())
            {
                if (db.Users.Any(u => u.Role == "MaintenanceManager"))
                {
                    TempData["Info"] = "Maintenance accounts already exist.";
                    return RedirectToAction("Login");
                }

                // ── 1. Maintenance Manager ──────────────────────────────────────
                var manager = new AppUser
                {
                    Name = "Mr. James Mokoena",
                    Email = "maintenance@michaelhouse.co.za",
                    PasswordHash = HashPassword("Manager@123"),
                    Role = "MaintenanceManager"
                };
                db.Users.Add(manager);
                db.SaveChanges();

                // ── 2. Maintenance Workers ──────────────────────────────────────
                var workersData = new[]
                {
                    new { Name = "Sipho Dlamini",   Email = "sipho.dlamini@michaelhouse.co.za",
                          Skill = "Plumbing",   ShiftStart = new TimeSpan(6,0,0),  ShiftEnd = new TimeSpan(14,0,0) },
                    new { Name = "Eric Mthembu",    Email = "eric.mthembu@michaelhouse.co.za",
                          Skill = "Electrical", ShiftStart = new TimeSpan(6,0,0),  ShiftEnd = new TimeSpan(14,0,0) },
                    new { Name = "Sifiso Khumalo",  Email = "sifiso.khumalo@michaelhouse.co.za",
                          Skill = "Grounds",    ShiftStart = new TimeSpan(6,0,0),  ShiftEnd = new TimeSpan(14,0,0) },
                    new { Name = "Bongani Nkosi",   Email = "bongani.nkosi@michaelhouse.co.za",
                          Skill = "Plumbing",   ShiftStart = new TimeSpan(14,0,0), ShiftEnd = new TimeSpan(22,0,0) },
                    new { Name = "Thabo Zulu",      Email = "thabo.zulu@michaelhouse.co.za",
                          Skill = "Electrical", ShiftStart = new TimeSpan(14,0,0), ShiftEnd = new TimeSpan(22,0,0) },
                    new { Name = "Lungelo Mbatha",  Email = "lungelo.mbatha@michaelhouse.co.za",
                          Skill = "HVAC",       ShiftStart = new TimeSpan(6,0,0),  ShiftEnd = new TimeSpan(14,0,0) },
                    new { Name = "Mandla Cele",     Email = "mandla.cele@michaelhouse.co.za",
                          Skill = "Pool",       ShiftStart = new TimeSpan(6,0,0),  ShiftEnd = new TimeSpan(14,0,0) },
                    new { Name = "Sandile Ntanzi",  Email = "sandile.ntanzi@michaelhouse.co.za",
                          Skill = "General",    ShiftStart = new TimeSpan(6,0,0),  ShiftEnd = new TimeSpan(14,0,0) },
                };

                int count = 1;
                foreach (var w in workersData)
                {
                    var wUser = new AppUser
                    {
                        Name = w.Name,
                        Email = w.Email,
                        PasswordHash = HashPassword("Worker@123"),
                        Role = "MaintenanceWorker"
                    };
                    db.Users.Add(wUser);
                    db.SaveChanges();

                    db.MaintenanceStaff.Add(new MaintenanceStaff
                    {
                        FullName = w.Name,
                        StaffNumber = string.Format("MH-MAINT-{0:D3}", count),
                        Email = w.Email,
                        Phone = "073100" + (1000 + count),
                        SkillType = w.Skill,
                        ShiftStart = w.ShiftStart,
                        ShiftEnd = w.ShiftEnd,
                        CurrentStatus = "Available",
                        IsActive = true,
                        DateJoined = DateTime.Now,
                        UserId = wUser.UserId
                    });
                    db.SaveChanges();
                    count++;
                }

                TempData["Success"] = "Maintenance accounts created. "
                    + "Manager: maintenance@michaelhouse.co.za / Manager@123. "
                    + "Workers: [firstname].[lastname]@michaelhouse.co.za / Worker@123";

                return RedirectToAction("Login");
            }
        }

        // ─── Seed Cafeteria & Sports Roles (NEW) ────────────────────────────────
        // Navigate to /Account/SeedCafeteria to create:
        // - Dietitian, Meal Coordinator, Chef, Cafeteria Manager, Sports Director
        // - 4 Coach users (Brown, White, Green, Black)
        // - 4 Coach records linked to their sports
        // - 4 Teams
        public ActionResult SeedCafeteria()
        {
            using (var db = new DBContextClass())
            {
                // ─── 1. Cafeteria Staff Users ───────────────────────────────────

                if (!db.Users.Any(u => u.Email == "dietitian@michaelhouse.co.za"))
                {
                    db.Users.Add(new AppUser
                    {
                        Name = "Dr. Sarah Naidoo",
                        Email = "dietitian@michaelhouse.co.za",
                        PasswordHash = HashPassword("Diet@123"),
                        Role = "Dietitian"
                    });

                    db.SaveChanges();
                }

                if (!db.Users.Any(u => u.Email == "meals@michaelhouse.co.za"))
                {
                    db.Users.Add(new AppUser
                    {
                        Name = "Mrs. Thandi Mkhize",
                        Email = "meals@michaelhouse.co.za",
                        PasswordHash = HashPassword("Meals@123"),
                        Role = "MealCoordinator"
                    });

                    db.SaveChanges();
                }

                if (!db.Users.Any(u => u.Email == "chef@michaelhouse.co.za"))
                {
                    db.Users.Add(new AppUser
                    {
                        Name = "Chef Sipho Ndlovu",
                        Email = "chef@michaelhouse.co.za",
                        PasswordHash = HashPassword("Chef@123"),
                        Role = "Chef"
                    });

                    db.SaveChanges();
                }

                if (!db.Users.Any(u => u.Email == "cafeteria@michaelhouse.co.za"))
                {
                    db.Users.Add(new AppUser
                    {
                        Name = "Mr. Bongani Zulu",
                        Email = "cafeteria@michaelhouse.co.za",
                        PasswordHash = HashPassword("Cafe@123"),
                        Role = "CafeteriaManager"
                    });

                    db.SaveChanges();
                }

                if (!db.Users.Any(u => u.Email == "sports@michaelhouse.co.za"))
                {
                    db.Users.Add(new AppUser
                    {
                        Name = "Mr. David Pretorius",
                        Email = "sports@michaelhouse.co.za",
                        PasswordHash = HashPassword("Sports@123"),
                        Role = "SportsDirector"
                    });

                    db.SaveChanges();
                }


                // ─── 2. Coach Users ─────────────────────────────────────────────

                var coachData = new[]
                {
            new
            {
                FirstName = "John",
                Surname = "Brown",
                Email = "brown@michaelhouse.co.za",
                Sport = "Rugby",
                Team = "U16 Rugby",
                Phone = "082 123 4567"
            },

            new
            {
                FirstName = "Sarah",
                Surname = "White",
                Email = "white@michaelhouse.co.za",
                Sport = "Hockey",
                Team = "1st Hockey",
                Phone = "082 987 6543"
            },

            new
            {
                FirstName = "David",
                Surname = "Green",
                Email = "green@michaelhouse.co.za",
                Sport = "Cricket",
                Team = "1st Cricket",
                Phone = "082 555 1234"
            },

            new
            {
                FirstName = "Jane",
                Surname = "Black",
                Email = "black@michaelhouse.co.za",
                Sport = "Swimming",
                Team = "Swimming Squad",
                Phone = "082 444 5678"
            }
        };


                foreach (var c in coachData)
                {
                    // ─── Create AppUser ─────────────────────────────────────────

                    if (!db.Users.Any(u => u.Email == c.Email))
                    {
                        db.Users.Add(new AppUser
                        {
                            Name = c.FirstName + " " + c.Surname,
                            Email = c.Email,
                            PasswordHash = HashPassword("Coach@123"),
                            Role = "Coach"
                        });

                        db.SaveChanges();
                    }


                    // ─── Create Coach record ───────────────────────────────────

                    if (!db.Coaches.Any(x => x.Email == c.Email))
                    {
                        db.Coaches.Add(new Coach
                        {
                            Name = c.FirstName,
                            Surname = c.Surname,

                            // IMPORTANT:
                            // Sport is a Sport navigation property.
                            // SportName is the legacy string field.
                            SportName = c.Sport,

                            Team = c.Team,
                            Email = c.Email,
                            Phone = c.Phone,
                            IsActive = true
                        });

                        db.SaveChanges();
                    }
                }


                // ─── 3. Teams ──────────────────────────────────────────────────

                var brownCoach = db.Coaches
                    .FirstOrDefault(c => c.Email == "brown@michaelhouse.co.za");

                var whiteCoach = db.Coaches
                    .FirstOrDefault(c => c.Email == "white@michaelhouse.co.za");

                var greenCoach = db.Coaches
                    .FirstOrDefault(c => c.Email == "green@michaelhouse.co.za");

                var blackCoach = db.Coaches
                    .FirstOrDefault(c => c.Email == "black@michaelhouse.co.za");


                // ─── U16 Rugby ─────────────────────────────────────────────────

                if (brownCoach != null &&
                    !db.Teams.Any(t => t.Name == "U16 Rugby"))
                {
                    db.Teams.Add(new Team
                    {
                        Name = "U16 Rugby",
                        SportName = "Rugby",
                        CoachID = brownCoach.CoachID,
                        Players = "",
                        IsActive = true
                    });

                    db.SaveChanges();
                }


                // ─── 1st Hockey ────────────────────────────────────────────────

                if (whiteCoach != null &&
                    !db.Teams.Any(t => t.Name == "1st Hockey"))
                {
                    db.Teams.Add(new Team
                    {
                        Name = "1st Hockey",
                        SportName = "Hockey",
                        CoachID = whiteCoach.CoachID,
                        Players = "",
                        IsActive = true
                    });

                    db.SaveChanges();
                }


                // ─── 1st Cricket ───────────────────────────────────────────────

                if (greenCoach != null &&
                    !db.Teams.Any(t => t.Name == "1st Cricket"))
                {
                    db.Teams.Add(new Team
                    {
                        Name = "1st Cricket",
                        SportName = "Cricket",
                        CoachID = greenCoach.CoachID,
                        Players = "",
                        IsActive = true
                    });

                    db.SaveChanges();
                }


                // ─── Swimming Squad ────────────────────────────────────────────

                if (blackCoach != null &&
                    !db.Teams.Any(t => t.Name == "Swimming Squad"))
                {
                    db.Teams.Add(new Team
                    {
                        Name = "Swimming Squad",
                        SportName = "Swimming",
                        CoachID = blackCoach.CoachID,
                        Players = "",
                        IsActive = true
                    });

                    db.SaveChanges();
                }


                // ─── 4. Link Coaches and Teams to Sport records ────────────────

                var sports = db.Sports.ToList();


                // Link coaches
                var coaches = db.Coaches.ToList();

                foreach (var coach in coaches)
                {
                    if (coach.SportId == null &&
                        !string.IsNullOrEmpty(coach.SportName))
                    {
                        var sport = sports.FirstOrDefault(s =>
                            s.Name.Equals(
                                coach.SportName,
                                StringComparison.OrdinalIgnoreCase));

                        if (sport != null)
                        {
                            coach.SportId = sport.SportId;
                        }
                    }
                }


                // Link teams
                var teams = db.Teams.ToList();

                foreach (var team in teams)
                {
                    if (team.SportId == null &&
                        !string.IsNullOrEmpty(team.SportName))
                    {
                        var sport = sports.FirstOrDefault(s =>
                            s.Name.Equals(
                                team.SportName,
                                StringComparison.OrdinalIgnoreCase));

                        if (sport != null)
                        {
                            team.SportId = sport.SportId;
                        }
                    }
                }

                db.SaveChanges();


                // ─── Success message ───────────────────────────────────────────

                TempData["Success"] =
                    "Cafeteria & Sports roles created. " +
                    "Dietitian: dietitian@michaelhouse.co.za / Diet@123. " +
                    "Meal Coordinator: meals@michaelhouse.co.za / Meals@123. " +
                    "Chef: chef@michaelhouse.co.za / Chef@123. " +
                    "Cafeteria Manager: cafeteria@michaelhouse.co.za / Cafe@123. " +
                    "Sports Director: sports@michaelhouse.co.za / Sports@123. " +
                    "Coaches: [brown|white|green|black]@michaelhouse.co.za / Coach@123";

                return RedirectToAction("Login");
            }
        }
    }
}