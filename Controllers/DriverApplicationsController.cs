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

            // ════════════════════════════════════════════════════════════════════
            // Trigger AI review in background
            // ════════════════════════════════════════════════════════════════════
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var ai = new AiReviewService();

                        // Load application WITH documents for the review
                        DriverApplication appWithDocs;
                        using (var db2 = new DBContextClass())
                        {
                            appWithDocs = db2.DriverApplications
                                .Include(a => a.Documents)
                                .FirstOrDefault(a => a.Id == driverApplication.Id);
                        }

                        if (appWithDocs == null) return;

                        // Call the AI
                        var result = await ai.ReviewDriverApplicationAsync(appWithDocs);

                        // Map AI Recommendation to a formal decision string for AdminReviews
                        string decision = result.Recommendation == "APPROVE" ? "Approved"
                                        : result.Recommendation == "REJECT" ? "Rejected"
                                        : "Waitlisted"; // Used for FLAG

                        using (var db2 = new DBContextClass())
                        {
                            // 1. Save Admin Review
                            var review = new AdminReview
                            {
                                DriverAppId = driverApplication.Id,
                                AdminId = "ai-system",
                                Date = DateTime.UtcNow,
                                Decision = decision,
                                AdminNotes = result.Summary,
                                AgreedWithAi = false
                            };
                            db2.AdminReviews.Add(review);

                            // 2. Save recommendation back to the application and update Status
                            var appToUpdate = db2.DriverApplications.Find(driverApplication.Id);
                            if (appToUpdate != null)
                            {
                                // Optional: If you added AiReviewSummary to your model, you can save it here
                                 appToUpdate.AiReviewSummary = result.Summary;
                                appToUpdate.AiRecommendation = result.Recommendation;

                                // Automatically progress the application based on the AI's review
                                if (result.Recommendation == "APPROVE")
                                {
                                    // Optional: You can also change this to "Pending" if you want 
                                    // the admin to manually approve good applications too.
                                    appToUpdate.Status = "Pending Interview";
                                }
                                else if (result.Recommendation == "REJECT")
                                {
                                    // AI thinks it's a reject, but we leave it to the Admin to decide.
                                    // Setting it to "Flagged" keeps it in the Admin's queue.
                                    appToUpdate.Status = "Flagged";
                                }
                                else
                                {
                                    appToUpdate.Status = "Flagged";
                                }
                            }

                            db2.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AI driver review failed: {ex.Message}");
                    }
                });
            }
            catch { /* ignore */ }
            // ════════════════════════════════════════════════════════════════════

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
        // POST: Quick send (no date stored) – maybe deprecate
        [HttpPost]
        [AdminOrTransportManagerOnly]
        public ActionResult SendInterviewInvitation(int id)
        {
            var app = db.DriverApplications.Find(id);
            if (app == null) return HttpNotFound();

            // If no interview link stored yet, generate one
            if (string.IsNullOrEmpty(app.InterviewMeetingLink))
            {
                string roomName = $"DriverInterview-{app.Id}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                app.InterviewMeetingLink = $"https://meet.jit.si/{roomName}";
            }
            // If no date set, default to tomorrow at 10:00
            if (app.InterviewDateTime == null)
            {
                app.InterviewDateTime = DateTime.Today.AddDays(1).AddHours(10);
            }

            db.SaveChanges();
            bool sent = SendInterviewEmailNow(app);
            if (sent)
            {
                app.InterviewEmailSent = true;
                app.InterviewEmailSentAt = DateTime.UtcNow;
                db.SaveChanges();
                TempData["Success"] = "Interview invitation re‑sent.";
            }
            else
                TempData["Error"] = "Email sending failed.";

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

        // POST: Schedule Interview + Store meeting link + Send email
        [HttpPost]
        [AdminOrTransportManagerOnly]
        public ActionResult ScheduleAndSendInterview(int id, DateTime interviewDateTime, string customMeetingLink = null)
        {
            var app = db.DriverApplications.Find(id);
            if (app == null) return HttpNotFound();

            // Auto-generate Jitsi link if none provided
            string meetingLink = customMeetingLink;
            if (string.IsNullOrEmpty(meetingLink))
            {
                string roomName = $"DriverInterview-{app.Id}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                meetingLink = $"https://meet.jit.si/{roomName}";
            }

            // Store interview details
            app.InterviewDateTime = interviewDateTime;
            app.InterviewMeetingLink = meetingLink;
            app.InterviewEmailSent = false;

            db.SaveChanges();

            // Send email with stored details
            bool emailSent = SendInterviewEmailNow(app);
            if (emailSent)
            {
                app.InterviewEmailSent = true;
                app.InterviewEmailSentAt = DateTime.UtcNow;
                db.SaveChanges();
                TempData["Success"] = "Interview scheduled and email sent.";
            }
            else
            {
                TempData["Error"] = "Interview details saved but email failed to send.";
            }

            return RedirectToAction("Details", new { id = id });
        }

        // Helper method to send email using stored details
        private bool SendInterviewEmailNow(DriverApplication app)
        {
            try
            {
                //Log attempt
                System.Diagnostics.Debug.WriteLine($"Attempting to send interview email to {app.Email}");

                var emailSvc = new EmailService();
                var subject = "Driver Interview Invitation - Michaelhouse";
                var body = $@"
Dear {app.FullName},

Your interview has been scheduled for:
<strong>{app.InterviewDateTime:dddd, MMMM dd, yyyy at h:mm tt}</strong>

Join via this link:
{app.InterviewMeetingLink}

Please be ready 5 minutes before the scheduled time.

Best regards,
Michaelhouse Transport Team
";
                emailSvc.SendPlain(app.Email, subject, body);
                return true;
            }
            catch
            {
                return false;
            }
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