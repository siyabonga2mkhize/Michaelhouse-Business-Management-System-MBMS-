using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class TeacherDashboardController : BaseController
    {
        private readonly DBContextClass _context = DbContextFactory.Create();

        public ActionResult Index()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = _context.Teachers.Find(teacherId);

            // If there's no TeacherId in session try to resolve it from the current
            // logged-in AppUser. This prevents a redirect loop where Login redirects
            // back to the dashboard but the dashboard then redirects to Login when
            // Session["TeacherId"] is missing.
            if (teacher == null)
            {
                int userId = (int)(Session["UserId"] ?? 0);
                if (userId > 0)
                {
                    teacher = _context.Teachers.FirstOrDefault(t => t.UserId == userId);
                    if (teacher != null)
                    {
                        Session["TeacherId"] = teacher.TeacherId;
                    }
                }
            }

            if (teacher == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var todayAttendance = _context.TeacherAttendances
                .FirstOrDefault(ta => ta.TeacherId == teacherId && ta.Date.Date == today);

            bool isCheckedIn = todayAttendance?.SignInTime != null;
            bool isCheckedOut = todayAttendance?.SignOutTime != null;

            // If not checked in today, show check-in prompt
            if (!isCheckedIn)
            {
                ViewBag.TeacherName = teacher.FirstName;
                return View("CheckInPrompt");
            }

            // Already checked in → show full dashboard
            var subjectsCount = _context.TeacherSubjectGrades.Count(tsg => tsg.TeacherId == teacherId);
            ViewBag.TeacherName = teacher.FirstName;
            ViewBag.SubjectsCount = subjectsCount;
            ViewBag.IsCheckedOut = isCheckedOut;
            if (todayAttendance != null)
            {
                ViewBag.CheckInTime = todayAttendance.SignInTime?.ToString("HH:mm");
                ViewBag.CheckOutTime = todayAttendance.SignOutTime?.ToString("HH:mm");
            }
            return View("Dashboard");
        }

        // Optional: action to skip check-in (if already checked in earlier)
        public ActionResult SkipCheckIn()
        {
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}