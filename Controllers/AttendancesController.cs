using Michaelhouse.Data;
using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class AttendancesController : Controller
    {
        // Removed 'readonly' so it can be assigned in multiple constructors
        private ApplicationDbContext _context;

        public AttendancesController()
        {
            _context = new ApplicationDbContext();
        }

        public AttendancesController(ApplicationDbContext context)
        {
            _context = context;
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
            if (!_context.Subjects.Any())
            {
                // 1. Add Subject
                var sub1 = new Subject { SubjectName = "ICT", SubjectCode = "ICT301", GradeLevel = 12 };
                _context.Subjects.Add(sub1);

                // 2. Add Students with valid dates
                _context.Students.Add(new Student
                {
                    FirstName = "John",
                    LastName = "Doe",
                    GradeLevel = 12,
                    StudentNumber = "2024001",
                    Gender = "Male",
                    // Assuming your model has a DateOfBirth or similar
                    DateOfBirth = new DateTime(2005, 05, 20),
                    EnrollmentDate = DateTime.Now
                });

                _context.Students.Add(new Student
                {
                    FirstName = "Jane",
                    LastName = "Smith",
                    GradeLevel = 12,
                    StudentNumber = "2024002",
                    Gender = "Female",
                    DateOfBirth = new DateTime(2006, 01, 15),
                    EnrollmentDate = DateTime.Now
                });

                _context.SaveChanges();
            }
            return Content("Database seeded successfully!");
        }
    }
}