using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class TeacherDashboardController : Controller
    {
        private readonly DBContextClass _context = new DBContextClass();

        public ActionResult Index()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = _context.Teachers.Find(teacherId);
            if (teacher == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var todayAttendance = _context.TeacherAttendances
                .FirstOrDefault(ta => ta.TeacherId == teacherId && DbFunctions.TruncateTime(ta.Date) == today);

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