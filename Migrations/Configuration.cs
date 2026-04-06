namespace Michaelhouse.Migrations
{
    using Michaelhouse.Models;
    using Michaelhouse.Models.Enums;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<Michaelhouse.Models.DBContextClass>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

         protected override void Seed(Michaelhouse.Models.DBContextClass context)
         {
            // Seed default admin account
            // Login: admin@michaelhouse.co.za / Admin@123
            /*if (!context.Users.Any(u => u.Role == "Admin"))
            {
                context.Users.Add(new Michaelhouse.Models.AppUser
                {
                    Name = "System Admin",
                    Email = "admin@michaelhouse.co.za",
                    PasswordHash = Michaelhouse.Controllers.AccountController.HashPassword("Admin@123"),
                    Role = "Admin"
                });
                context.SaveChanges();
            }*/
            // 1. SYSTEM USERS (Admin & Staff)
            context.Users.AddOrUpdate(u => u.Email,
                new AppUser { Name = "Headmaster Admin", Email = "admin@michaelhouse.org", PasswordHash = "AQAAAAEAACcQAAAAE...", Role = "Admin" }
            );
            context.SaveChanges();

            // 2. TEACHERS (Seeding 15 teachers to satisfy the 2-assignment max rule)
            var staffNames = new[] { "Henderson", "Dhlamini", "VanWyk", "Smit", "Naidoo", "Pillay", "Muller", "Khosi", "Botha", "Greeff" };
            foreach (var sName in staffNames)
            {
                context.Teachers.AddOrUpdate(t => t.Email, new Teacher
                {
                    FirstName = "Staff",
                    LastName = sName,
                    Email = $"{sName.ToLower()}@michaelhouse.org",
                    Phone = "0332344001"
                });
            }
            context.SaveChanges();

            // 3. SUBJECTS (Populating according to RegistrationService curriculum)
            var coreSubjects = new[] { "English Home Language", "Mathematics", "Life Orientation", "Natural Sciences", "Social Sciences" };
            foreach (var sub in coreSubjects)
            {
                context.Subjects.AddOrUpdate(s => s.Name, new Subject { Name = sub, IsCompulsory = true, ApplicableGrades = "8,9,10,11,12" });
            }
            // Stream Subjects
            context.Subjects.AddOrUpdate(s => s.Name,
                new Subject { Name = "Physics", Stream = AcademicStream.Sciences, RequiresMaths = true, ApplicableGrades = "10,11,12" },
                new Subject { Name = "Accounting", Stream = AcademicStream.Commerce, ApplicableGrades = "10,11,12" }
            );
            context.SaveChanges();

            // 4. FAMILIES (Parent + Student + Application)
            for (int i = 1; i <= 10; i++)
            {
                var parent = new Parent { Name = $"Parent {i}", Contact = $"parent{i}@mhouse.co.za", Relationship = "Father", CellPhone = $"082100000{i}" };
                context.Parents.AddOrUpdate(p => p.Contact, parent);
                context.SaveChanges();

                var student = new Student
                {
                    FirstName = $"Liam_{i}",
                    LastName = "Smith",
                    DOB = new DateTime(2011, 01, i),
                    ParentId = parent.ParentId,
                    CurrentGrade = "9",
                    IdNumber = $"ID{2000 + i}"
                };
                context.Students.AddOrUpdate(s => s.IdNumber, student);
                context.SaveChanges();

                // Create Application
                var app = new Application
                {
                    ParentId = parent.ParentId,
                    StudentId = student.StudentId,
                    ApplicationYear = 2026,
                    GradeApplying = 10,
                    Status = ApplicationStatus.Approved,
                    Date = DateTime.Now
                };
                context.Applications.AddOrUpdate(a => new { a.StudentId, a.ApplicationYear }, app);
                context.SaveChanges();

                // 5. REGISTRATIONS & INVOICES (Following your InvoiceService logic)
                var reg = new Registration { AppId = app.AppId, StudentId = student.StudentId, GradeEnrolling = 10, Status = RegistrationStatus.Pending };
                context.Registrations.AddOrUpdate(r => r.AppId, reg);
                context.SaveChanges();

                context.Invoices.AddOrUpdate(inv => inv.InvoiceNumber, new Invoice
                {
                    InvoiceNumber = $"MHS-REG-2026-{i:D5}",
                    RegistrationId = reg.RegistrationId,
                    StudentId = student.StudentId,
                    ParentId = parent.ParentId,
                    InvoiceType = "RegistrationFee",
                    Amount = 950m,
                    Status = "Paid",
                    CreatedDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(7)
                });
            }
            context.SaveChanges();

            // 6. ACADEMIC ASSIGNMENTS (TeacherSubjectGrade)
            var allTeachers = context.Teachers.ToList();
            var allSubs = context.Subjects.ToList();
            int tIdx = 0;
            foreach (var sub in allSubs)
            {
                for (int g = 8; g <= 10; g++)
                {
                    var teacher = allTeachers[tIdx % allTeachers.Count];
                    context.TeacherSubjectGrades.AddOrUpdate(tsg => new { tsg.TeacherId, tsg.SubjectId, tsg.Grade },
                        new TeacherSubjectGrade { TeacherId = teacher.TeacherId, SubjectId = sub.SubjectId, Grade = g, Stream = sub.Stream }
                    );
                    tIdx++;
                }
            }
            context.SaveChanges();

            // 7. AUTOMATED TIMETABLE (Triggering your TimetableGenerator Service)
            var timetableSvc = new Michaelhouse.Services.TimetableGenerator();
            timetableSvc.SeedPeriods(); // Creates the 7 teaching periods + breaks
            timetableSvc.GenerateTimetable(2026); // Generates all TimetableSlots based on Assignments
        }
    }
}
