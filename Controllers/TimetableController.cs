using System;
using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    public class TimetableController : Controller
    {
        private readonly TimetableGenerator _generator = new TimetableGenerator();

        // ─── Admin: Generate Timetable ────────────────────────────────────────────

        [AdminOnly]
        public ActionResult Generate()
        {
            ViewBag.Year = DateTime.Now.Year;

            using (var db = new DBContextClass())
            {
                ViewBag.TeacherCount = db.Teachers.Count();
                ViewBag.AssignmentCount = db.TeacherSubjectGrades.Count();
                ViewBag.PeriodCount = db.Periods.Count(p => !p.IsBreak);
                ViewBag.HasTimetable = db.TimetableSlots
                    .Any(ts => ts.AcademicYear == DateTime.Now.Year);
            }

            return View();
        }

        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Generate(int academicYear)
        {
            _generator.SeedPeriods();

            var (success, errors) = _generator.GenerateTimetable(academicYear);

            if (errors.Any())
                TempData["Error"] = string.Join("<br/>", errors);

            if (success)
                TempData["Success"] = $"Timetable generated for {academicYear}!";
            else
                TempData["Error"] = "Timetable generated with issues:<br/>" + string.Join("<br/>", errors);

            return RedirectToAction("GradeView", new { grade = 8, year = academicYear });
        }

        // ─── Admin: Grade view ────────────────────────────────────────────────────

        [AdminOnly]
        public ActionResult GradeView(int grade = 8, int? year = null)
        {
            int academicYear = year ?? DateTime.Now.Year;
            var slots = _generator.GetTimetableForGrade(grade, academicYear);

            using (var db = new DBContextClass())
            {
                ViewBag.Periods = db.Periods.OrderBy(p => p.PeriodNumber).ToList();
                ViewBag.Grade = grade;
                ViewBag.AcademicYear = academicYear;
                ViewBag.Grades = new[] { 8, 9, 10, 11, 12 };
            }

            return View(slots);
        }

        // ─── Admin: View any teacher's timetable ──────────────────────────────────

        [AdminOnly]
        public ActionResult TeacherView(int teacherId, int? year = null)
        {
            int academicYear = year ?? DateTime.Now.Year;
            var slots = _generator.GetTimetableForTeacher(teacherId, academicYear);

            using (var db = new DBContextClass())
            {
                ViewBag.Teacher = db.Teachers.Find(teacherId);
                ViewBag.Periods = db.Periods.OrderBy(p => p.PeriodNumber).ToList();
                ViewBag.AcademicYear = academicYear;
            }

            return View(slots);
        }

        // ─── Teacher: View own timetable ──────────────────────────────────────────

        [RequireLogin]
        public ActionResult MyTimetable(int? year = null)
        {
            int userId = (int)Session["UserId"];
            int academicYear = year ?? DateTime.Now.Year;

            using (var db = new DBContextClass())
            {
                var teacher = db.Teachers.FirstOrDefault(t => t.UserId == userId);
                if (teacher == null)
                {
                    TempData["Error"] = "Teacher profile not found.";
                    return RedirectToAction("Login", "Account");
                }

                var slots = _generator.GetTimetableForTeacher(teacher.TeacherId, academicYear);
                var periods = db.Periods.OrderBy(p => p.PeriodNumber).ToList();

                ViewBag.Teacher = teacher;
                ViewBag.Periods = periods;
                ViewBag.AcademicYear = academicYear;

                // Uses its own MyTimetable view — not TeacherView
                return View("MyTimetable", slots);
            }
        }

        // ─── Parent: View student's timetable ────────────────────────────────────

        [ParentOnly]
        public ActionResult StudentView(int studentId, int? year = null)
        {
            int parentId = (int)Session["ParentId"];

            using (var db = new DBContextClass())
            {
                // Security: parent can only see their own student
                var student = db.Students
                    .FirstOrDefault(s => s.StudentId == studentId &&
                                         s.ParentId == parentId);

                if (student == null) return HttpNotFound();

                int academicYear = year ?? DateTime.Now.Year;
                var slots = _generator.GetTimetableForStudent(studentId, academicYear);

                var reg = db.Registrations.FirstOrDefault(r => r.StudentId == studentId);
                var streamEnr = db.StreamEnrolments.FirstOrDefault(se => se.StudentId == studentId);
                var periods = db.Periods.OrderBy(p => p.PeriodNumber).ToList();

                ViewBag.Student = student;
                ViewBag.Grade = reg?.GradeEnrolling;
                ViewBag.Stream = streamEnr?.Stream ?? AcademicStream.None;
                ViewBag.Periods = periods;
                ViewBag.AcademicYear = academicYear;

                return View(slots);
            }
        }
    }
}