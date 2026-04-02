using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;


namespace Michaelhouse.Services
{
    public class RegistrationService
    {
        // ─── Subject Lists ────────────────────────────────────────────────────────

        // Grade 8 & 9 — fixed, no choice
        public static readonly List<string> Grade8And9Subjects = new List<string>
        {
            "English Home Language",
            "Afrikaans First Additional Language",
            "Zulu First Additional Language",
            "Mathematics",
            "Life Orientation",
            "Natural Sciences",
            "Social Sciences",
            "Technology",
            "Economic and Management Sciences",
            "Creative Arts"
        };

        // Grade 10-12 — always compulsory
        public static readonly List<string> CompulsorySubjects = new List<string>
        {
            "English Home Language",
            "Life Orientation"
        };

        // Grade 10-12 — pick ONE language
        public static readonly List<string> ElectiveGroupA = new List<string>
        {
            "Afrikaans First Additional Language",
            "Zulu First Additional Language",
            "French",
            "German"
        };

        // Grade 10-12 — pick TWO electives
        public static readonly List<string> ElectiveGroupB = new List<string>
        {
            "Mathematics",
            "Mathematical Literacy",
            "Physical Sciences",
            "Life Sciences",
            "Geography",
            "History",
            "Business Studies",
            "Economics",
            "Accounting",
            "Computer Applications Technology",
            "Information Technology",
            "Dramatic Arts",
            "Visual Arts",
            "Music"
        };

        // ─── Create Registration Record ───────────────────────────────────────────

        /// <summary>
        /// Called when admin approves an application.
        /// Creates a Registration record so the parent can start the registration process.
        /// Does NOT create a student account yet.
        /// </summary>
        public Registration CreateRegistration(int appId, int studentId, int grade)
        {
            using (var db = new DBContextClass())
            {
                // Don't create duplicate
                var existing = db.Registrations.FirstOrDefault(r => r.AppId == appId);
                if (existing != null) return existing;

                var registration = new Registration
                {
                    AppId = appId,
                    StudentId = studentId,
                    GradeEnrolling = grade,
                    Status = RegistrationStatus.Pending,
                    CreatedAt = DateTime.Now
                };

                db.Registrations.Add(registration);
                db.SaveChanges();
                return registration;
            }
        }

        // ─── Get Registration ─────────────────────────────────────────────────────

        public Registration GetRegistrationForApp(int appId)
        {
            using (var db = new DBContextClass())
            {
                return db.Registrations
                    .Include("Student")
                    .Include("Application")
                    .FirstOrDefault(r => r.AppId == appId);
            }
        }

        public Registration GetRegistrationById(int registrationId)
        {
            using (var db = new DBContextClass())
            {
                return db.Registrations
                    .Include("Student")
                    .Include("Application")
                    .FirstOrDefault(r => r.RegistrationId == registrationId);
            }
        }

        // ─── Save Subject Selections (Grade 10-12) ────────────────────────────────

        /// <summary>
        /// Parent selects subjects for Grade 10-12 students.
        /// Validates: 1 from Group A, 2 from Group B.
        /// </summary>
        public (bool Success, string Error) SaveSubjectSelections(
            int studentId,
            int registrationId,
            string groupASubject,
            List<string> groupBSubjects)
        {
            if (string.IsNullOrEmpty(groupASubject))
                return (false, "Please select one language from Group A.");

            if (!ElectiveGroupA.Contains(groupASubject))
                return (false, "Invalid Group A subject selected.");

            if (groupBSubjects == null || groupBSubjects.Count != 2)
                return (false, "Please select exactly 2 subjects from Group B.");

            foreach (var sub in groupBSubjects)
                if (!ElectiveGroupB.Contains(sub))
                    return (false, $"Invalid subject: {sub}");

            using (var db = new DBContextClass())
            {
                // Remove previous selections
                var existing = db.StudentSubjects
                    .Where(ss => ss.StudentId == studentId)
                    .ToList();
                db.StudentSubjects.RemoveRange(existing);
                db.SaveChanges();

                // Add compulsory
                foreach (var name in CompulsorySubjects)
                    AddSubjectByName(studentId, name, false, db);

                // Add Group A
                AddSubjectByName(studentId, groupASubject, true, db);

                // Add Group B
                foreach (var name in groupBSubjects)
                    AddSubjectByName(studentId, name, true, db);

                db.SaveChanges();

                // Update status
                var reg = db.Registrations.Find(registrationId);
                if (reg != null)
                {
                    reg.Status = RegistrationStatus.SubjectsSelected;
                    db.SaveChanges();
                }
            }

            return (true, null);
        }

        // ─── Assign Grade 8 & 9 Subjects ─────────────────────────────────────────

        public void AssignGrade8And9Subjects(int studentId)
        {
            using (var db = new DBContextClass())
            {
                // Remove previous
                var existing = db.StudentSubjects
                    .Where(ss => ss.StudentId == studentId)
                    .ToList();
                db.StudentSubjects.RemoveRange(existing);
                db.SaveChanges();

                foreach (var name in Grade8And9Subjects)
                    AddSubjectByName(studentId, name, false, db);

                db.SaveChanges();
            }
        }

        // ─── Complete Registration ────────────────────────────────────────────────

        /// <summary>
        /// Parent confirms and submits registration.
        /// This triggers student account creation.
        /// </summary>
        public void CompleteRegistration(int registrationId)
        {
            using (var db = new DBContextClass())
            {
                var reg = db.Registrations.Find(registrationId);
                if (reg == null) return;

                reg.Status = RegistrationStatus.Completed;
                reg.CompletedAt = DateTime.Now;
                db.SaveChanges();
            }
        }

        // ─── Helper ───────────────────────────────────────────────────────────────

        private void AddSubjectByName(int studentId, string subjectName, bool isElective, DBContextClass db)
        {
            var subject = db.Subjects.FirstOrDefault(s => s.SubjectName == subjectName);
            if (subject == null)
            {
                subject = new Subject { SubjectName = subjectName };
                db.Subjects.Add(subject);
                db.SaveChanges();
            }

            bool exists = db.StudentSubjects
                .Any(ss => ss.StudentId == studentId && ss.SubjectId == subject.SubjectId);

            if (!exists)
            {
                db.StudentSubjects.Add(new StudentSubject
                {
                    StudentId = studentId,
                    SubjectId = subject.SubjectId,
                    IsElective = isElective
                });
            }
        }
    }
}