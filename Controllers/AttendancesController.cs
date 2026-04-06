using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class AttendancesController : Controller
    {
        private readonly DBContextClass _context = new DBContextClass();

        // ── Index: Show subjects assigned to this teacher ─────────────────────────
        [RequireLogin]
        public async Task<ActionResult> Index()
        {
            int userId = (int)(Session["UserId"] ?? 0);
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            List<Subject> subjects;

            if (teacher != null)
            {
                subjects = await _context.Subjects
                    .Where(s => s.TeacherId == teacher.TeacherId)
                    .ToListAsync();

                // If no subjects assigned yet, seed demo subjects for this teacher
                if (!subjects.Any())
                {
                    await SeedDemoSubjects(teacher.TeacherId);
                    subjects = await _context.Subjects
                        .Where(s => s.TeacherId == teacher.TeacherId)
                        .ToListAsync();
                }
            }
            else
            {
                // Admin fallback: show all subjects
                subjects = await _context.Subjects.ToListAsync();
            }

            return View(subjects);
        }

        // ── MarkRegister: Show student list for a subject ─────────────────────────
        [RequireLogin]
        public async Task<ActionResult> MarkRegister(int subjectId)
        {
            int userId = (int)(Session["UserId"] ?? 0);
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null) return HttpNotFound("Teacher profile not found.");

            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return HttpNotFound("Subject not found.");

            // Get students in this grade, ordered by last name
            var students = await _context.Students
                .Where(s => s.GradeLevel == subject.GradeLevel)
                .OrderBy(s => s.LastName)
                .ToListAsync();

            // If no students seeded yet, create demo students
            if (!students.Any())
            {
                await SeedDemoStudents(subject.GradeLevel);
                students = await _context.Students
                    .Where(s => s.GradeLevel == subject.GradeLevel)
                    .OrderBy(s => s.LastName)
                    .ToListAsync();
            }

            var viewModel = new MarkAttendanceViewModel
            {
                SubjectId = subject.SubjectId,
                SubjectName = subject.Name,
                Date = DateTime.Today,
                Students = students.Select(s => new StudentAttendanceSelection
                {
                    StudentId = s.StudentId,
                    FullName = s.FirstName + " " + s.LastName,
                    AttendanceStatus = "Present" // default
                }).ToList()
            };

            return View(viewModel);
        }

        // ── SaveRegister: POST – persist attendance records ───────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public async Task<ActionResult> SaveRegister(MarkAttendanceViewModel model)
        {
            int userId = (int)(Session["UserId"] ?? 0);
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null) return HttpNotFound("Teacher profile not found.");

            var today = model.Date.Date;

            foreach (var entry in model.Students)
            {
                // 1. Parse the string from the ViewModel into the Enum type
                AttendanceStatus parsedStatus = (AttendanceStatus)Enum.Parse(typeof(AttendanceStatus), entry.AttendanceStatus);

                // 2. Avoid duplicate entries for same student/subject/date
                var existing = await _context.Attendances
                    .FirstOrDefaultAsync(a =>
                        a.StudentId == entry.StudentId &&
                        a.SubjectId == model.SubjectId &&
                        DbFunctions.TruncateTime(a.Date) == today);

                if (existing != null)
                {
                    // Use the parsed Enum here
                    existing.Status = parsedStatus;
                }
                else
                {
                    _context.Attendances.Add(new Attendance
                    {
                        StudentId = entry.StudentId,
                        SubjectId = model.SubjectId,
                        Date = today,
                        // Use the parsed Enum here
                        Status = parsedStatus,
                        RecordedBy = teacher.Email
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Register saved successfully for {today:dd MMM yyyy}.";
            return RedirectToAction("Index");
        }

        // ── Record (legacy GET – kept for compatibility) ──────────────────────────
        public async Task<ActionResult> Record(int? subjectId)
        {
            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return new HttpStatusCodeResult(HttpStatusCode.NotFound);

            var students = await _context.Students
                .Where(s => s.GradeLevel == subject.GradeLevel)
                .ToListAsync();

            ViewBag.SubjectName = subject.Name;
            ViewBag.SubjectId = subjectId;

            return View(students);
        }

        // ── Demo seed helpers ─────────────────────────────────────────────────────

        private async Task SeedDemoSubjects(int teacherId)
        {
            if (!_context.Subjects.Any(s => s.TeacherId == teacherId))
            {
                _context.Subjects.Add(new Subject
                {
                    Name = "Mathematics",
                    Code = "MATH10",
                    GradeLevel = 10,
                    TeacherId = teacherId
                });
                _context.Subjects.Add(new Subject
                {
                    Name = "Physical Sciences",
                    Code = "SCI10",
                    GradeLevel = 10,
                    TeacherId = teacherId
                });
                _context.Subjects.Add(new Subject
                {
                    Name = "English Home Language",
                    Code = "ENG10",
                    GradeLevel = 10,
                    TeacherId = teacherId
                });
                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedDemoStudents(int gradeLevel)
        {
            if (!_context.Students.Any(s => s.GradeLevel == gradeLevel))
            {
                var names = new[]
                {
                    ("Amahle", "Dube"),
                    ("Sipho", "Nkosi"),
                    ("Lethabo", "Mokoena"),
                    ("Thandiwe", "Mthembu"),
                    ("Kagiso", "Sithole"),
                    ("Rethabile", "Molefe"),
                    ("Bongani", "Zulu"),
                    ("Nokwanda", "Ndlovu")
                };
                foreach (var (first, last) in names)
                {
                    _context.Students.Add(new Student
                    {
                        FirstName = first,
                        LastName = last,
                        GradeLevel = gradeLevel,
                        DOB = new DateTime(2008, 1, 1)
                    });
                }
                await _context.SaveChangesAsync();
            }
        }

        // ── Misc setup actions ────────────────────────────────────────────────────
        public ActionResult Setup()
        {
            var teacher = _context.Teachers.FirstOrDefault();
            int teacherId = teacher?.TeacherId ?? 0;

            if (!_context.Subjects.Any())
            {
                _context.Subjects.Add(new Subject { Name = "Mathematics", Code = "MATH101", GradeLevel = 12, TeacherId = teacherId });
                _context.Subjects.Add(new Subject { Name = "English", Code = "ENG101", GradeLevel = 12, TeacherId = teacherId });
            }

            if (!_context.Students.Any())
            {
                _context.Students.Add(new Student { FirstName = "John", LastName = "Doe", GradeLevel = 12, DOB = new DateTime(2006, 5, 20) });
                _context.Students.Add(new Student { FirstName = "Jane", LastName = "Smith", GradeLevel = 12, DOB = new DateTime(2006, 1, 15) });
            }

            _context.SaveChanges();
            return Content("Demo data seeded successfully!");
        }

        public ActionResult CreateTeacherAccount()
        {
            var existingUser = _context.Users.FirstOrDefault(u => u.Email == "teacher@michaelhouse.org");
            if (existingUser != null) return Content("User already exists!");

            var newUser = new AppUser
            {
                Name = "John Staff",
                Email = "teacher@michaelhouse.org",
                PasswordHash = AccountController.HashPassword("Teacher@123"),
                Role = "Teacher"
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            var newTeacher = new Teacher
            {
                FirstName = "John",
                LastName = "Staff",
                Email = "teacher@michaelhouse.org",
                Specialization = "Science",
                HireDate = DateTime.Now,
                UserId = newUser.UserId
            };

            _context.Teachers.Add(newTeacher);
            _context.SaveChanges();

            return Content("Teacher account created! Email: teacher@michaelhouse.org | Password: Teacher@123");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}