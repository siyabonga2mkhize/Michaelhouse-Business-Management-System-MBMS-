using System;
using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    public class ReportController : Controller
    {
        private readonly MarksService _marks = new MarksService();

        // ─── Student: View My Progress ────────────────────────────────────────────

        /// <summary>
        /// Student's own progress page — shows all assessments, captured marks,
        /// and computed term results for the current year.
        /// </summary>
        [StudentOnly]
        public ActionResult MyProgress()
        {
            int studentId = (int)Session["StudentId"];
            int year = DateTime.Now.Year;

            var report = _marks.GetStudentProgress(studentId, year);
            if (report == null) return HttpNotFound();

            return View(report);
        }

        /// <summary>
        /// Student's printable/downloadable report card for a specific term.
        /// Only available once the term results have been calculated.
        /// </summary>
        [StudentOnly]
        public ActionResult ReportCard(int term)
        {
            int studentId = (int)Session["StudentId"];
            int year = DateTime.Now.Year;

            using (var db = new DBContextClass())
            {
                var student = db.Students
                    .Include("Parent")
                    .FirstOrDefault(s => s.StudentId == studentId);

                var termResults = db.TermResults
                    .Include("Subject")
                    .Where(tr =>
                        tr.StudentId == studentId &&
                        tr.Term == term &&
                        tr.AcademicYear == year)
                    .OrderBy(tr => tr.Subject.Name)
                    .ToList();

                if (!termResults.Any())
                {
                    TempData["Info"] =
                        $"Term {term} results are not yet available. " +
                        "Please check back once your teacher has finalised marks.";
                    return RedirectToAction("MyProgress");
                }

                ViewBag.Student = student;
                ViewBag.Term = term;
                ViewBag.Year = year;
                return View(termResults);
            }
        }

        // ─── Admin: All Student Results ───────────────────────────────────────────

        /// <summary>
        /// Admin view — full results grid for a grade+term.
        /// Shows all students with their per-subject term marks.
        /// </summary>
        [AdminOnly]
        public ActionResult GradeResults(int grade, int term, int? year)
        {
            int targetYear = year ?? DateTime.Now.Year;

            using (var db = new DBContextClass())
            {
                var results = db.TermResults
                    .Include("Student")
                    .Include("Subject")
                    .Where(tr =>
                        tr.Grade == grade &&
                        tr.Term == term &&
                        tr.AcademicYear == targetYear)
                    .OrderBy(tr => tr.Student.LastName)
                    .ThenBy(tr => tr.Subject.Name)
                    .ToList();

                ViewBag.Grade = grade;
                ViewBag.Term = term;
                ViewBag.Year = targetYear;
                return View(results);
            }
        }

        /// <summary>
        /// Admin: calculate year-end result and set promotion status for one student.
        /// </summary>
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Promote(
            int studentId, int grade, int academicYear, string overrideStatus, string adminNotes)
        {
            var result = _marks.CalculateYearResult(studentId, grade, academicYear);

            if (result == null)
            {
                TempData["Error"] = "Could not calculate year result — no term results found.";
                return RedirectToAction("GradeResults", new { grade, term = 4, year = academicYear });
            }

            // Admin override
            if (!string.IsNullOrEmpty(overrideStatus) &&
                (overrideStatus == "Promoted" || overrideStatus == "Retained"))
            {
                using (var db = new DBContextClass())
                {
                    var yr = db.YearResults.Find(result.YearResultId);
                    if (yr != null)
                    {
                        yr.PromotionStatus = overrideStatus;
                        yr.AdminNotes = adminNotes;
                        db.SaveChanges();
                    }
                }
            }

            // Update student's CurrentGrade if promoted
            if (result.PromotionStatus == "Promoted")
            {
                using (var db = new DBContextClass())
                {
                    var student = db.Students.Find(studentId);
                    if (student != null)
                    {
                        int nextGrade = grade + 1;
                        student.CurrentGrade = nextGrade <= 12
                            ? nextGrade.ToString()
                            : "Graduate";
                        db.SaveChanges();
                    }
                }
            }

            TempData["Success"] =
                $"Year result saved: {result.PromotionStatus} " +
                $"(overall: {result.OverallPercent:F1}%).";
            return RedirectToAction("GradeResults", new { grade, term = 4, year = academicYear });
        }

        /// <summary>
        /// Admin: run year-end calculation for ALL students in a grade at once.
        /// </summary>
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CalculateAllYearResults(int grade, int academicYear)
        {
            using (var db = new DBContextClass())
            {
                // Students who have term results for this grade+year
                var studentIds = db.TermResults
                    .Where(tr => tr.Grade == grade && tr.AcademicYear == academicYear)
                    .Select(tr => tr.StudentId)
                    .Distinct()
                    .ToList();

                int promoted = 0, retained = 0;

                foreach (var sid in studentIds)
                {
                    var result = _marks.CalculateYearResult(sid, grade, academicYear);
                    if (result == null) continue;
                    if (result.PromotionStatus == "Promoted") promoted++;
                    else retained++;
                }

                TempData["Success"] =
                    $"Year-end results calculated for {studentIds.Count} student(s): " +
                    $"{promoted} promoted, {retained} retained.";
            }

            return RedirectToAction("GradeResults", new { grade, term = 4, year = academicYear });
        }
    }
}