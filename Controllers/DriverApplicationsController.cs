using Michaelhouse.Controllers;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Michaelhouse
{
    public class DriverApplicationsController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // helper to resolve current AppUser.Id (AppUser.UserName expected to match User.Identity.Name)
        private int? GetCurrentAppUserId()
        {
            if (!User.Identity.IsAuthenticated) return null;
            var u = db.Users.FirstOrDefault(x => x.Email == User.Identity.Name);
            return u?.UserId;
        }

        // GET: DriverApplications
        // Only admins can see the full list
        [Authorize(Roles = "Admin")]
        public ActionResult Index()
        {
            return View(db.DriverApplications.ToList());
        }

        // Applicant-only list of their own applications
        [Authorize]
        public ActionResult MyApplications()
        {
            var uid = GetCurrentAppUserId();
            if (uid == null) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            var apps = db.DriverApplications.Where(a => a.UserId == uid).ToList();
            return View(apps);
        }

        // GET: DriverApplications/Details/5
        // Admins or owning applicant can view
        [Authorize]
// No-op patch to ensure file context consistent
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var application = db.DriverApplications.Include(d => d.Documents).FirstOrDefault(d => d.Id == id);
            if (application == null) return HttpNotFound();

// No-op patch to ensure file context consistent
            var uid = GetCurrentAppUserId();
            if (!User.IsInRole("Admin") && application.UserId != uid)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            return View(application);
        }

        // GET: DriverApplications/Create
        // Allow anonymous users to create driver applications so applicants without accounts can apply
        [AllowAnonymous]
        public ActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult Create(
           DriverApplication driverApplication,
           HttpPostedFileBase idFile,
           HttpPostedFileBase licenceFile,
           IEnumerable<HttpPostedFileBase> otherFiles,
           string otherDescription)
        {
            if (!ModelState.IsValid) return View(driverApplication);

            // link to current user (if one exists)
            driverApplication.UserId = GetCurrentAppUserId();

            // Generate a public token for unauthenticated applicants so they can return and edit/view
            var rawToken = Guid.NewGuid().ToString("N");
            driverApplication.PublicTokenExpiry = DateTime.UtcNow.AddDays(14); // token valid for 14 days
            driverApplication.PublicTokenHash = ComputeSha256Hash(rawToken);

            driverApplication.Status = "Pending";
            driverApplication.DateSubmitted = DateTime.Now;

            db.DriverApplications.Add(driverApplication);
            db.SaveChanges();

            // helper to save files
            Action<HttpPostedFileBase, string> saveDoc = (file, docType) =>
            {
                if (file == null || file.ContentLength == 0) return;
                string fileName = Guid.NewGuid() + System.IO.Path.GetExtension(file.FileName);
                var uploadsDir = Server.MapPath("~/Uploads");
                // ensure upload folder exists
                if (!System.IO.Directory.Exists(uploadsDir))
                {
                    System.IO.Directory.CreateDirectory(uploadsDir);
                }
                string path = System.IO.Path.Combine(uploadsDir, fileName);
                file.SaveAs(path);

                var doc = new DriverDocument
                {
                    DriverApplicationId = driverApplication.Id,
                    FilePath = "/Uploads/" + fileName,
                    DocumentType = docType,
                    OtherDocumentType = docType == "Other" ? otherDescription : null
                };
                db.DriverDocuments.Add(doc);
            };

            // required typed uploads
            saveDoc(idFile, "ID");
            saveDoc(licenceFile, "Licence");

            if (otherFiles != null)
            {
                foreach (var f in otherFiles) saveDoc(f, "Other");
            }

            db.SaveChanges();

            // call AI review (adapt method name / mapping to your AiReviewService API)
            try
            {
                // Trigger AI review in background and persist to AdminReviews linking DriverAppId
                Task.Run(async () =>
                {
                    try
                    {
                        var ai = new AiReviewService();
                        var result = await ai.ReviewDriverApplicationAsync(driverApplication);

                        // Map AI recommendation -> AdminReview.Decision
                        string decision;
                        if (result.Recommendation == "APPROVE")
                            decision = "Approved";
                        else if (result.Recommendation == "REJECT")
                            decision = "Rejected";
                        else
                            decision = "Waitlisted"; // FLAG -> Waitlisted (human attention)

                        using (var db2 = new DBContextClass())
                        {
                            var review = new AdminReview
                            {
                                DriverAppId = driverApplication.Id,
                                AdminId = "system", // synthetic system reviewer
                                Date = DateTime.UtcNow,
                                Decision = decision,
                                AdminNotes = result.Summary,
                                AgreedWithAi = false
                            };
                            db2.AdminReviews.Add(review);
                            db2.SaveChanges();
                        }
                    }
                    catch
                    {
                        // logging can be added here; do not affect user flow
                    }
                });
            }
            catch
            {
                // do not block applicant; log if you have logging
            }

            // If the applicant is not authenticated, send them to a public details page for their submission and email the link
            var currentUid = GetCurrentAppUserId();
            if (currentUid == null)
            {
                // send email with public link
                try
                {
                    var emailSvc = new Services.EmailService();
                    var publicUrl = Url.Action("PublicDetails", "DriverApplications", new { id = driverApplication.Id, token = rawToken }, protocol: Request.Url.Scheme);
                    var body = $"Your driver application has been received. You can view/edit it using this link (valid until {driverApplication.PublicTokenExpiry:yyyy-MM-dd}): {publicUrl}";
                    emailSvc.SendPlain(driverApplication.Email, "Your Driver Application - Michaelhouse", body);
                }
                catch
                {
                    // swallow email errors — user can still use the immediate redirect
                }

                return RedirectToAction("PublicDetails", new { id = driverApplication.Id, token = rawToken });
            }

            return RedirectToAction("MyApplications");
        }

        // GET: DriverApplications/Edit/5
        [AllowAnonymous]
        public ActionResult Edit(int? id, string token = null)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            DriverApplication driverApplication = db.DriverApplications.Find(id);
            if (driverApplication == null) return HttpNotFound();

            var uid = GetCurrentAppUserId();
            // Allow owner (authenticated), admin, or a matching public token to edit
            bool tokenMatches = false;
            if (!string.IsNullOrEmpty(token) && driverApplication.PublicTokenExpiry != null && driverApplication.PublicTokenExpiry > DateTime.UtcNow)
            {
                tokenMatches = ComputeSha256Hash(token) == driverApplication.PublicTokenHash;
            }

            if (!User.IsInRole("Admin") && driverApplication.UserId != uid && !tokenMatches)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            return View(driverApplication);
        }

        // POST: DriverApplications/Create
       

        // POST: DriverApplications/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult DeleteConfirmed(int id)
        {
            DriverApplication driverApplication = db.DriverApplications.Find(id);
            if (driverApplication == null) return HttpNotFound();

            var uid = GetCurrentAppUserId();
            if (!User.IsInRole("Admin") && driverApplication.UserId != uid)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            db.DriverApplications.Remove(driverApplication);
            db.SaveChanges();
            if (User.IsInRole("Admin")) return RedirectToAction("Index");
            return RedirectToAction("MyApplications");
        }

        // POST: DriverApplications/Review/5 (admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // only admins review
        public ActionResult Review(int id, string decision, string adminNotes)
        {
            var app = db.DriverApplications.Find(id);
            if (app == null) return HttpNotFound();

            if (app.Status == "Approved" || app.Status == "Rejected")
                return RedirectToAction("Details", new { id = id });

            app.Status = decision;
            app.AdminNotes = adminNotes;
            app.ReviewedDate = DateTime.Now;

            if (decision == "Approve")
            {
                var existingDriver = db.Drivers.FirstOrDefault(d => d.IDNumber == app.IDNumber);

                if (existingDriver == null)
                {
                    string schoolEmail = app.FullName.ToLower().Replace(" ", ".") + "@michealhouse.com";
                    string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

                    // 1. Create USER (for login system)
                    var user = new AppUser
                    {
                        Name = app.FullName,
                        Email = schoolEmail,
                        PasswordHash = AccountController.HashPassword(tempPassword),
                        Role = "Driver"
                    };

                    db.Users.Add(user);
                    db.SaveChanges();

                    // 2. Create DRIVER (linked to user)
                    var driver = new Driver
                    {
                        FullName = app.FullName,
                        IDNumber = app.IDNumber,
                        PhoneNumber = app.PhoneNumber,
                        Email = schoolEmail,
                        PasswordHash = user.PasswordHash, // optional (can remove later)
                        LicenceNumber = app.LicenceNumber,
                        LicenceExpiryDate = app.LicenceExpiryDate,
                        HasPDP = app.HasPDP,
                        IsActive = true,
                        DateCreated = DateTime.Now,
                        UserId = user.UserId // 🔥 IMPORTANT LINK
                    };

                    db.Drivers.Add(driver);
                    db.SaveChanges();

                    // 3. Send login details
                    SendDriverEmail(app.Email, schoolEmail, tempPassword);
                }

                return RedirectToAction("Details", new { id = id });
            }

            db.SaveChanges();
            return RedirectToAction("Details", new { id = id });
        }

        // Public details view for unauthenticated applicants to view their submission
        [AllowAnonymous]
        public ActionResult PublicDetails(int id)
        {
            var application = db.DriverApplications.Include(d => d.Documents).FirstOrDefault(d => d.Id == id);
            if (application == null) return HttpNotFound();

            // Return the same Details view but hide admin controls in the view based on user role
            ViewBag.IsPublicViewer = true;
            return View("Details", application);
        }

        private void SendDriverEmail(string personalEmail, string schoolEmail, string tempPassword)
        {
            var mail = new System.Net.Mail.MailMessage();
            mail.To.Add(personalEmail);
            mail.Subject = "Michaelhouse Driver Account Created";
            mail.Body =
                $"Your account has been approved.\n\n" +
                $"Email: {schoolEmail}\n" +
                $"Temporary Password: {tempPassword}\n\n" +
                $"Please change your password after login.";

            var smtp = new System.Net.Mail.SmtpClient();
            smtp.Send(mail);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // Helper: compute SHA256 hash for tokens
        private static string ComputeSha256Hash(string rawData)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(rawData);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
