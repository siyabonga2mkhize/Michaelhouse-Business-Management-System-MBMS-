
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [ParentOnly]
    public class StudentProfileController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private readonly BoardingProfileService boardingProfileService =
            new BoardingProfileService();


        // ============================================================
        // START
        // ============================================================

        public ActionResult Start(int appId)
        {
            var reg = GetRegistration(appId);

            if (reg == null)
                return HttpNotFound();

            EnsureProfile(reg);

            return RedirectToAction(
                "Basic",
                new { appId = appId }
            );
        }


        // ============================================================
        // STEP 1 - BASIC
        // ============================================================

        [HttpGet]
        public ActionResult Basic(int appId)
        {
            var reg = GetRegistration(appId);

            if (reg == null)
                return HttpNotFound();

            var profile = EnsureProfile(reg);

            return View(new StudentProfileBasicViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 1,
                StudentName = reg.Student.Name,

                PreferredName = profile.PreferredName,

                DateOfBirth =
                    reg.Student.DOB == default(DateTime)
                        ? DateTime.Today
                        : reg.Student.DOB,

                Gender = profile.Gender,

                HomeLanguage =
                    profile.HomeLanguage ??
                    reg.Student.HomeLanguage,

                Nationality = profile.Nationality
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Basic(
            StudentProfileBasicViewModel model)
        {
            var reg = GetRegistration(model.AppId);

            if (reg == null)
                return HttpNotFound();

            if (!ModelState.IsValid)
            {
                model.StudentName = reg.Student.Name;
                model.StudentId = reg.StudentId;
                model.Step = 1;

                return View(model);
            }

            var profile = EnsureProfile(reg);

            profile.PreferredName = model.PreferredName;
            profile.Gender = model.Gender;
            profile.HomeLanguage = model.HomeLanguage;
            profile.Nationality = model.Nationality;

            profile.CompletedStep =
                Math.Max(profile.CompletedStep, 1);

            // Keep existing Student fields synchronized.
            reg.Student.DOB = model.DateOfBirth;
            reg.Student.HomeLanguage = model.HomeLanguage;

            db.SaveChanges();

            return RedirectToAction(
                "Academic",
                new { appId = model.AppId }
            );
        }


        // ============================================================
        // STEP 2 - ACADEMIC
        // ============================================================

        [HttpGet]
        public ActionResult Academic(int appId)
        {
            var reg = GetRegistration(appId);

            if (reg == null)
                return HttpNotFound();

            var profile = EnsureProfile(reg);

            var registeredSubjects =
                GetRegisteredSubjectNames(reg.StudentId);

            return View(new StudentProfileAcademicViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 2,
                StudentName = reg.Student.Name,

                Grade = reg.GradeEnrolling.ToString(),

                Subjects = registeredSubjects,

                AcademicInterests =
                    profile.AcademicInterests,

                LearningStyle =
                    profile.LearningStyle,

                AcademicStream =
                    profile.AcademicStream
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Academic(
            StudentProfileAcademicViewModel model)
        {
            var reg = GetRegistration(model.AppId);

            if (reg == null)
                return HttpNotFound();

            if (!ModelState.IsValid)
            {
                model.StudentName = reg.Student.Name;
                model.StudentId = reg.StudentId;
                model.Step = 2;

                // Subjects are readonly in the view,
                // so reload them when returning because
                // they are not posted by the form.
                model.Subjects =
                    GetRegisteredSubjectNames(reg.StudentId);

                return View(model);
            }

            var profile = EnsureProfile(reg);

            profile.Grade =
                reg.GradeEnrolling.ToString();

            profile.ElectiveSubjects =
                GetRegisteredSubjectNames(reg.StudentId);

            profile.AcademicInterests =
                model.AcademicInterests;

            profile.LearningStyle =
                model.LearningStyle;

            profile.AcademicStream =
                model.AcademicStream;

            profile.CompletedStep =
                Math.Max(profile.CompletedStep, 2);

            // Keep Student record synchronized.
            reg.Student.GradeLevel =
                reg.GradeEnrolling;

            db.SaveChanges();

            return RedirectToAction(
                "Activities",
                new { appId = model.AppId }
            );
        }


        // ============================================================
        // STEP 3 - ACTIVITIES
        // ============================================================

        [HttpGet]
        public ActionResult Activities(int appId)
        {
            var reg = GetRegistration(appId);

            if (reg == null)
                return HttpNotFound();

            var profile = EnsureProfile(reg);

            /*
             * ClubsAndSocieties is an existing string in the database.
             *
             * Example:
             *
             * "Chess, Coding & Robotics, Music"
             *
             * The ViewModel needs a List<string> so the
             * checkboxes can automatically be checked.
             */

            var selectedClubs =
                SplitClubs(profile.ClubsAndSocieties);

            return View(new StudentProfileActivitiesViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 3,
                StudentName = reg.Student.Name,

                Sports = profile.Sports,

                Clubs = profile.ClubsAndSocieties,

                ClubsSelected = selectedClubs,

                LeadershipRoles =
                    profile.LeadershipRoles,

                CulturalActivities =
                    profile.CulturalActivities
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Activities(
            StudentProfileActivitiesViewModel model)
        {
            var reg = GetRegistration(model.AppId);

            if (reg == null)
                return HttpNotFound();

            var profile = EnsureProfile(reg);

            profile.Sports =
                model.Sports;

            /*
             * The Activities view posts:
             *
             * ClubsSelected = ["Chess", "Music", "Drama"]
             *
             * But StudentProfile.ClubsAndSocieties is
             * an existing string field.
             *
             * Therefore convert the list to:
             *
             * "Chess, Music, Drama"
             */

            profile.ClubsAndSocieties =
                JoinClubs(model.ClubsSelected);

            profile.LeadershipRoles =
                model.LeadershipRoles;

            profile.CulturalActivities =
                model.CulturalActivities;

            profile.CompletedStep =
                Math.Max(profile.CompletedStep, 3);

            db.SaveChanges();

            return RedirectToAction(
                "Medical",
                new { appId = model.AppId }
            );
        }


        // ============================================================
        // STEP 4 - MEDICAL
        // ============================================================

        [HttpGet]
        public ActionResult Medical(int appId)
        {
            var reg = GetRegistration(appId);

            if (reg == null)
                return HttpNotFound();

            var profile = EnsureProfile(reg);

            return View(new StudentProfileMedicalViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 4,
                StudentName = reg.Student.Name,

                Allergies =
                    profile.Allergies,

                Disabilities =
                    profile.Disabilities,

                MedicalConditions =
                    profile.MedicalConditions ??
                    reg.Student.MedicalConditions,

                EmergencyMedication =
                    profile.EmergencyMedication,

                MobilityRequirements =
                    profile.MobilityRequirements,

                AccessibilityRequired =
                    profile.AccessibilityRequired,

                AccessibilityNotes =
                    profile.AccessibilityNotes,

                MedicalAccommodationRequired =
                    profile.MedicalAccommodationRequired,

                MedicalAccommodationNotes =
                    profile.MedicalAccommodationNotes
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Medical(
            StudentProfileMedicalViewModel model)
        {
            var reg = GetRegistration(model.AppId);

            if (reg == null)
                return HttpNotFound();

            if (!ModelState.IsValid)
            {
                model.StudentName = reg.Student.Name;
                model.StudentId = reg.StudentId;
                model.Step = 4;

                return View(model);
            }

            var profile = EnsureProfile(reg);

            profile.Allergies =
                model.Allergies;

            profile.Disabilities =
                model.Disabilities;

            profile.MedicalConditions =
                model.MedicalConditions;

            profile.EmergencyMedication =
                model.EmergencyMedication;

            profile.MobilityRequirements =
                model.MobilityRequirements;

            profile.AccessibilityRequired =
                model.AccessibilityRequired;

            profile.AccessibilityNotes =
                model.AccessibilityNotes;

            profile.MedicalAccommodationRequired =
                model.MedicalAccommodationRequired;

            profile.MedicalAccommodationNotes =
                model.MedicalAccommodationNotes;

            profile.CompletedStep =
                Math.Max(profile.CompletedStep, 4);

            // Keep the existing Student field synchronized.
            reg.Student.MedicalConditions =
                model.MedicalConditions;

            db.SaveChanges();

            return RedirectToAction(
                "Personality",
                new { appId = model.AppId }
            );
        }


        // ============================================================
        // STEP 5 - PERSONALITY
        // ============================================================

        [HttpGet]
        public ActionResult Personality(int appId)
        {
            var reg = GetRegistration(appId);

            if (reg == null)
                return HttpNotFound();

            var profile = EnsureProfile(reg);

            return View(new StudentProfilePersonalityViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 5,
                StudentName = reg.Student.Name,

                EnvironmentPreference =
                    profile.EnvironmentPreference,

                RoutinePreference =
                    profile.RoutinePreference,

                ActivityPreference =
                    profile.ActivityPreference,

                StudyPreference =
                    profile.StudyPreference,

                SocialPreference =
                    profile.SocialPreference
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Personality(
            StudentProfilePersonalityViewModel model)
        {
            var reg = GetRegistration(model.AppId);

            if (reg == null)
                return HttpNotFound();

            if (!ModelState.IsValid)
            {
                model.StudentName = reg.Student.Name;
                model.StudentId = reg.StudentId;
                model.Step = 5;

                return View(model);
            }

            var profile = EnsureProfile(reg);

            profile.EnvironmentPreference =
                model.EnvironmentPreference;

            profile.RoutinePreference =
                model.RoutinePreference;

            profile.ActivityPreference =
                model.ActivityPreference;

            profile.StudyPreference =
                model.StudyPreference;

            profile.SocialPreference =
                model.SocialPreference;

            profile.CompletedStep = 5;

            profile.IsProfileComplete = true;

            profile.ProfileCompletedAt =
                DateTime.UtcNow;

            /*
             * Generate the boarding profile only after
             * all five wizard steps have been completed.
             */
            boardingProfileService.GenerateBoardingProfile(
                profile
            );

            db.SaveChanges();

            TempData["Success"] =
                "Student profile completed.";

            return RedirectToAction(
                "Confirm",
                "Registration",
                new { appId = model.AppId }
            );
        }


        // ============================================================
        // REGISTRATION LOOKUP
        // ============================================================

        private Registration GetRegistration(int appId)
        {
            if (Session["ParentId"] == null)
                return null;

            var parentId =
                (int)Session["ParentId"];

            return db.Registrations
                .Include(r => r.Student)
                .Include(r => r.Student.StudentProfile)
                .Include(r => r.Application)
                .FirstOrDefault(
                    r =>
                        r.AppId == appId &&
                        r.Student.ParentId == parentId
                );
        }


        // ============================================================
        // ENSURE STUDENT PROFILE
        // ============================================================

        private StudentProfile EnsureProfile(
            Registration registration)
        {
            var profile =
                registration.Student.StudentProfile
                ??
                db.StudentProfiles.Find(
                    registration.StudentId
                );

            if (profile != null)
                return profile;

            profile = new StudentProfile
            {
                StudentId =
                    registration.StudentId,

                Grade =
                    registration.GradeEnrolling.ToString(),

                HomeLanguage =
                    registration.Student.HomeLanguage,

                MedicalConditions =
                    registration.Student.MedicalConditions,

                CompletedStep = 0,

                IsProfileComplete = false
            };

            db.StudentProfiles.Add(profile);

            registration.Student.StudentProfile =
                profile;

            db.SaveChanges();

            return profile;
        }


        // ============================================================
        // REGISTERED SUBJECTS
        // ============================================================

        private string GetRegisteredSubjectNames(
            int studentId)
        {
            return string.Join(
                ", ",
                db.StudentSubjects
                    .Include(ss => ss.Subject)
                    .Where(
                        ss =>
                            ss.StudentId == studentId
                    )
                    .OrderBy(
                        ss => ss.Subject.Name
                    )
                    .Select(
                        ss => ss.Subject.Name
                    )
                    .ToList()
            );
        }


        // ============================================================
        // CLUB HELPERS
        // ============================================================

        private List<string> SplitClubs(
            string clubs)
        {
            if (string.IsNullOrWhiteSpace(clubs))
                return new List<string>();

            return clubs
                .Split(
                    new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(
                    club => club.Trim()
                )
                .Where(
                    club => !string.IsNullOrWhiteSpace(club)
                )
                .ToList();
        }


        private string JoinClubs(
            IEnumerable<string> clubs)
        {
            if (clubs == null)
                return string.Empty;

            return string.Join(
                ", ",
                clubs
                    .Where(
                        club =>
                            !string.IsNullOrWhiteSpace(club)
                    )
                    .Select(
                        club => club.Trim()
                    )
                    .Distinct()
                    .ToList()
            );
        }


        // ============================================================
        // DISPOSE
        // ============================================================

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
