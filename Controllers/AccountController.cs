using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class AccountController : Controller
    {
        // ─── Login ────────────────────────────────────────────────────────────────

        public ActionResult Login()
        {
            if (Session["UserId"] != null)
            {
                string role = Session["UserRole"]?.ToString();
                if (role == "Parent" && Session["ParentId"] == null)
                {
                    Session.Clear();
                    Session.Abandon();
                    TempData["Error"] = "Your account is not properly linked. Please contact support.";
                    return View();
                }
                else if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors); // Set a breakpoint here!
                    return View();
                }
                return RedirectByRole(role);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

            using (var db = new DBContextClass())
            {
                var hash = HashPassword(vm.Password);
                var user = db.Users.FirstOrDefault(u =>
                    u.Email == vm.Email && u.PasswordHash == hash);

                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(vm);
                }

                // Set session
                Session["UserId"] = user.UserId;
                Session["UserName"] = user.Name;
                Session["UserRole"] = user.Role;

                if (user.Role == "Parent")
                {
                    var parent = db.Parents.FirstOrDefault(p => p.UserId == user.UserId);
                    if (parent != null)
                        Session["ParentId"] = parent.ParentId;
                }
                else if (user.Role == "Student")
                {
                    var student = db.Students.FirstOrDefault(s => s.UserId == user.UserId);
                    if (student != null)
                        Session["StudentId"] = student.StudentId;
                }

				else if (user.Role == "Teacher")
				{
					var teacher = db.Teachers.FirstOrDefault(t => t.UserId == user.UserId);
					if (teacher != null)
						Session["TeacherId"] = teacher.TeacherId;
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
                // Check email not already taken
                if (db.Users.Any(u => u.Email == vm.Email))
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(vm);
                }

                // Create user account
                var user = new AppUser
                {
                    Name = vm.Name,
                    Email = vm.Email,
                    PasswordHash = HashPassword(vm.Password),
                    Role = "Parent"
                };
                db.Users.Add(user);
                db.SaveChanges();

                // Create linked parent profile
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
            Session.Clear();
            Session.Abandon();
            TempData["Success"] = "You have been logged out.";
            return RedirectToAction("Login");
        }

        // ─── Edit Profile ─────────────────────────────────────────────────────────

        [RequireLogin]
        public ActionResult Profile()
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
        public ActionResult Profile(ParentProfileViewModel vm)
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

                // Update session name if changed
                Session["UserName"] = vm.Name;

                db.SaveChanges();

                TempData["Success"] = "Profile updated successfully.";
                return RedirectToAction("Profile");
            }
        }

        // ─── Seed Admin (run once) ────────────────────────────────────────────────

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
<<<<<<< HEAD
            if (role == "Admin")
                return RedirectToAction("Dashboard", "Admin");
                case "Parent": return RedirectToAction("Dashboard", "Parents");
                case "Student": return RedirectToAction("Dashboard", "Students");
                case "Teacher": return RedirectToAction("Index", "Marks");
				default: return RedirectToAction("Login", "Account");
            }
>>>>>>> fd5ded9f696e1d78c5a52b31a453ae61bc131b0f
        }

        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }
    }
}