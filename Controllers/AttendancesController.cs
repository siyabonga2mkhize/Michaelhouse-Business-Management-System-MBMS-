using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Michaelhouse.Filters;

namespace Michaelhouse.Controllers
{
    public class AttendancesController : Controller
    {
        private readonly DBContextClass _context;

        public AttendancesController()
        {
            _context = new DBContextClass();
        }

        public AttendancesController(DBContextClass context)
        {
            _context = context;
        }

        [RequireLogin]
        public async Task<ActionResult> Index()
        {
            var userEmail = Session["UserName"]?.ToString();
            int userId = (int)(Session["UserId"] ?? 0);

            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            List<Subject> subjects;
            if (teacher != null)
            {
                subjects = await _context.Subjects
                    .Where(s => s.TeacherId == teacher.TeacherId)
                    .ToListAsync();
            }
            else
            {
                subjects = await _context.Subjects.ToListAsync();
            }

            return View(subjects);
        }

        // GET: Attendances/Record
        public async Task<ActionResult> Record(int? subjectId)
        {
            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return new HttpStatusCodeResult(HttpStatusCode.NotFound);

            var students = await _context.Students
                .Where(s => s.GradeLevel == subject.GradeLevel)
                .ToListAsync();

            ViewBag.SubjectName = subject.SubjectName;
            ViewBag.SubjectId = subjectId;

            return View(students);
        }

        // POST: Attendances/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Save(int subjectId, Dictionary<string, string> attendanceRecords)
        {
            if (attendanceRecords != null)
            {
                foreach (var key in attendanceRecords.Keys)
                {
                    // Convert the key back to int (StudentId) and value to Enum
                    int studentId = int.Parse(key);
                    var statusStr = attendanceRecords[key];
                    AttendanceStatus status = (AttendanceStatus)Enum.Parse(typeof(AttendanceStatus), statusStr);

                    var attendance = new Attendance
                    {
                        StudentId = studentId,
                        Status = status,
                        Date = DateTime.Today,
                        SubjectId = subjectId,
                        RecordedBy = User.Identity.IsAuthenticated ? User.Identity.Name : "Anonymous Teacher"
                    };
                    _context.Attendances.Add(attendance);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", "Home");
        }

        [RequireLogin]
        public async Task<ActionResult> MarkRegister(int subjectId)
        {
            int userId = (int)(Session["UserId"] ?? 0);
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null) return HttpNotFound("Teacher profile not found.");

            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return HttpNotFound("Subject not found.");

            var students = await _context.Students
                .Where(s => s.GradeLevel == subject.GradeLevel)
                .OrderBy(s => s.LastName)
                .ToListAsync();

            var viewModel = new MarkAttendanceViewModel
            {
                SubjectId = subject.SubjectId,
                SubjectName = subject.SubjectName,
                Date = DateTime.Today,
                Students = students.Select(s => new StudentAttendanceSelection
                {
                    StudentId = s.StudentId,
                    FullName = s.Name
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveRegister(MarkAttendanceViewModel vm)
        {
            if (vm.Students == null || !vm.Students.Any()) return RedirectToAction("Index", "Home");

            string recordedBy = Session["UserName"]?.ToString() ?? "Unknown";

            foreach (var item in vm.Students)
            {
                var record = new Attendance
                {
                    StudentId = item.StudentId,
                    SubjectId = vm.SubjectId,
                    Date = DateTime.Today,
                    Status = (AttendanceStatus)Enum.Parse(typeof(AttendanceStatus), item.Status),
                    RecordedBy = recordedBy
                };
                _context.Attendances.Add(record);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Register submitted successfully!";
            return RedirectToAction("Index", "Attendances");
        }

        [RequireLogin]
        public async Task<ActionResult> History()
        {
            int userId = (int)(Session["UserId"] ?? 0);
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return RedirectToAction("Index");
            }

            var records = await _context.Attendances
                .Include(a => a.Student)
                .Include(a => a.Subject)
                .Where(a => a.RecordedBy == teacher.Email)
                .OrderByDescending(a => a.Date)
                .Take(50)
                .ToListAsync();

            return View(records);
        }

        // IMPORTANT: Clean up the database connection
        protected override void Dispose(bool disposing)
        {
            if (disposing && _context != null)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }

        public ActionResult Setup()
        {
            var teacher = _context.Teachers.FirstOrDefault();
            int teacherId = teacher?.TeacherId ?? 0;

            if (!_context.Subjects.Any())
            {
                _context.Subjects.Add(new Subject { SubjectName = "Mathematics", SubjectCode = "MATH101", GradeLevel = 12, TeacherId = teacherId });
                _context.Subjects.Add(new Subject { SubjectName = "English", SubjectCode = "ENG101", GradeLevel = 12, TeacherId = teacherId });
                _context.Subjects.Add(new Subject { SubjectName = "Physical Science", SubjectCode = "PHY101", GradeLevel = 11, TeacherId = teacherId });
                _context.Subjects.Add(new Subject { SubjectName = "Life Sciences", SubjectCode = "LSC101", GradeLevel = 11, TeacherId = teacherId });
            }

            if (!_context.Students.Any())
            {
                _context.Students.Add(new Student { FirstName = "John", LastName = "Doe", GradeLevel = 12, StudentNumber = "2024001", Gender = "Male", DOB = new DateTime(2006, 5, 20), EnrollmentDate = DateTime.Now });
                _context.Students.Add(new Student { FirstName = "Jane", LastName = "Smith", GradeLevel = 12, StudentNumber = "2024002", Gender = "Female", DOB = new DateTime(2006, 1, 15), EnrollmentDate = DateTime.Now });
                _context.Students.Add(new Student { FirstName = "Mike", LastName = "Johnson", GradeLevel = 12, StudentNumber = "2024003", Gender = "Male", DOB = new DateTime(2005, 8, 10), EnrollmentDate = DateTime.Now });
                _context.Students.Add(new Student { FirstName = "Sarah", LastName = "Williams", GradeLevel = 11, StudentNumber = "2024004", Gender = "Female", DOB = new DateTime(2007, 3, 25), EnrollmentDate = DateTime.Now });
                _context.Students.Add(new Student { FirstName = "David", LastName = "Brown", GradeLevel = 11, StudentNumber = "2024005", Gender = "Male", DOB = new DateTime(2006, 11, 8), EnrollmentDate = DateTime.Now });
            }

            _context.SaveChanges();
            return Content("Demo data seeded successfully!");
        }
        public ActionResult CreateTeacherAccount()
        {
            using (var db = new DBContextClass())
            {
                var existingUser = db.Users.FirstOrDefault(u => u.Email == "teacher@michaelhouse.org");
                if (existingUser != null) return Content("User already exists!");

                var newUser = new AppUser
                {
                    Name = "John Staff",
                    Email = "teacher@michaelhouse.org",
                    PasswordHash = AccountController.HashPassword("Teacher@123"),
                    Role = "Teacher"
                };

                db.Users.Add(newUser);
                db.SaveChanges();

                var newTeacher = new Teacher
                {
                    FirstName = "John",
                    LastName = "Staff",
                    Email = "teacher@michaelhouse.org",
                    EmployeeNumber = "T001",
                    Department = "Science",
                    HireDate = DateTime.Now,
                    UserId = newUser.UserId
                };

                db.Teachers.Add(newTeacher);
                db.SaveChanges();

                Session["TeacherId"] = newTeacher.TeacherId;

                return Content("Teacher account and profile created! Email: teacher@michaelhouse.org | Password: Teacher@123");
            }
        }
    }
}