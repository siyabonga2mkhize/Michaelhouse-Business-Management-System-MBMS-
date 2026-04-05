using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Michaelhouse.Models.Enums;


namespace Michaelhouse.Services
{
    public class ApplicationService
    {
        private readonly AiReviewService _ai = new AiReviewService();

        private readonly RegistrationService _regService = new RegistrationService();
        private readonly EmailService _email = new EmailService();

        // ─── Upload root (resolved for both web and background threads) ───────────
        private string GetUploadRoot()
        {
            var rel = ConfigurationManager.AppSettings["DocumentStorage:UploadRoot"]
                      ?? "~/App_Data/Uploads";
            return rel.StartsWith("~")
                ? System.Web.Hosting.HostingEnvironment.MapPath(rel)
                : rel;
        }

        // ─── Submit Application ───────────────────────────────────────────────────

        /// <summary>
        /// Creates the Application record and saves each uploaded file to disk.
        /// Returns the saved Application.
        /// </summary>
        public Application SubmitApplication(
            int parentId,
            int studentId,
            int year,
            int gradeApplying,
            string additionalNotes,
            HttpFileCollectionBase files,
            string[] docTypes)
        {
            using (var db = new DBContextClass())
            {
                var application = new Application
                {
                    ParentId = parentId,
                    StudentId = studentId,
                    ApplicationYear = year,
                    GradeApplying = gradeApplying,
                    AdditionalNotes = additionalNotes,
                    Date = DateTime.Now,
                    Status = ApplicationStatus.Pending
                };

                db.Applications.Add(application);
                db.SaveChanges();

                // Ensure upload folder exists
                var uploadRoot = GetUploadRoot();
                if (!Directory.Exists(uploadRoot))
                    Directory.CreateDirectory(uploadRoot);

                // Save each file
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file == null || file.ContentLength == 0) continue;

                    var docType = (docTypes != null && i < docTypes.Length) ? docTypes[i] : "Other";
                    var ext = Path.GetExtension(file.FileName);
                    var savedName = $"{Guid.NewGuid()}{ext}";

                    file.SaveAs(Path.Combine(uploadRoot, savedName));

                    db.Documents.Add(new Document
                    {
                        AppId = application.AppId,
                        StudentId = studentId,
                        Type = docType,
                        FilePath = savedName,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        UploadedAt = DateTime.Now
                    });
                }

                db.SaveChanges();
                return application;
            }
        }

        // ─── AI Review (runs on background thread) ────────────────────────────────

        /// <summary>
        /// Sets status to UnderAiReview, calls Azure AI, then updates
        /// the application with the result and moves it forward.
        /// </summary>
        public async Task TriggerAiReviewAsync(int appId)
        {
            // Each background thread needs its own DbContext instance
            using (var db = new DBContextClass())
            {
                var app = db.Applications
                    .Include("Student")
                    .Include("Parent")
                    .Include("Documents")
                    .FirstOrDefault(a => a.AppId == appId);

                if (app == null) return;

                app.Status = ApplicationStatus.UnderAiReview;
                db.SaveChanges();

                try
                {
                    var (summary, recommendation) = await _ai.ReviewApplicationAsync(app);

                    app.AiReviewSummary = summary;
                    app.AiRecommendation = recommendation;
                    app.Status = recommendation == "FLAG"
                        ? ApplicationStatus.Flagged
                        : ApplicationStatus.AwaitingAdminDecision;
                }
                catch (Exception ex)
                {
                    // If AI fails, flag it and store the error so admin can see it
                    app.AiReviewSummary = $"AI review failed: {ex.Message}";
                    app.AiRecommendation = "FLAG";
                    app.Status = ApplicationStatus.Flagged;
                }

                db.SaveChanges();
            }
        }

        // ─── Re-trigger AI Review ─────────────────────────────────────────────────

        /// <summary>
        /// Resets the AI fields and re-runs the review.
        /// Admin can call this from the Review page.
        /// </summary>
        public async Task RetriggerAiReviewAsync(int appId)
        {
            using (var db = new DBContextClass())
            {
                var app = db.Applications.Find(appId);
                if (app == null) return;

                app.AiReviewSummary = null;
                app.AiRecommendation = null;
                app.Status = ApplicationStatus.Pending;
                db.SaveChanges();
            }

            await TriggerAiReviewAsync(appId);
        }

        // ─── Admin Confirmation ───────────────────────────────────────────────────

        /// <summary>
        /// Admin makes the final decision. Updates status and creates an AdminReview record.
        /// </summary>
        public void AdminConfirm(int appId, string adminId, string decision, string notes, bool agreedWithAi)
        {
            using (var db = new DBContextClass())
            {
                var app = db.Applications.Find(appId);
                if (app == null) return;

                switch (decision)
                {
                    case "Approved": app.Status = ApplicationStatus.Approved; break;
                    case "Rejected": app.Status = ApplicationStatus.Rejected; break;
                    case "Flagged": app.Status = ApplicationStatus.Flagged; break;
                }

                db.AdminReviews.Add(new AdminReview
                {
                    AppId = appId,
                    AdminId = adminId,
                    Decision = decision,
                    AdminNotes = notes,
                    AgreedWithAi = agreedWithAi,
                    Date = DateTime.Now
                });

                db.SaveChanges();

                if (decision == "Approved")
                {
                    try
                    {
                        // Create registration record (no student account yet)
                        _regService.CreateRegistration(appId, app.StudentId, app.GradeApplying);

                        // Email parent to log in and complete registration
                        _email.SendApplicationApproved(
                            app.Parent.Contact,
                            app.Parent.Name,
                            app.Student.Name,
                            app.GradeApplying);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Post-approval setup failed: {ex.Message}");
                    }
                }

                // On Flagged — email parent to resubmit
                if (decision == "Flagged")
                {
                    try
                    {
                        _email.SendApplicationFlagged(
                            app.Parent.Contact,
                            app.Parent.Name,
                            app.Student.Name,
                            appId,
                            notes);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Flagged email failed: {ex.Message}");
                    }
                }
            }
        }

    }
}