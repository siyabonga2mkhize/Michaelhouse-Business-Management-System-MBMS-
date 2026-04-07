using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class AttendancesController : Controller
    {
        private readonly DBContextClass _context = new DBContextClass();
        private readonly RegistrationService _regService = new RegistrationService();

        // ── Index: Subjects assigned to this teacher ─────────────────────────
        public async Task<ActionResult> Index()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = await _context.Teachers.FindAsync(teacherId);
            if (teacher == null) return RedirectToAction("Login", "Account");

            var subjects = _regService.GetSubjectsForTeacher(teacherId);
            return View(subjects);
        }

        // ── MarkRegister: Show students enrolled in this subject ────────────
        public async Task<ActionResult> MarkRegister(int subjectId)
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = await _context.Teachers.FindAsync(teacherId);
            if (teacher == null) return HttpNotFound();

            // Verify teacher is actually assigned to this subject
            bool isAssigned = _context.TeacherSubjectGrades
                .Any(tsg => tsg.TeacherId == teacherId && tsg.SubjectId == subjectId);
            if (!isAssigned) return new HttpUnauthorizedResult();

            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return HttpNotFound();

            // Get students who are enrolled in this subject (via StudentSubject)
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

            var viewModel = new MarkAttendanceViewModel
            {
                SubjectId = subject.SubjectId,
                SubjectName = subject.Name,
                Date = DateTime.Today,
                Students = students.Select(s => new StudentAttendanceSelection
                {
                    StudentId = s.StudentId,
                    FullName = s.FirstName + " " + s.LastName,
                    AttendanceStatus = "Present"
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
            if (teacher == null) return HttpNotFound();

            foreach (var entry in model.Students)
            {
                var existing = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.StudentId == entry.StudentId &&
                                              a.SubjectId == model.SubjectId &&
                                              DbFunctions.TruncateTime(a.Date) == model.Date.Date);

                var status = (AttendanceStatus)Enum.Parse(typeof(AttendanceStatus), entry.AttendanceStatus);

                if (existing != null)
                    existing.Status = status;
                else
                {
                    _context.Attendances.Add(new Attendance
                    {
                        StudentId = entry.StudentId,
                        SubjectId = model.SubjectId,
                        Date = model.Date.Date,
                        Status = status,
                        RecordedBy = teacher.Email
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Attendance saved for {model.Date:dd MMM yyyy}.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}