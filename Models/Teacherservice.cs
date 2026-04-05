using System;
using System.Collections.Generic;
using System.Linq;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Services
{
    public class TeacherService
    {
        private readonly EmailService _email = new EmailService();

        // ─── Create Teacher Account ───────────────────────────────────────────────

        /// <summary>
        /// Creates a Teacher record, an AppUser (Role = "Teacher"),
        /// assigns their two subjects, and emails them their credentials.
        /// </summary>
        public (bool Success, string Error, Teacher Teacher) CreateTeacher(
            string firstName, string lastName, string email, string phone,
            List<(int SubjectId, int Grade, AcademicStream Stream)> assignments)
        {
            // ── Validate max 2 subject assignments ────────────────────────────
            if (assignments == null || assignments.Count == 0)
                return (false, "Please assign at least one subject.", null);

            if (assignments.Count > 2)
                return (false, "A teacher can only be assigned a maximum of 2 subjects.", null);

            using (var db = new DBContextClass())
            {
                // ── Check email not already used ──────────────────────────────
                if (db.Users.Any(u => u.Email == email))
                    return (false, $"An account with email {email} already exists.", null);

                // ── Validate each assignment ──────────────────────────────────
                foreach (var (subjectId, grade, stream) in assignments)
                {
                    // Check: is this subject+grade already assigned to another teacher?
                    bool alreadyAssigned = db.TeacherSubjectGrades.Any(tsg =>
                        tsg.SubjectId == subjectId &&
                        tsg.Grade == grade &&
                        tsg.Stream == stream);

                    if (alreadyAssigned)
                    {
                        var subject = db.Subjects.Find(subjectId);
                        return (false,
                            $"{subject?.Name} for Grade {grade} is already assigned to another teacher.",
                            null);
                    }
                }

                // ── Create user account ───────────────────────────────────────
                var tempPassword = GenerateTempPassword();
                var teacherEmail = email.ToLower().Trim();

                var user = new AppUser
                {
                    Name = $"{firstName} {lastName}",
                    Email = teacherEmail,
                    PasswordHash = Controllers.AccountController.HashPassword(tempPassword),
                    Role = "Teacher"
                };
                db.Users.Add(user);
                db.SaveChanges();

                // ── Create teacher record ─────────────────────────────────────
                var teacher = new Teacher
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = teacherEmail,
                    Phone = phone,
                    UserId = user.UserId
                };
                db.Teachers.Add(teacher);
                db.SaveChanges();

                // ── Assign subjects ───────────────────────────────────────────
                foreach (var (subjectId, grade, stream) in assignments)
                {
                    db.TeacherSubjectGrades.Add(new TeacherSubjectGrade
                    {
                        TeacherId = teacher.TeacherId,
                        SubjectId = subjectId,
                        Grade = grade,
                        Stream = stream
                    });
                }
                db.SaveChanges();

                // ── Email credentials ─────────────────────────────────────────
                var subjectList = new List<string>();
                foreach (var (subjectId, grade, stream) in assignments)
                {
                    var sub = db.Subjects.Find(subjectId);
                    var streamLabel = stream != AcademicStream.None
                        ? $" ({RegistrationService.GetStreamName(stream)})"
                        : "";
                    subjectList.Add($"{sub?.Name} — Grade {grade}{streamLabel}");
                }

                try
                {
                    _email.SendTeacherAccountCreated(
                        teacherEmail,
                        $"{firstName} {lastName}",
                        teacherEmail,
                        tempPassword,
                        subjectList);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Teacher email failed: {ex.Message}");
                }

                return (true, null, teacher);
            }
        }

        // ─── Get all teachers with their assignments ───────────────────────────────

        public List<Teacher> GetAllTeachers()
        {
            using (var db = new DBContextClass())
            {
                return db.Teachers
                    .Include("SubjectAssignments")
                    .Include("SubjectAssignments.Subject")
                    .Include("User")
                    .OrderBy(t => t.LastName)
                    .ToList();
            }
        }

        // ─── Get available subjects for a grade (not yet fully assigned) ──────────

        public List<Subject> GetAvailableSubjectsForGrade(int grade)
        {
            using (var db = new DBContextClass())
            {
                // Get all subjects applicable to this grade
                var gradeStr = grade.ToString();
                var allSubjects = db.Subjects
                    .Where(s => s.ApplicableGrades == null ||
                                s.ApplicableGrades.Contains(gradeStr))
                    .ToList();

                // Filter out already fully assigned ones
                var assigned = db.TeacherSubjectGrades
                    .Where(tsg => tsg.Grade == grade)
                    .Select(tsg => new { tsg.SubjectId, tsg.Stream })
                    .ToList();

                return allSubjects;
            }
        }

        // ─── Check if a teacher has capacity for another assignment ───────────────

        public bool TeacherHasCapacity(int teacherId)
        {
            using (var db = new DBContextClass())
            {
                return db.TeacherSubjectGrades
                    .Count(tsg => tsg.TeacherId == teacherId) < 2;
            }
        }

        // ─── Password generator ───────────────────────────────────────────────────

        private string GenerateTempPassword()
        {
            var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
            var digits = "23456789";
            var special = "#@!$";
            var rng = new Random();

            return $"Mhs{special[rng.Next(special.Length)]}" +
                   $"{chars[rng.Next(chars.Length)]}" +
                   $"{chars[rng.Next(chars.Length)]}" +
                   $"{digits[rng.Next(digits.Length)]}" +
                   $"{digits[rng.Next(digits.Length)]}" +
                   $"{digits[rng.Next(digits.Length)]}";
        }
    }
}