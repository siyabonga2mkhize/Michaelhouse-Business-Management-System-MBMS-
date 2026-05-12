using Michaelhouse.Controllers;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using static Michaelhouse.Filters.TransportManagerOrAdminOnlyAttribute;

namespace Michaelhouse
{
    public class DriverApplicationsController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // Helper to resolve current AppUser.Id
        private int? GetCurrentAppUserId()
        {
            if (!User.Identity.IsAuthenticated) return null;
            var u = db.Users.FirstOrDefault(x => x.Email == User.Identity.Name);
            return u?.UserId;
        }

        // GET: DriverApplications (Admin only)
        [AdminOrTransportManagerOnly]
        public ActionResult Index()
        {
            return View(db.DriverApplications.ToList());
        }

        // Applicant-only list of their own applications
        [RequireLogin]
        public ActionResult MyApplications()
        {
            var uid = GetCurrentAppUserId();
            if (uid == null) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            var apps = db.DriverApplications.Where(a => a.UserId == uid).ToList();
            return View(apps);
        }

        // GET: Details (Admin or owning applicant)
        [RequireLogin]
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var application = db.DriverApplications.Include(d => d.Documents).FirstOrDefault(d => d.Id == id);
            if (application == null) return HttpNotFound();

            var currentUserId = (int)Session["UserId"];
            var currentRole = Session["UserRole"]?.ToString();
            bool isAdminOrTransport = (currentRole == "Admin" || currentRole == "TransportManager");
            bool isOwner = (application.UserId == currentUserId);

            if (!isAdminOrTransport && !isOwner)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            return View(application);
        }

        // GET: Create (Allow anonymous)
        [AllowAnonymous]
        public ActionResult Create()
        {
            return View();
        }

        // GET: Edit (Allow anonymous with valid token)
        [AllowAnonymous]
        public ActionResult Edit(int? id, string token = null)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            DriverApplication driverApplication = db.DriverApplications.Find(id);
            if (driverApplication == null) return HttpNotFound();

            var uid = GetCurrentAppUserId();
            bool tokenMatches = false;
            if (!string.IsNullOrEmpty(token) && driverApplication.PublicTokenExpiry != null && driverApplication.PublicTokenExpiry > DateTime.UtcNow)
            {
                tokenMatches = ComputeSha256Hash(token) == driverApplication.PublicTokenHash;
            }

            if (!User.IsInRole("Admin") && driverApplication.UserId != uid && !tokenMatches)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            var currentUserId = (int?)Session["UserId"];
            var currentRole = Session["UserRole"]?.ToString();
            bool isAdmin = currentRole == "Admin";
            bool isOwner = (driverApplication.UserId == currentUserId);

            if (!isAdmin && !isOwner && !tokenMatches)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            return View(driverApplication);
        }

        // POST: Create – Save application, send acknowledgment email, no account creation
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
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var e in errors)
                    System.Diagnostics.Debug.WriteLine("Model error: " + e.ErrorMessage);
                return View(driverApplication);
            }

            driverApplication.UserId = GetCurrentAppUserId();

            // Generate public token for unauthenticated applicants
            var rawToken = Guid.NewGuid().ToString("N");
            driverApplication.PublicTokenExpiry = DateTime.UtcNow.AddDays(14);
            driverApplication.PublicTokenHash = ComputeSha256Hash(rawToken);

            driverApplication.Status = "Pending";
            driverApplication.DateSubmitted = DateTime.Now;

            db.DriverApplications.Add(driverApplication);
            db.SaveChanges();

            // Save uploaded files
            Action<HttpPostedFileBase, string> saveDoc = (file, docType) =>
            {
                if (file == null || file.ContentLength == 0) return;
                string fileName = Guid.NewGuid() + System.IO.Path.GetExtension(file.FileName);
                var uploadsDir = Server.MapPath("~/Uploads");
                if (!System.IO.Directory.Exists(uploadsDir))
                    System.IO.Directory.CreateDirectory(uploadsDir);
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

            saveDoc(idFile, "ID");
            saveDoc(licenceFile, "Licence");
            if (otherFiles != null)
            {
                foreach (var f in otherFiles) saveDoc(f, "Other");
            }
            db.SaveChanges();

            // Trigger AI review in background (optional)
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var ai = new AiReviewService();
                        var result = await ai.ReviewDriverApplicationAsync(driverApplication);
                        string decision;
                        if (result.Recommendation == "APPROVE") decision = "Approved";
                        else if (result.Recommendation == "REJECT") decision = "Rejected";
                        else decision = "Waitlisted";

                        using (var db2 = new DBContextClass())
                        {
                            var review = new AdminReview
                            {
                                DriverAppId = driverApplication.Id,
                                AdminId = "system",
                                Date = DateTime.UtcNow,
                                Decision = decision,
                                AdminNotes = result.Summary,
                                AgreedWithAi = false
                            };
                            db2.AdminReviews.Add(review);
                            db2.SaveChanges();
                        }
                    }
                    catch { /* log if needed */ }
                });
            }
            catch { /* ignore */ }

            // Send acknowledgment email (no account created)
            var currentUid = GetCurrentAppUserId();
            if (currentUid == null)
            {
                try
                {
                    var emailSvc = new EmailService();
                    var publicUrl = Url.Action("PublicDetails", "DriverApplications", new { id = driverApplication.Id, token = rawToken }, protocol: Request.Url.Scheme);
                    var subject = "Driver Application Received - Michaelhouse";
                    var body = $@"
Dear {driverApplication.FullName},

Thank you for submitting your driver application. We have received it and will review it shortly.

You can view or edit your application using this link (valid until {driverApplication.PublicTokenExpiry:yyyy-MM-dd}):
{publicUrl}

If we decide to proceed, we will contact you for an interview.

Regards,
Michaelhouse Transport Team
";
                    emailSvc.SendPlain(driverApplication.Email, subject, body);
                }
                catch { /* log email error */ }

                return RedirectToAction("PublicDetails", new { id = driverApplication.Id, token = rawToken });
            }

            return RedirectToAction("MyApplications");
        }

        // POST: Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public ActionResult DeleteConfirmed(int id)
        {
            DriverApplication driverApplication = db.DriverApplications.Find(id);
            if (driverApplication == null) return HttpNotFound();

            var currentUserId = (int)Session["UserId"];
            var currentRole = Session["UserRole"]?.ToString();
            bool isAdmin = currentRole == "Admin";
            bool isOwner = (driverApplication.UserId == currentUserId);

            if (!isAdmin && !isOwner)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            db.DriverApplications.Remove(driverApplication);
            db.SaveChanges();
            if (isAdmin) return RedirectToAction("Index");
            return RedirectToAction("MyApplications");
        }

        // POST: Admin Review (no automatic account creation)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminOrTransportManagerOnly]
        public ActionResult Review(int id, string decision, string adminNotes)
        {
            var app = db.DriverApplications.Find(id);
            if (app == null) return HttpNotFound();

            if (app.Status == "Approved" || app.Status == "Rejected")
                return RedirectToAction("Details", new { id = id });

            app.Status = decision;
            app.AdminNotes = adminNotes;
            app.ReviewedDate = DateTime.Now;

            try
            {
                db.SaveChanges();
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"Property: {x.PropertyName}, Error: {x.ErrorMessage}");
                var fullErrorMessage = string.Join("; ", errorMessages);
                TempData["Error"] = $"Validation failed: {fullErrorMessage}";
                return RedirectToAction("Details", new { id = id });
            }

            TempData["Success"] = $"Application {decision}.";
            return RedirectToAction("Details", new { id = id });
        }

        // POST: Send Interview Invitation Email (generates Jitsi meeting link)
        [HttpPost]
        [AdminOrTransportManagerOnly]
        public ActionResult SendInterviewInvitation(int id)
        {
            var app = db.DriverApplications.Find(id);
            if (app == null) return HttpNotFound();

            // Generate a unique meeting room name for Jitsi
            string roomName = $"DriverInterview-{app.Id}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            string meetingLink = $"https://meet.jit.si/{roomName}";

            try
            {
                var emailSvc = new EmailService();
                var subject = "Driver Interview Invitation - Michaelhouse";
                var body = $@"
Dear {app.FullName},

Congratulations! Your driver application has progressed to the interview stage.

Please join us for an online interview at the following link (open source Jitsi Meet):
{meetingLink}

We look forward to speaking with you.

Best regards,
Michaelhouse Transport Team
";
                emailSvc.SendPlain(app.Email, subject, body);
                TempData["Success"] = "Interview invitation email sent.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to send email: {ex.Message}";
            }
            return RedirectToAction("Details", new { id = id });
        }

        // POST: Create Driver Account (after successful interview)
        [HttpPost]
        [AdminOrTransportManagerOnly]
        public ActionResult CreateDriverAccount(int id)
        {
            var app = db.DriverApplications.Find(id);
            if (app == null) return HttpNotFound();

            // Check if driver already exists (by ID number)
            var existingDriver = db.Drivers.FirstOrDefault(d => d.IDNumber == app.IDNumber);
            if (existingDriver != null)
            {
                TempData["Error"] = "A driver with this ID number already exists.";
                return RedirectToAction("Details", new { id = id });
            }

            string schoolEmail = app.FullName.ToLower().Replace(" ", ".") + "@michaelhouse.co.za";
            string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

            // Create AppUser
            var user = new AppUser
            {
                Name = app.FullName,
                Email = schoolEmail,
                PasswordHash = AccountController.HashPassword(tempPassword),
                Role = "Driver"
            };
            db.Users.Add(user);
            db.SaveChanges();

            // Create Driver
            var driver = new Driver
            {
                FullName = app.FullName,
                IDNumber = app.IDNumber,
                PhoneNumber = app.PhoneNumber,
                Email = schoolEmail,
                PasswordHash = user.PasswordHash,
                LicenceNumber = app.LicenceNumber,
                LicenceExpiryDate = app.LicenceExpiryDate,
                HasPDP = app.HasPDP,
                IsActive = true,
                DateCreated = DateTime.Now,
                UserId = user.UserId
            };
            db.Drivers.Add(driver);
            db.SaveChanges();

            // Send login credentials email
            try
            {
                var emailSvc = new EmailService();
                var subject = "Driver Account Created - Michaelhouse";
                var body = $@"
Dear {app.FullName},

Your driver account has been created. You can now log in to the system.

Email: {schoolEmail}
Temporary Password: {tempPassword}

Please change your password after your first login.

Best regards,
Michaelhouse Transport Team
";
                emailSvc.SendPlain(app.Email, subject, body);
                TempData["Success"] = "Driver account created and login details emailed.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Account created but email failed: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = id });
        }

        // Public details view for unauthenticated applicants
        [AllowAnonymous]
        public ActionResult PublicDetails(int id, string token = null)
        {
            var application = db.DriverApplications
                .Include(d => d.Documents)
                .FirstOrDefault(d => d.Id == id);

            if (application == null) return HttpNotFound();

            bool tokenValid = false;
            if (!string.IsNullOrEmpty(token)
                && application.PublicTokenExpiry != null
                && application.PublicTokenExpiry > DateTime.UtcNow)
            {
                tokenValid = ComputeSha256Hash(token) == application.PublicTokenHash;
            }

            var uid = GetCurrentAppUserId();
            bool isOwner = uid != null && application.UserId == uid;

            if (!User.IsInRole("Admin") && !isOwner && !tokenValid)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            ViewBag.IsPublicViewer = !User.IsInRole("Admin") && !isOwner;

            if (!string.IsNullOrEmpty(token) && tokenValid)
                ViewBag.PublicToken = token;

            return View("Details", application);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}