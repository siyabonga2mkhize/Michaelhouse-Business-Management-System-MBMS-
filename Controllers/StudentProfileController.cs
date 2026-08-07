using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [ParentOnly]
    public class StudentProfileController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private readonly BoardingProfileService boardingProfileService = new BoardingProfileService();

        public ActionResult Start(int appId)
        {
            var reg = GetRegistration(appId);
            if (reg == null) return HttpNotFound();

            EnsureProfile(reg);
            return RedirectToAction("Basic", new { appId });
        }

        public ActionResult Basic(int appId)
        {
            var reg = GetRegistration(appId);
            if (reg == null) return HttpNotFound();

            var profile = EnsureProfile(reg);
            return View(new StudentProfileBasicViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 1,
                StudentName = reg.Student.Name,
                PreferredName = profile.PreferredName,
                DateOfBirth = reg.Student.DOB == default(DateTime) ? DateTime.Today : reg.Student.DOB,
                Gender = profile.Gender,
                HomeLanguage = profile.HomeLanguage ?? reg.Student.HomeLanguage,
                Nationality = profile.Nationality
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Basic(StudentProfileBasicViewModel model)
        {
            var reg = GetRegistration(model.AppId);
            if (reg == null) return HttpNotFound();

            if (!ModelState.IsValid)
            {
                model.StudentName = reg.Student.Name;
                model.Step = 1;
                return View(model);
            }

            var profile = EnsureProfile(reg);
            profile.PreferredName = model.PreferredName;
            profile.Gender = model.Gender;
            profile.HomeLanguage = model.HomeLanguage;
            profile.Nationality = model.Nationality;
            profile.CompletedStep = Math.Max(profile.CompletedStep, 1);

            reg.Student.DOB = model.DateOfBirth;
            reg.Student.HomeLanguage = model.HomeLanguage;
            db.SaveChanges();

            return RedirectToAction("Academic", new { appId = model.AppId });
        }

        public ActionResult Academic(int appId)
        {
            var reg = GetRegistration(appId);
            if (reg == null) return HttpNotFound();

            var profile = EnsureProfile(reg);
            return View(new StudentProfileAcademicViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 2,
                StudentName = reg.Student.Name,
                Grade = profile.Grade ?? reg.GradeEnrolling.ToString(),
                Subjects = profile.ElectiveSubjects,
                AcademicInterests = profile.AcademicInterests,
                LearningStyle = profile.LearningStyle,
                AcademicStream = profile.AcademicStream
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Academic(StudentProfileAcademicViewModel model)
        {
            var reg = GetRegistration(model.AppId);
            if (reg == null) return HttpNotFound();

            if (!ModelState.IsValid)
            {
                model.StudentName = reg.Student.Name;
                model.Step = 2;
                return View(model);
            }

            var profile = EnsureProfile(reg);
            profile.Grade = model.Grade;
            profile.ElectiveSubjects = model.Subjects;
            profile.AcademicInterests = model.AcademicInterests;
            profile.LearningStyle = model.LearningStyle;
            profile.AcademicStream = model.AcademicStream;
            profile.CompletedStep = Math.Max(profile.CompletedStep, 2);

            if (int.TryParse(model.Grade, out var gradeLevel))
                reg.Student.GradeLevel = gradeLevel;

            db.SaveChanges();
            return RedirectToAction("Activities", new { appId = model.AppId });
        }

        public ActionResult Activities(int appId)
        {
            var reg = GetRegistration(appId);
            if (reg == null) return HttpNotFound();

            var profile = EnsureProfile(reg);
            return View(new StudentProfileActivitiesViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 3,
                StudentName = reg.Student.Name,
                Sports = profile.Sports,
                Clubs = profile.ClubsAndSocieties,
                LeadershipRoles = profile.LeadershipRoles,
                CulturalActivities = profile.CulturalActivities
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Activities(StudentProfileActivitiesViewModel model)
        {
            var reg = GetRegistration(model.AppId);
            if (reg == null) return HttpNotFound();

            var profile = EnsureProfile(reg);
            profile.Sports = model.Sports;
            profile.ClubsAndSocieties = model.Clubs;
            profile.LeadershipRoles = model.LeadershipRoles;
            profile.CulturalActivities = model.CulturalActivities;
            profile.CompletedStep = Math.Max(profile.CompletedStep, 3);
            db.SaveChanges();

            return RedirectToAction("Medical", new { appId = model.AppId });
        }

        public ActionResult Medical(int appId)
        {
            var reg = GetRegistration(appId);
            if (reg == null) return HttpNotFound();

            var profile = EnsureProfile(reg);
            return View(new StudentProfileMedicalViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 4,
                StudentName = reg.Student.Name,
                Allergies = profile.Allergies,
                Disabilities = profile.Disabilities,
                MedicalConditions = profile.MedicalConditions ?? reg.Student.MedicalConditions,
                EmergencyMedication = profile.EmergencyMedication,
                MobilityRequirements = profile.MobilityRequirements,
                AccessibilityRequired = profile.AccessibilityRequired,
                AccessibilityNotes = profile.AccessibilityNotes,
                MedicalAccommodationRequired = profile.MedicalAccommodationRequired,
                MedicalAccommodationNotes = profile.MedicalAccommodationNotes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Medical(StudentProfileMedicalViewModel model)
        {
            var reg = GetRegistration(model.AppId);
            if (reg == null) return HttpNotFound();

            var profile = EnsureProfile(reg);
            profile.Allergies = model.Allergies;
            profile.Disabilities = model.Disabilities;
            profile.MedicalConditions = model.MedicalConditions;
            profile.EmergencyMedication = model.EmergencyMedication;
            profile.MobilityRequirements = model.MobilityRequirements;
            profile.AccessibilityRequired = model.AccessibilityRequired;
            profile.AccessibilityNotes = model.AccessibilityNotes;
            profile.MedicalAccommodationRequired = model.MedicalAccommodationRequired;
            profile.MedicalAccommodationNotes = model.MedicalAccommodationNotes;
            profile.CompletedStep = Math.Max(profile.CompletedStep, 4);

            reg.Student.MedicalConditions = model.MedicalConditions;
            db.SaveChanges();

            return RedirectToAction("Personality", new { appId = model.AppId });
        }

        public ActionResult Personality(int appId)
        {
            var reg = GetRegistration(appId);
            if (reg == null) return HttpNotFound();

            var profile = EnsureProfile(reg);
            return View(new StudentProfilePersonalityViewModel
            {
                AppId = appId,
                StudentId = reg.StudentId,
                Step = 5,
                StudentName = reg.Student.Name,
                EnvironmentPreference = profile.EnvironmentPreference,
                RoutinePreference = profile.RoutinePreference,
                ActivityPreference = profile.ActivityPreference,
                StudyPreference = profile.StudyPreference,
                SocialPreference = profile.SocialPreference
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Personality(StudentProfilePersonalityViewModel model)
        {
            var reg = GetRegistration(model.AppId);
            if (reg == null) return HttpNotFound();

            if (!ModelState.IsValid)
            {
                model.StudentName = reg.Student.Name;
                model.Step = 5;
                return View(model);
            }

            var profile = EnsureProfile(reg);
            profile.EnvironmentPreference = model.EnvironmentPreference;
            profile.RoutinePreference = model.RoutinePreference;
            profile.ActivityPreference = model.ActivityPreference;
            profile.StudyPreference = model.StudyPreference;
            profile.SocialPreference = model.SocialPreference;
            profile.CompletedStep = 5;
            profile.IsProfileComplete = true;
            profile.ProfileCompletedAt = DateTime.UtcNow;

            boardingProfileService.GenerateBoardingProfile(profile);
            db.SaveChanges();

            TempData["Success"] = "Student profile completed.";
            return RedirectToAction("Confirm", "Registration", new { appId = model.AppId });
        }

        private Registration GetRegistration(int appId)
        {
            var parentId = (int)Session["ParentId"];
            return db.Registrations
                .Include(r => r.Student)
                .Include(r => r.Student.StudentProfile)
                .Include(r => r.Application)
                .FirstOrDefault(r => r.AppId == appId && r.Student.ParentId == parentId);
        }

        private StudentProfile EnsureProfile(Registration registration)
        {
            var profile = registration.Student.StudentProfile ?? db.StudentProfiles.Find(registration.StudentId);
            if (profile != null)
                return profile;

            profile = new StudentProfile
            {
                StudentId = registration.StudentId,
                Grade = registration.GradeEnrolling.ToString(),
                HomeLanguage = registration.Student.HomeLanguage,
                MedicalConditions = registration.Student.MedicalConditions,
                CompletedStep = 0,
                IsProfileComplete = false
            };

            db.StudentProfiles.Add(profile);
            registration.Student.StudentProfile = profile;
            db.SaveChanges();
            return profile;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
