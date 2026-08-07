using Microsoft.EntityFrameworkCore;
using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Michaelhouse.Services
{
    public class RegistrationService
    {
        // ═══════════════════════════════════════════════════════════════════════
        // GRADE 8 & 9 — Fixed curriculum, no stream, no choice
        // ═══════════════════════════════════════════════════════════════════════

        public static readonly List<string> Grade8And9Subjects = new List<string>
        {
            "English Home Language",
            "Afrikaans First Additional Language",
            "isiZulu First Additional Language",
            "Mathematics",
            "Life Orientation",
            "Natural Sciences",
            "Social Sciences",
            "Technology",
            "Economic and Management Sciences",
            "Creative Arts"
        };

        // ═══════════════════════════════════════════════════════════════════════
        // GRADE 10-12 — Compulsory (all students)
        // ═══════════════════════════════════════════════════════════════════════

        public static readonly List<string> CompulsorySubjects = new List<string>
        {
            "English Home Language",
            "Life Orientation"
        };

        // ═══════════════════════════════════════════════════════════════════════
        // GRADE 10-12 — Language choices (pick ONE)
        // ═══════════════════════════════════════════════════════════════════════

        public static readonly List<string> LanguageChoices = new List<string>
        {
            "Afrikaans First Additional Language",
            "isiZulu First Additional Language",
            "French",
            "Conversational isiZulu (E Block)",
            "Further Studies English"
        };

        // ═══════════════════════════════════════════════════════════════════════
        // GRADE 10-12 — Mathematics options (pick ONE)
        // ═══════════════════════════════════════════════════════════════════════

        public static readonly List<string> MathsOptions = new List<string>
        {
            "Mathematics",
            "Mathematical Literacy"
        };

        // ═══════════════════════════════════════════════════════════════════════
        // GRADE 10-12 — Streams and their subjects
        // Mathematics: ALL streams available
        // Mathematical Literacy: only Arts, Human & Social, Commerce
        // ═══════════════════════════════════════════════════════════════════════

        public static readonly Dictionary<AcademicStream, List<string>> StreamSubjects =
            new Dictionary<AcademicStream, List<string>>
        {
            {
                AcademicStream.ArtsAndCulture, new List<string>
                {
                    "Music",
                    "Visual Arts",
                    "Dramatic Arts"
                }
            },
            {
                AcademicStream.HumanAndSocialStudies, new List<string>
                {
                    "Geography",
                    "History",
                    "Tourism"
                    // Life Orientation is compulsory — not listed here again
                }
            },
            {
                AcademicStream.Sciences, new List<string>
                {
                    "Physics",
                    "Chemistry",
                    "Life Sciences (Biology)"
                }
            },
            {
                AcademicStream.Commerce, new List<string>
                {
                    "Accounting",
                    "Economics",
                    "Business Studies"
                }
            },
            {
                AcademicStream.EngineeringAndTechnology, new List<string>
                {
                    "Engineering Graphics and Design",
                    "Computer Applications Technology",
                    "Information Technology",
                    "Coding and Robotics"
                }
            }
        };

        // Streams available to Mathematical Literacy students
        public static readonly List<AcademicStream> MathLiteracyAllowedStreams =
            new List<AcademicStream>
        {
            AcademicStream.ArtsAndCulture,
            AcademicStream.HumanAndSocialStudies,
            AcademicStream.Commerce
        };

        // Minimum stream subjects needed
        // Total = 2 compulsory + 1 language + 1 maths + 3 stream = 7
        public const int RequiredStreamSubjects = 3;
        public const int TotalSubjects = 7;

        // ═══════════════════════════════════════════════════════════════════════
        // REGISTRATION METHODS
        // ═══════════════════════════════════════════════════════════════════════

        public Registration CreateRegistration(int appId, int studentId, int grade)
        {
            using (var db = DbContextFactory.Create())
            {
                var existing = db.Registrations.FirstOrDefault(r => r.AppId == appId);
                if (existing != null) return existing;

                var reg = new Registration
                {
                    AppId = appId,
                    StudentId = studentId,
                    GradeEnrolling = grade,
                    Status = RegistrationStatus.Pending,
                    CreatedAt = DateTime.Now
                };

                db.Registrations.Add(reg);
                db.SaveChanges();
                return reg;
            }
        }

        public Registration GetRegistrationForApp(int appId)
        {
            using (var db = DbContextFactory.Create())
            {
                return db.Registrations
                    .Include("Student")
                    .Include("Application")
                    .FirstOrDefault(r => r.AppId == appId);
            }
        }

        public Registration GetRegistrationById(int registrationId)
        {
            using (var db = DbContextFactory.Create())
            {
                return db.Registrations
                    .Include("Student")
                    .Include("Application")
                    .FirstOrDefault(r => r.RegistrationId == registrationId);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GRADE 8 & 9 — Auto-assign subjects
        // ═══════════════════════════════════════════════════════════════════════

        public void AssignGrade8And9Subjects(int studentId)
        {
            using (var db = DbContextFactory.Create())
            {
                var existing = db.StudentSubjects
                    .Where(ss => ss.StudentId == studentId).ToList();
                db.StudentSubjects.RemoveRange(existing);
                db.SaveChanges();

                foreach (var name in Grade8And9Subjects)
                    AddOrGetSubject(studentId, name, AcademicStream.None, true, db);

                db.SaveChanges();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GRADE 10-12 — Save full subject selection
        // Total: 2 compulsory + 1 language + 1 maths + 3 stream = 7
        // ═══════════════════════════════════════════════════════════════════════

        public (bool Success, string Error) SaveSubjectSelections(
            int studentId,
            int registrationId,
            int grade,
            string languageChoice,
            string mathsChoice,
            AcademicStream stream,
            List<string> streamSubjects)
        {
            // ── Validate language ─────────────────────────────────────────────
            if (string.IsNullOrEmpty(languageChoice))
                return (false, "Please select a First Additional Language.");
            if (!LanguageChoices.Contains(languageChoice))
                return (false, "Invalid language selection.");

            // ── Validate maths ────────────────────────────────────────────────
            if (string.IsNullOrEmpty(mathsChoice))
                return (false, "Please select Mathematics or Mathematical Literacy.");
            if (!MathsOptions.Contains(mathsChoice))
                return (false, "Invalid maths selection.");

            bool takesMaths = mathsChoice == "Mathematics";

            // ── Validate stream eligibility ───────────────────────────────────
            if (stream == AcademicStream.None)
                return (false, "Please select an academic stream.");

            if (!takesMaths && !MathLiteracyAllowedStreams.Contains(stream))
                return (false,
                    $"Students taking Mathematical Literacy cannot choose the " +
                    $"{GetStreamName(stream)} stream. " +
                    $"Please choose Arts & Culture, Human & Social Studies, or Commerce.");

            // ── Validate stream subjects ──────────────────────────────────────
            if (streamSubjects == null || streamSubjects.Count != RequiredStreamSubjects)
                return (false, $"Please select exactly {RequiredStreamSubjects} subjects from your stream.");

            var validStreamSubs = StreamSubjects[stream];
            foreach (var sub in streamSubjects)
                if (!validStreamSubs.Contains(sub))
                    return (false, $"'{sub}' is not a valid subject for the {GetStreamName(stream)} stream.");

            // ── Save ──────────────────────────────────────────────────────────
            using (var db = DbContextFactory.Create())
            {
                // Remove existing selections
                var existing = db.StudentSubjects
                    .Where(ss => ss.StudentId == studentId).ToList();
                db.StudentSubjects.RemoveRange(existing);
                db.SaveChanges();

                // 1. Compulsory subjects
                foreach (var name in CompulsorySubjects)
                    AddOrGetSubject(studentId, name, AcademicStream.None, true, db);

                // 2. Language choice
                AddOrGetSubject(studentId, languageChoice, AcademicStream.None, false, db);

                // 3. Maths choice
                AddOrGetSubject(studentId, mathsChoice, AcademicStream.None, false, db);

                // 4. Stream subjects
                foreach (var name in streamSubjects)
                    AddOrGetSubject(studentId, name, stream, false, db);

                db.SaveChanges();

                // Save stream enrolment record
                var existingStream = db.StreamEnrolments
                    .FirstOrDefault(se => se.StudentId == studentId &&
                                          se.RegistrationId == registrationId);

                if (existingStream != null)
                {
                    existingStream.Stream = stream;
                    existingStream.TakesMathematics = takesMaths;
                    existingStream.Grade = grade;
                    existingStream.EnrolledAt = DateTime.Now;
                }
                else
                {
                    db.StreamEnrolments.Add(new StreamEnrolment
                    {
                        StudentId = studentId,
                        RegistrationId = registrationId,
                        Grade = grade,
                        Stream = stream,
                        TakesMathematics = takesMaths,
                        EnrolledAt = DateTime.Now
                    });
                }

                db.SaveChanges();

                // Update registration status
                var reg = db.Registrations.Find(registrationId);
                if (reg != null)
                {
                    reg.Status = RegistrationStatus.SubjectsSelected;
                    db.SaveChanges();
                }
            }

            return (true, null);
        }

        public void CompleteRegistration(int registrationId)
        {
            using (var db = DbContextFactory.Create())
            {
                var reg = db.Registrations.Find(registrationId);
                if (reg == null) return;
                reg.Status = RegistrationStatus.Completed;
                reg.CompletedAt = DateTime.Now;
                db.SaveChanges();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        public static string GetStreamName(AcademicStream stream)
        {
            switch (stream)
            {
                case AcademicStream.ArtsAndCulture: return "Arts & Culture";
                case AcademicStream.HumanAndSocialStudies: return "Human & Social Studies";
                case AcademicStream.Sciences: return "Sciences";
                case AcademicStream.Commerce: return "Commerce";
                case AcademicStream.EngineeringAndTechnology: return "Engineering & Technology";
                default: return "None";
            }
        }

        public static string GetStreamIcon(AcademicStream stream)
        {
            switch (stream)
            {
                case AcademicStream.ArtsAndCulture: return "bi-palette";
                case AcademicStream.HumanAndSocialStudies: return "bi-globe";
                case AcademicStream.Sciences: return "bi-flask";
                case AcademicStream.Commerce: return "bi-graph-up";
                case AcademicStream.EngineeringAndTechnology: return "bi-cpu";
                default: return "bi-book";
            }
        }

        private void AddOrGetSubject(
            int studentId, string subjectName,
            AcademicStream stream, bool isCompulsory,
            DBContextClass db)
        {
            var subject = db.Subjects.FirstOrDefault(s => s.Name == subjectName);
            if (subject == null)
            {
                subject = new Subject
                {
                    Name = subjectName,
                    Stream = stream,
                    IsCompulsory = isCompulsory
                };
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
                    Stream = stream,
                    IsCompulsory = isCompulsory
                });
            }
        }
        // Get all subjects assigned to a teacher (via TeacherSubjectGrade)
        public List<Subject> GetSubjectsForTeacher(int teacherId)
        {
            using (var db = DbContextFactory.Create())
            {
                return db.TeacherSubjectGrades
                    .Where(tsg => tsg.TeacherId == teacherId)
                    .Select(tsg => tsg.Subject)
                    .Distinct()
                    .ToList();
            }
        }
    }
}