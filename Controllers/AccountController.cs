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
                // Same pattern as the Teacher check above, so refreshing the login
                // page while already logged in as a worker doesn't cause redirect loops.
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
                case "CafeteriaManager":
                    return RedirectToAction("Dashboard", "Cafeteria");
                case "Chef":
                    return RedirectToAction("ChefIndex", "Cafeteria");
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
                    MaintenanceStaffId = Session["MaintenanceStaffId"]
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

                // Optional: If you have a Drivers table and want to create a linked record
                // int newUserId = transportManager.UserId;
                // db.Drivers.Add(new Driver
                // {
                //     UserId = newUserId,
                //     FullName = "Transport Manager",
                //     Email = "transport@michaelhouse.co.za",
                //     IsActive = true,
                //     DateCreated = DateTime.Now,
                //     HasPDP = true,
                //     LicenceNumber = "MGR000"
                // });
                // db.SaveChanges();

                TempData["Success"] = "Transport Manager created. Email: transport@michaelhouse.co.za | Password: Transport@123";
                return RedirectToAction("Login");
            }

            // To seed everything in one go, navigate to import_all.sql and execute the SQL script in your database.
        }

        // ─── Seed Maintenance Management ───────────────────────────────────────────
        // Navigate to /Account/SeedMaintenance to create the Maintenance Manager
        // and a full team of Maintenance Workers, all as AppUser accounts with
        // linked MaintenanceStaff records — same pattern as SeedTransportManager.
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
    }
}
