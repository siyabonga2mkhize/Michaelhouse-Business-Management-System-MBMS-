using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class AttendancesController : BaseController
    {
        private readonly DBContextClass _context = DbContextFactory.Create();
        private readonly RegistrationService _regService = new RegistrationService();

        // ── Index: Subjects assigned to this teacher ─────────────────────────
        public async Task<ActionResult> Index()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = await _context.Teachers.FindAsync(teacherId);
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subjects = _regService.GetSubjectsForTeacher(teacherId); // returns List<Subject> with grade level

            // Stats
            int totalSubjects = subjects.Count();
            int markedToday = await _context.Attendances
                .Where(a => a.RecordedBy == teacher.Email && a.Date.Date == DateTime.Today)
                .Select(a => a.SubjectId)
                .Distinct()
                .CountAsync();

            int pending = totalSubjects - markedToday;

            ViewBag.TotalSubjects = totalSubjects;
            ViewBag.MarkedToday = markedToday;
            ViewBag.Pending = pending;
            ViewBag.Subjects = subjects;

            return View();
        }

        // ── MarkRegister: Show students enrolled in this subject ────────────
        public async Task<ActionResult> MarkRegister(int subjectId)
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = await _context.Teachers.FindAsync(teacherId);
            if (teacher == null) return NotFound();

            bool isAssigned = _context.TeacherSubjectGrades
                .Any(tsg => tsg.TeacherId == teacherId && tsg.SubjectId == subjectId);
            if (!isAssigned) return StatusCode(403);

            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return NotFound();

            var students = await _context.StudentSubjects
                .Where(ss => ss.SubjectId == subjectId)
                .Select(ss => ss.Student)
                .OrderBy(s => s.LastName)
                .ToListAsync();

            if (!students.Any())
            {
                TempData["Error"] = "No students are enrolled in this subject yet.";
                return RedirectToAction("Index");
            }

            // Load existing attendance for today
            var today = DateTime.Today;
            var existingAttendances = await _context.Attendances
                .Where(a => a.SubjectId == subjectId && a.Date.Date == today)
                .ToDictionaryAsync(a => a.StudentId);

            var viewModel = new MarkAttendanceViewModel
            {
                SubjectId = subject.SubjectId,
                SubjectName = subject.Name,
                Date = today,
                Students = students.Select(s => new StudentAttendanceSelection
                {
                    StudentId = s.StudentId,
                    FullName = s.FirstName + " " + s.LastName,
                    AttendanceStatus = existingAttendances.ContainsKey(s.StudentId)
                        ? existingAttendances[s.StudentId].Status.ToString()
                        : "Present"   // default for first-time marking
                }).ToList()
            };

            return View(viewModel);
        }

        // ── SaveRegister: POST – persist attendance (current day only) ──────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveRegister(MarkAttendanceViewModel model)
        {
            // Only allow saving for today
            if (model.Date.Date != DateTime.Today)
            {
                TempData["Error"] = "You can only mark attendance for the current day.";
                return RedirectToAction("Index");
            }

            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = await _context.Teachers.FindAsync(teacherId);
            if (teacher == null) return NotFound();

            // Load existing records for this subject & date (if any)
            var existingRecords = await _context.Attendances
                .Where(a => a.SubjectId == model.SubjectId && a.Date.Date == model.Date.Date)
                .ToDictionaryAsync(a => a.StudentId);

            bool hasInvalidChange = false;

            foreach (var entry in model.Students)
            {
                var newStatus = (AttendanceStatus)Enum.Parse(typeof(AttendanceStatus), entry.AttendanceStatus);

                if (existingRecords.TryGetValue(entry.StudentId, out var existing))
                {
                    // Existing record: only allow change to Late
                    if (existing.Status != newStatus)
                    {
                        if (newStatus == AttendanceStatus.Late)
                        {
                            // Allowed: change to Late
                            existing.Status = newStatus;
                        }
                        else
                        {
                            // Trying to change from something to Present or Absent -> reject
                            hasInvalidChange = true;
                        }
                    }
                    // If status unchanged, do nothing
                }
                else
                {
                    // No existing record: first time marking -> allow any status
                    _context.Attendances.Add(new Attendance
                    {
                        StudentId = entry.StudentId,
                        SubjectId = model.SubjectId,
                        Date = model.Date.Date,
                        Status = newStatus,
                        RecordedBy = teacher.Email
                    });
                }
            }

            if (hasInvalidChange)
            {
                TempData["Error"] = "You can only change attendance to 'Late' after the register has been saved. Other changes are not allowed.";
                return RedirectToAction("MarkRegister", new { subjectId = model.SubjectId });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Attendance saved for {model.Date:dd MMM yyyy}.";
            return RedirectToAction("Index");
        }

        public async Task<ActionResult> ViewRegister(int subjectId, int? studentId, DateTime? fromDate, DateTime? toDate)
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            bool isAssigned = _context.TeacherSubjectGrades
                .Any(tsg => tsg.TeacherId == teacherId && tsg.SubjectId == subjectId);
            if (!isAssigned) return StatusCode(403);

            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return NotFound();

            var query = _context.Attendances
                .Include(a => a.Student)
                .Where(a => a.SubjectId == subjectId)
                .AsQueryable();

            if (studentId.HasValue)
                query = query.Where(a => a.StudentId == studentId.Value);
            if (fromDate.HasValue)
                query = query.Where(a => a.Date >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(a => a.Date <= toDate.Value);

            var records = await query
                .OrderByDescending(a => a.Date)
                .ThenBy(a => a.Student.LastName)
                .Select(a => new AttendanceRecordViewModel
                {
                    Date = a.Date,
                    StudentName = a.Student.FirstName + " " + a.Student.LastName,
                    SubjectName = subject.Name,
                    Status = a.Status,
                    RecordedBy = a.RecordedBy
                })
                .ToListAsync();

            // For filter dropdown: list of students enrolled in this subject
            var students = await _context.StudentSubjects
                .Where(ss => ss.SubjectId == subjectId)
                .Select(ss => ss.Student)
                .OrderBy(s => s.LastName)
                .ToListAsync();

            ViewBag.Subject = subject;
            ViewBag.Students = students;
            ViewBag.SelectedStudentId = studentId;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            return View(records);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}