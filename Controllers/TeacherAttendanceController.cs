using Michaelhouse.Infrastructure;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    public class TeacherAttendanceController : BaseController
    {
        private readonly DBContextClass _context;

        public TeacherAttendanceController()
        {
            _context = DbContextFactory.Create();
        }

        [RequireLogin]
        public ActionResult Index()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = _context.Teachers.FirstOrDefault(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var today = DateTime.Today;
            var todayAttendance = _context.TeacherAttendances
                .FirstOrDefault(ta => ta.TeacherId == teacherId && ta.Date.Date == today);

            var viewModel = new TeacherAttendanceViewModel
            {
                TeacherId = teacherId,
                TeacherName = teacher.FirstName,
                TodayAttendance = todayAttendance,
                IsCheckedIn = todayAttendance?.SignInTime != null,
                IsCheckedOut = todayAttendance?.SignOutTime != null,
                CanCheckIn = todayAttendance == null,
                CanCheckOut = todayAttendance != null && todayAttendance.SignInTime != null && todayAttendance.SignOutTime == null
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckIn(double latitude, double longitude)
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            if (teacherId == 0)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var teacher = _context.Teachers.Find(teacherId);
            if (teacher == null)
                return Json(new { success = false, message = "Teacher not found." });

            var today = DateTime.Today;
            var existingAttendance = _context.TeacherAttendances
                .FirstOrDefault(ta => ta.TeacherId == teacherId && ta.Date.Date == today);

            if (existingAttendance?.SignInTime != null)
                return Json(new { success = false, message = "Already checked in today." });

            var schoolLocation = new Helpers.GeofencingService.Location(-29.85023161019211, 31.006719322082617);
            var teacherLocation = new Helpers.GeofencingService.Location(latitude, longitude);
            bool isWithinGeofence = Helpers.GeofencingService.IsWithinRadius(schoolLocation, teacherLocation, 500);

            if (!isWithinGeofence)
            {
                return Json(new { success = false, message = "You are not within school premises. Check-in denied." });
            }

            var attendance = existingAttendance ?? new TeacherAttendance
            {
                TeacherId = teacherId,
                Date = today
            };

            attendance.SignInTime = DateTime.Now;
            attendance.Status = TeacherStatus.Present;
            attendance.IsVerified = true;

            if (existingAttendance == null)
                _context.TeacherAttendances.Add(attendance);

            _context.SaveChanges();

            return Json(new { success = true, message = "Checked in successfully! Location verified within school premises.", isVerified = true, checkInTime = attendance.SignInTime });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckOut(double latitude, double longitude)
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            if (teacherId == 0)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            var today = DateTime.Today;
            var attendance = _context.TeacherAttendances
                .FirstOrDefault(ta => ta.TeacherId == teacherId && ta.Date.Date == today);

            if (attendance == null)
                return Json(new { success = false, message = "No check-in record found for today." });

            if (attendance.SignOutTime != null)
                return Json(new { success = false, message = "Already checked out today." });

            var schoolLocation = new Helpers.GeofencingService.Location(-29.851102176006535, 31.00771554938651);
            var teacherLocation = new Helpers.GeofencingService.Location(latitude, longitude);
            bool isWithinGeofence = Helpers.GeofencingService.IsWithinRadius(schoolLocation, teacherLocation, 500);

            attendance.SignOutTime = DateTime.Now;
            attendance.IsVerified = attendance.IsVerified && isWithinGeofence;
            _context.SaveChanges();

            var duration = attendance.SignOutTime.Value - attendance.SignInTime.Value;
            var message = $"Checked out successfully! Duration: {duration.Hours}h {duration.Minutes}m";
            if (!isWithinGeofence)
                message += " (Location unverified)";

            return Json(new { success = true, message, checkOutTime = attendance.SignOutTime, duration = $"{duration.Hours}h {duration.Minutes}m" });
        }

        [RequireLogin]
        public ActionResult History()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var records = _context.TeacherAttendances
                .Where(ta => ta.TeacherId == teacherId)
                .OrderByDescending(ta => ta.Date)
                .Take(30)
                .ToList();

            return View(records);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _context != null)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class TeacherAttendanceViewModel
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; }
        public TeacherAttendance TodayAttendance { get; set; }
        public bool IsCheckedIn { get; set; }
        public bool IsCheckedOut { get; set; }
        public bool CanCheckIn { get; set; }
        public bool CanCheckOut { get; set; }
    }
}
