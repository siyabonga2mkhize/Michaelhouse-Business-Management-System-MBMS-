using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Michaelhouse.Models;


namespace Michaelhouse.Services
{
    public class StudentAccountService
    {
        /// <summary>
        /// Called after PARENT completes registration (not on admin approval).
        /// Creates the student AppUser account and returns credentials.
        /// </summary>
        public (AppUser User, string TempPassword) CreateStudentAccount(int studentId)
        {
            using (var db = new DBContextClass())
            {
                var student = db.Students
                    .Include("Parent")
                    .FirstOrDefault(s => s.StudentId == studentId);

                if (student == null)
                    throw new Exception($"Student #{studentId} not found.");

                // Don't create duplicate
                if (student.UserId != null)
                {
                    var existing = db.Users.Find(student.UserId);
                    if (existing != null)
                        return (existing, null);
                }

                var email = GenerateStudentEmail(student, db);
                var tempPassword = GenerateTempPassword();

                var user = new AppUser
                {
                    Name = student.Name,
                    Email = email,
                    PasswordHash = Controllers.AccountController.HashPassword(tempPassword),
                    Role = "Student"
                };

                db.Users.Add(user);
                db.SaveChanges();

                // Link to student
                student.UserId = user.UserId;
                db.SaveChanges();

                return (user, tempPassword);
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private string GenerateStudentEmail(Student student, DBContextClass db)
        {
            var first = student.FirstName.ToLower().Replace(" ", "");
            var last = student.LastName.ToLower().Replace(" ", "");
            var baseEmail = $"{first}.{last}@michaelhouse.co.za";

            if (!db.Users.Any(u => u.Email == baseEmail))
                return baseEmail;

            int counter = 2;
            while (true)
            {
                var candidate = $"{first}.{last}{counter}@michaelhouse.co.za";
                if (!db.Users.Any(u => u.Email == candidate))
                    return candidate;
                counter++;
            }
        }

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