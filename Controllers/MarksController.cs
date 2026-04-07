using System;
using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Services;

namespace Michaelhouse.Controllers
{
    /// <summary>
    /// Handles assessment planning and mark capture for teachers.
    /// All routes under /Marks/ require the caller to be a logged-in Teacher.
    /// </summary>
    [TeacherOnly]
    public class MarksController : Controller
    {
        private readonly MarksService _marks = new MarksService();

        private int GetTeacherId()
        {
            // Session["TeacherId"] is set at login — same pattern as ParentId / StudentId
            return Session["TeacherId"] != null ? (int)Session["TeacherId"] : 0;
        }

        // ─── Dashboard ────────────────────────────────────────────────────────────

        /// <summary>
        /// Teacher landing page: lists all their assessments for the current year.
        /// </summary>
        public ActionResult Index()
        {
            int teacherId = GetTeacherId();
            int year = DateTime.Now.Year;

            var assessments = _marks.GetAssessmentsForTeacher(teacherId, year);

            // Group for easy display: Term → Subject → Assessments
            ViewBag.Year = year;
            ViewBag.TeacherId = teacherId;
            return View(assessments);
        }

        // ─── Plan Assessment ──────────────────────────────────────────────────────

        public ActionResult Plan()
        {
            int teacherId = GetTeacherId();
            PopulateAssignmentDropdowns(teacherId);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Plan(
                  int? subjectId, int? grade, int? stream,
                  string title, string assessmentType,
                  int? term, int? academicYear,
                  DateTime? scheduledDate,
                  decimal? totalMarks, decimal? weightingPercent,
                  string notes)
        {
            int teacherId = GetTeacherId();

            // Institutional Validation: Catch missing data before it hits the Service
            if (!subjectId.HasValue || !grade.HasValue || !term.HasValue || !totalMarks.HasValue || !weightingPercent.HasValue)
            {
                TempData["Error"] = "Dossier Incomplete: The internal record mapping (Grade/Subject) failed to transmit.";
                PopulateAssignmentDropdowns(teacherId);
                return View();
            }

            var (success, error, assessment) = _marks.CreateAssessment(
                teacherId, subjectId.Value, grade.Value,
                (AcademicStream)(stream ?? 0),
                title, assessmentType,
                term.Value, academicYear ?? DateTime.Now.Year,
                scheduledDate ?? DateTime.Today,
                totalMarks.Value, weightingPercent.Value, notes);

            if (!success)
            {
                TempData["Error"] = error;
                PopulateAssignmentDropdowns(teacherId);
                return View();
            }

            TempData["Success"] = $"Assessment '{title}' formally provisioned.";
            return RedirectToAction("Index");
        }

        // ─── Mark Capture ─────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the mark-entry sheet for a specific assessment.
        /// Lists every student enrolled in that subject+grade.
        /// </summary>
        [HttpGet]
        public ActionResult Capture(int id)
        {
            int teacherId = GetTeacherId();

            using (var db = new DBContextClass())
            {
                // CRITICAL FIX: Eager load Subject to prevent DynamicProxy strings in the header
                var assessment = db.Assessments
                    .Include("Subject")
                    .FirstOrDefault(a => a.AssessmentId == id);

                if (assessment == null) return HttpNotFound();

                // Guard: only the owning teacher can access
                if (assessment.TeacherId != teacherId)
                    return new HttpUnauthorizedResult();

                // Note: Ensure _marks.GetMarkSheet internally also handles student name loading
                var sheet = _marks.GetMarkSheet(id);

                ViewBag.Assessment = assessment;
                return View(sheet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Capture(int assessmentId, bool closeCapture = false)
        {
            int teacherId = GetTeacherId();

            using (var db = new DBContextClass())
            {
                var assessment = db.Assessments.Find(assessmentId);
                if (assessment == null) return HttpNotFound();

                // Verification: Ensure the teacher actually owns this assessment
                if (assessment.TeacherId != teacherId) return new HttpUnauthorizedResult();

                // Prevent saving if already closed
                if (assessment.MarksCaptureClosed)
                {
                    TempData["Error"] = "This assessment is sealed and cannot be modified.";
                    return RedirectToAction("Index");
                }
            }

            // Parse mark inputs from form — keys: mark_{studentId}, absent_{studentId}, comment_{studentId}
            var inputs = new System.Collections.Generic.List<MarkInput>();

            // We iterate through AllKeys to capture every student row submitted
            foreach (var key in Request.Form.AllKeys)
            {
                if (key == null || !key.StartsWith("mark_")) continue;

                int studentId;
                if (!int.TryParse(key.Substring(5), out studentId)) continue;

                // Checkboxes only send a value if they are "on"
                bool isAbsent = Request.Form["absent_" + studentId] == "on";
                string comment = Request.Form["comment_" + studentId];

                decimal? marksObtained = null;
                decimal parsedMark;

                // If not absent, try to parse the mark
                if (!isAbsent && decimal.TryParse(Request.Form[key], out parsedMark))
                {
                    marksObtained = parsedMark;
                }

                inputs.Add(new MarkInput
                {
                    StudentId = studentId,
                    MarksObtained = marksObtained,
                    IsAbsent = isAbsent,
                    Comment = comment
                });
            }

            // Service call to handle the business logic of saving/recalculating averages
            var (success, error) = _marks.SaveMarks(assessmentId, teacherId, inputs, closeCapture);

            if (!success)
            {
                TempData["Error"] = error;
                return RedirectToAction("Capture", new { id = assessmentId });
            }

            TempData["Success"] = closeCapture
                ? "Mark Ledger successfully sealed and finalized."
                : "Progress saved to the dossier. The ledger remains open for further entries.";

            return RedirectToAction("Index");
        }

        // ─── Calculate Term Results ───────────────────────────────────────────────

        /// <summary>
        /// Triggers weighted mark calculation for a subject+grade+term.
        /// Teacher can do this once all marks are captured for the term.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CalculateTerm(
            int subjectId, int grade, int stream, int term, int academicYear)
        {
            // Guard: teacher must own this subject+grade
            int teacherId = GetTeacherId();
            using (var db = new DBContextClass())
            {
                bool owns = db.TeacherSubjectGrades.Any(tsg =>
                    tsg.TeacherId == teacherId &&
                    tsg.SubjectId == subjectId &&
                    tsg.Grade == grade);

                if (!owns) return new HttpUnauthorizedResult();
            }

            var results = _marks.CalculateTermResults(
                subjectId, grade, term, academicYear, (AcademicStream)stream);

            TempData["Success"] =
                $"Term {term} results calculated for {results.Count} student(s).";
            return RedirectToAction("Index");
        }

        // ─── Helper ───────────────────────────────────────────────────────────────

        private void PopulateAssignmentDropdowns(int teacherId)
        {
            using (var db = new DBContextClass())
            {
                var assignments = db.TeacherSubjectGrades
             .Include("Subject")
             .AsNoTracking()
             .Where(tsg => tsg.TeacherId == teacherId)
             .ToList();

                ViewBag.Assignments = assignments;

                // Simplified for the View
                ViewBag.Terms = new SelectList(new[]
                {
            new { Value = 1, Text = "Term 1" },
            new { Value = 2, Text = "Term 2" },
            new { Value = 3, Text = "Term 3" },
            new { Value = 4, Text = "Term 4" }
        }, "Value", "Text");

                ViewBag.AssessmentTypes = new[] { "Test", "Exam", "Assignment", "Oral", "Practical" };
            }
        }
    }
}