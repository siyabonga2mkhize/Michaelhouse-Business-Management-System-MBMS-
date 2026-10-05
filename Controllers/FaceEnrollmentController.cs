using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Student, Admin")]
    public class FaceEnrollmentController : Controller
    {
        private readonly DBContextClass _db;
        private readonly IFaceRecognitionService _faceService;

        public FaceEnrollmentController()
        {
            _db = new DBContextClass();
            _faceService = FaceRecognitionServices.Create();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // GET: FaceEnrollment
        // Shows the enrollment page
        // ============================================================

        [HttpGet]
        public ActionResult Index()
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return new HttpStatusCodeResult(403);

            var existing = _db.StudentFaceSignatures
                .FirstOrDefault(x => x.StudentId == studentId);

            ViewBag.IsEnrolled = existing != null;
            ViewBag.EnrolledAt = existing != null
                ? existing.EnrolledAt.ToString("dd MMM yyyy HH:mm")
                : null;

            return View();
        }

        // ============================================================
        // POST: FaceEnrollment/Capture
        // Accepts the base64 image from the browser, generates an
        // encoding, stores it (photo itself is discarded)
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Capture(string imageBase64)
        {
            try
            {
                int studentId = ResolveStudentId();
                if (studentId <= 0)
                {
                    return Json(new { success = false, message = "Not logged in as a student." });
                }

                if (string.IsNullOrWhiteSpace(imageBase64))
                {
                    return Json(new { success = false, message = "No image received." });
                }

                // Strip "data:image/jpeg;base64," prefix if present
                int commaIdx = imageBase64.IndexOf(',');
                if (commaIdx > 0)
                {
                    imageBase64 = imageBase64.Substring(commaIdx + 1);
                }

                byte[] imageBytes = Convert.FromBase64String(imageBase64);

                if (imageBytes.Length < 500)
                {
                    return Json(new { success = false, message = "Image too small." });
                }

                // Exactly one face, clearly visible
                var analysis = _faceService.Analyse(imageBytes);
                if (!analysis.IsOk)
                {
                    return Json(new
                    {
                        success = false,
                        message = analysis.Status == FaceAnalysisStatus.NotAnImage
                            ? "The photo could not be read. Make sure your face is well lit and centred, then try again."
                            : analysis.Message
                    });
                }
                string encoding = analysis.Encoding;

                // A face may be enrolled for one student only
                var others = _db.StudentFaceSignatures
                    .Where(x => x.IsActive && x.StudentId != studentId)
                    .ToList();
                var sameFace = _faceService.FindBestMatch(encoding, others);
                if (sameFace != null && sameFace.Distance <= DlibFaceRecognitionService.MatchThreshold)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This face is already enrolled for another student. A face can only be enrolled once — ask the Admin to remove the other enrollment if it's wrong."
                    });
                }

                // Save or update
                var existing = _db.StudentFaceSignatures
                    .FirstOrDefault(x => x.StudentId == studentId);

                if (existing == null)
                {
                    existing = new StudentFaceSignature
                    {
                        StudentId = studentId,
                        FaceEncoding = encoding,
                        ConsentGivenAt = DateTime.UtcNow,
                        EnrolledAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _db.StudentFaceSignatures.Add(existing);
                }
                else
                {
                    existing.FaceEncoding = encoding;
                    existing.EnrolledAt = DateTime.UtcNow;
                    existing.IsActive = true;
                }

                _db.SaveChanges();

                return Json(new { success = true, message = "Enrolled successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // POST: FaceEnrollment/Revoke
        // Allows the student to delete their face signature
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Revoke()
        {
            int studentId = ResolveStudentId();
            if (studentId <= 0) return new HttpStatusCodeResult(403);

            var existing = _db.StudentFaceSignatures
                .FirstOrDefault(x => x.StudentId == studentId);

            if (existing != null)
            {
                _db.StudentFaceSignatures.Remove(existing);
                _db.SaveChanges();
            }

            TempData["Success"] = "Face signature removed.";
            return RedirectToAction("Index");
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private int ResolveStudentId()
        {
            if (Session["StudentId"] != null)
            {
                int id;
                if (int.TryParse(Session["StudentId"].ToString(), out id))
                {
                    return id;
                }
            }

            if (Session["UserId"] != null)
            {
                int userId;
                if (int.TryParse(Session["UserId"].ToString(), out userId))
                {
                    var student = _db.Students.FirstOrDefault(s => s.UserId == userId);
                    if (student != null)
                    {
                        Session["StudentId"] = student.StudentId;
                        return student.StudentId;
                    }
                }
            }

            return 0;
        }
    }
}