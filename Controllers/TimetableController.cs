using Microsoft.EntityFrameworkCore;
using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Services;
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    public class TimetableController : BaseController
    {
        private readonly TimetableGenerator _generator = new TimetableGenerator();

        // ─── Admin: Generate Timetable ────────────────────────────────────────────

        [AdminOnly]
        public ActionResult Generate()
        {
            ViewBag.Year = DateTime.Now.Year;

            using (var db = DbContextFactory.Create())
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

            using (var db = DbContextFactory.Create())
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

            using (var db = DbContextFactory.Create())
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

            using (var db = DbContextFactory.Create())
            {
                // Disable proxy creation for this specific query to get clean data

                var teacher = db.Teachers
                    .AsNoTracking() // This prevents the DynamicProxies string
                    .FirstOrDefault(t => t.UserId == userId);

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

                return View("MyTimetable", slots);
            }
        }

        // ─── Parent: View student's timetable ────────────────────────────────────

        // ─── Shared: View student's timetable (Visible to Parent & Student) ─────────

        [RequireLogin]
        public ActionResult StudentView(int? studentId, int? year = null)
        {
            // 1. Capture Session Metadata
            int userId = (int)(Session["UserId"] ?? 0);
            string userRole = Session["UserRole"]?.ToString();
            int academicYear = year ?? DateTime.Now.Year;

            using (var db = DbContextFactory.Create())
            {
                // IMPORTANT: Disable Proxy to fix naming issues seen in your screenshots

                Student student = null;

                // 2. IDENTIFICATION LOGIC
                if (studentId.HasValue && studentId.Value > 0)
                {
                    // Case A: Explicit ID provided (Parent/Admin/Link click)
                    student = db.Students.AsNoTracking().FirstOrDefault(s => s.StudentId == studentId.Value);
                }
                else if (userRole == "Student")
                {
                    // Case B: Logged in as Student, no ID in URL - find record by UserId link
                    student = db.Students.AsNoTracking().FirstOrDefault(s => s.UserId == userId);
                }

                // 3. THE 404 CULPRIT CHECK
                if (student == null)
                {
                    return NotFound($"Institutional Archive Error: Record not found. " +
                                        $"(Logged in as: {userRole}, UserId: {userId}, Requested ID: {studentId})");
                }

                // 4. DATA RETRIEVAL
                var slots = _generator.GetTimetableForStudent(student.StudentId, academicYear);
                var periods = db.Periods.OrderBy(p => p.PeriodNumber).ToList();

                // Load Enrollment metadata
                var reg = db.Registrations.AsNoTracking()
                            .OrderByDescending(r => r.RegistrationId)
                            .FirstOrDefault(r => r.StudentId == student.StudentId);

                var streamEnr = db.StreamEnrolments.AsNoTracking()
                                  .FirstOrDefault(se => se.StudentId == student.StudentId);

                ViewBag.Student = student;
                ViewBag.Grade = reg?.GradeEnrolling ?? 0;
                ViewBag.Stream = streamEnr?.Stream ?? AcademicStream.None;
                ViewBag.Periods = periods;
                ViewBag.AcademicYear = academicYear;

                return View("StudentView", slots);
            }
        }

    }
}