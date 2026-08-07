using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore; // CRITICAL: Required for Lambda .Include()
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOnly]
    public class TeacherController : BaseController
    {
        private readonly TeacherService _teacherService = new TeacherService();

        // ─── List all teachers (Staff Ledger) ────────────────────────────────────
        // Fix: Order by actual DB columns (LastName, FirstName) to avoid LINQ error
        public ActionResult Index()
        {
            using (var db = DbContextFactory.Create())
            {
                var teachers = db.Teachers
                    .Include(t => t.User)
                    .Include(t => t.SubjectAssignments.Select(sa => sa.Subject))
                    .OrderBy(t => t.LastName) // DB Column 1
                    .ThenBy(t => t.FirstName) // DB Column 2
                    .ToList();

                return View(teachers);
            }
        }

        // ─── Create teacher form ──────────────────────────────────────────────────

        public ActionResult Create()
        {
            PopulateSubjectsJson();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            string firstName, string lastName,
            string email, string phone,
            int? subject1Id, int? subject1Grade, int subject1Stream,
            int? subject2Id, int? subject2Grade, int subject2Stream)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Institutional records require a full Name and Email identity.";
                PopulateSubjectsJson();
                return View();
            }

            if (subject1Id == null || subject1Grade == null)
            {
                TempData["Error"] = "Primary Subject Allocation is mandatory.";
                PopulateSubjectsJson();
                return View();
            }

            var assignments = new List<(int SubjectId, int Grade, AcademicStream Stream)>
            {
                (subject1Id.Value, subject1Grade.Value, (AcademicStream)subject1Stream)
            };

            // Secondary assignment logic
            if (subject2Id.HasValue && subject2Id.Value > 0 &&
                subject2Grade.HasValue && subject2Grade.Value > 0)
            {
                if (subject2Id == subject1Id && subject2Grade == subject1Grade)
                {
                    TempData["Error"] = "Duplicate detected: Secondary assignment cannot match primary.";
                    PopulateSubjectsJson();
                    return View();
                }
                assignments.Add((subject2Id.Value, subject2Grade.Value, (AcademicStream)subject2Stream));
            }

            var (success, error, teacher) = _teacherService.CreateTeacher(
                firstName.Trim(), lastName.Trim(), email.Trim(), phone?.Trim(), assignments);

            if (!success)
            {
                TempData["Error"] = error;
                PopulateSubjectsJson();
                return View();
            }

            TempData["Success"] = $"Faculty record formalized for {firstName} {lastName}.";
            return RedirectToAction("Index");
        }

        // ─── View teacher details (Faculty Dossier) ───────────────────────────────

        public ActionResult Details(int id)
        {
            using (var db = DbContextFactory.Create())
            {
                var teacher = db.Teachers
                    .Include(t => t.User)
                    .Include(t => t.SubjectAssignments.Select(sa => sa.Subject))
                    .FirstOrDefault(t => t.TeacherId == id);

                if (teacher == null) return NotFound();

                // Generate institutional timetable
                var timetable = new TimetableGenerator().GetTimetableForTeacher(id, DateTime.Now.Year);
                var periods = db.Periods.OrderBy(p => p.PeriodNumber).ToList();

                ViewBag.Timetable = timetable;
                ViewBag.Periods = periods;
                ViewBag.AcademicYear = DateTime.Now.Year;
                ViewBag.Teacher = teacher; // Required for header welcome message

                return View(teacher);
            }
        }

        // ─── Populate subjects as JSON (Proxy-Proof Fix) ──────────────────────────

        private void PopulateSubjectsJson()
        {
            using (var db = DbContextFactory.Create())
            {

                var subjects = db.Subjects
                    .AsNoTracking()
                    .ToList()
                    .Select(s => new
                    {
                        subjectId = s.SubjectId,
                        name = s.Name,
                        stream = (int)s.Stream,
                        isCompulsory = s.IsCompulsory, // Ensure these booleans are in your Model
                        isLanguage = s.IsLanguage,
                        applicableGrades = s.ApplicableGrades ?? ""
                    })
                    .OrderBy(s => s.name)
                    .ToList();

                ViewBag.SubjectsJson = JsonConvert.SerializeObject(subjects);
            }
        }

        /*public ActionResult Dashboard()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = db.Teachers.FirstOrDefault(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var today = System.DateTime.Today;
            var todayAttendance = db.TeacherAttendances
                .FirstOrDefault(ta => ta.TeacherId == teacherId && ta.Date.Date == today);

            var subjectCount = db.Subjects.Count(s => s.TeacherId == teacherId);
            var recentAttendance = db.Attendances
                .Where(a => a.RecordedBy == teacher.Email)
                .OrderByDescending(a => a.Date)
                .Take(5)
                .ToList();

            ViewBag.TeacherName = teacher.FirstName;
            ViewBag.TodayAttendance = todayAttendance;
            ViewBag.SubjectCount = subjectCount;
            ViewBag.RecentAttendance = recentAttendance;

            return View();
        }*/

    }
}