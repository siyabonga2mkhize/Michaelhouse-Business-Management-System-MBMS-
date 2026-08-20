using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class StudentsController : BaseController
    {
        private DBContextClass db = new DBContextClass();
        private int GetCurrentStudentId()
        {
            if (Session["StudentId"] == null)
                return 0;
            return (int)Session["StudentId"];
        }

        // GET: Students
        public ActionResult Index()
        {
            var students = db.Students.Include(s => s.Parent);
            return View(students.ToList());
        }

        // GET: Students/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }

        // GET: Students/Create
        public ActionResult Create()
        {
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name");
            return View();
        }

        // POST: Students/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "StudentId,Name,DOB,ParentId")] Student student)
        {
            if (ModelState.IsValid)
            {
                db.Students.Add(student);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name", student.ParentId);
            return View(student);
        }

        // GET: Students/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name", student.ParentId);
            return View(student);
        }

        // POST: Students/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "StudentId,Name,DOB,ParentId")] Student student)
        {
            if (ModelState.IsValid)
            {
                db.Entry(student).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name", student.ParentId);
            return View(student);
        }

        // GET: Students/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }

        [RequireLogin]
        public ActionResult MyQRCode()
        {
            if (Session["UserRole"]?.ToString() != "Student")
                return new HttpUnauthorizedResult();

            var studentId = Session["StudentId"] as int?;

            if (!studentId.HasValue)
                return RedirectToAction("Login", "Account");

            var student = db.Students.Find(studentId.Value);

            if (student == null)
                return HttpNotFound();

            var qrService = new StudentQRCodeService();

            var qr = qrService.GenerateQRCode(student.StudentId);

            ViewBag.Student = student;

            return View("~/Views/StudentQRCode/Details.cshtml", qr);
        }
        [RequireLogin]
        public ActionResult MyQRCodeImage()
        {
            if (Session["UserRole"]?.ToString() != "Student")
                return new HttpUnauthorizedResult();

            var studentId = Session["StudentId"] as int?;

            if (!studentId.HasValue)
                return new HttpUnauthorizedResult();

            var qrService = new StudentQRCodeService();
            var qr = qrService.GetActiveQRCode(studentId.Value);

            if (qr == null)
                qr = qrService.GenerateQRCode(studentId.Value);

            if (qr.QRImage == null || qr.QRImage.Length == 0)
            {
                qr.QRImage = qrService.GenerateQRImage(qr.QRCodeValue);

                using (var db = new DBContextClass())
                {
                    var existing = db.StudentQRCodes.Find(qr.QRCodeId);

                    if (existing != null)
                    {
                        existing.QRImage = qr.QRImage;
                        db.SaveChanges();
                    }
                }
            }

            return File(qr.QRImage, "image/png");
        }
        [RequireLogin]
        public ActionResult DownloadMyQRCode()
        {
            if (Session["UserRole"]?.ToString() != "Student")
                return new HttpUnauthorizedResult();

            var studentId = Session["StudentId"] as int?;

            if (!studentId.HasValue)
                return RedirectToAction("Login", "Account");

            using (var db = new DBContextClass())
            {
                var student = db.Students.Find(studentId.Value);

                if (student == null)
                    return HttpNotFound();

                var qrService = new StudentQRCodeService();

                var qr = qrService.GetActiveQRCode(studentId.Value);

                if (qr == null)
                    qr = qrService.GenerateQRCode(studentId.Value);

                if (qr.QRImage == null || qr.QRImage.Length == 0)
                {
                    qr.QRImage = qrService.GenerateQRImage(qr.QRCodeValue);

                    var existing = db.StudentQRCodes.Find(qr.QRCodeId);

                    if (existing != null)
                    {
                        existing.QRImage = qr.QRImage;
                        db.SaveChanges();
                    }
                }

                var fileName =
                    $"Michaelhouse-QR-{student.FirstName}-{student.LastName}.png";

                return File(qr.QRImage, "image/png", fileName);
            }
        }


        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Student student = db.Students.Find(id);
            db.Students.Remove(student);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        public ActionResult Dashboard()
        {
            int userId = (int)Session["UserId"];
            using (var db = new DBContextClass())
            {
                // CRITICAL: .Include("Parent") ensures the Guardian data is loaded
                var student = db.Students
                    .Include("Parent")
                    .FirstOrDefault(s => s.UserId == userId);

                if (student == null) return RedirectToAction("Login", "Account");

                // ====== EMERGENCY OVERRIDE (SERVER‑SIDE) ======
                var activeAlert = db.EmergencyAlerts
                    .Where(a => a.Status == AlertStatus.Active)
                    .OrderByDescending(a => a.AlertTime)
                    .FirstOrDefault();

                if (activeAlert != null)
                {
                    bool alreadyConfirmed = db.StudentSafetyConfirmations
    .Any(c => c.AlertId == activeAlert.AlertId
              && c.StudentId == student.StudentId
              && (c.Status == SafetyStatus.Confirmed || c.Status == SafetyStatus.OutsideZone));

                    if (!alreadyConfirmed)
                    {
                        // Redirect to the emergency safety confirmation page
                        return RedirectToAction("ConfirmSafety", "Emergency");
                    }
                }

                var reg = db.Registrations
                    .FirstOrDefault(r => r.StudentId == student.StudentId);

                // CRITICAL: .Include("Subject") ensures the Subject names are loaded
                var subjects = db.StudentSubjects
                    .Include("Subject")
                    .Where(ss => ss.StudentId == student.StudentId)
                    .ToList();

                var streamEnr = db.StreamEnrolments
                    .FirstOrDefault(se => se.StudentId == student.StudentId);

                ViewBag.Student = student;
                ViewBag.Registration = reg;
                ViewBag.Subjects = subjects;
                ViewBag.StreamEnrolment = streamEnr;

                return View();
            }
        }
        [RequireLogin]
        public ActionResult MyAttendance(DateTime? fromDate, DateTime? toDate)
        {
            int studentId = (int)(Session["StudentId"] ?? 0);
            if (studentId == 0) return RedirectToAction("Login", "Account");

            using (var db = new DBContextClass())
            {
                var query = db.Attendances
                    .Where(a => a.StudentId == studentId)
                    .Include(a => a.Subject)
                    .AsQueryable();

                if (fromDate.HasValue)
                    query = query.Where(a => a.Date >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(a => a.Date <= toDate.Value);

                // 1. SUMMARY: Group by SubjectId
                var summaryRaw = query
                    .GroupBy(a => a.SubjectId)
                    .Select(g => new
                    {
                        SubjectId = g.Key,
                        TotalClasses = g.Count(),
                        PresentCount = g.Count(a => a.Status == AttendanceStatus.Present),
                        LateCount = g.Count(a => a.Status == AttendanceStatus.Late),
                        AbsentCount = g.Count(a => a.Status == AttendanceStatus.Absent)
                    })
                    .ToList();

                var summary = (from s in summaryRaw
                               join subject in db.Subjects on s.SubjectId equals subject.SubjectId
                               select new StudentAttendanceSummaryViewModel
                               {
                                   SubjectId = s.SubjectId,
                                   SubjectName = subject.Name,
                                   TotalClasses = s.TotalClasses,
                                   PresentCount = s.PresentCount,
                                   LateCount = s.LateCount,
                                   AbsentCount = s.AbsentCount
                               })
                               .OrderBy(x => x.SubjectName)
                               .ToList();

                // 2. DETAILED records
                var records = query
                    .OrderByDescending(a => a.Date)
                    .Select(a => new AttendanceRecordViewModel
                    {
                        Date = a.Date,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    })
                    .ToList();

                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.AttendanceSummary = summary;   // pass to view

                return View(records);
            }
        }
        public ActionResult GetSubjectAttendanceDetails(int subjectId)
        {
            int studentId = (int)(Session["StudentId"] ?? 0);
            if (studentId == 0) return Content("<p class='text-sm text-red-500'>Not authenticated.</p>");

            using (var db = new DBContextClass())
            {
                var records = db.Attendances
                    .Include(a => a.Subject)
                    .Where(a => a.StudentId == studentId && a.SubjectId == subjectId)
                    .OrderByDescending(a => a.Date)
                    .Select(a => new AttendanceRecordViewModel
                    {
                        Date = a.Date,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    })
                    .ToList();

                if (!records.Any())
                    return Content("<p class='text-sm text-gray-500'>No attendance records for this subject.</p>");

                return PartialView("_SubjectAttendanceDetails", records);
            }
        }
        [RequireLogin]
        public ActionResult ScanQRCode()
        {
            return View();
        }

        [HttpPost]
        public JsonResult VerifyScan()
        {
            try
            {
                // Read raw JSON body
                Request.InputStream.Position = 0;
                string json = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                    return Json(new { approved = false, reason = "Empty request." });

                dynamic payload = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                string qrValue = payload?.qrValue;

                var verifier = new AIQRCodeVerificationService();
                var result = verifier.VerifyByValue(qrValue, "WebScanner");
                return Json(new { approved = result.Approved, reason = result.Reason });
            }
            catch (Exception ex)
            {
                return Json(new { approved = false, reason = ex.Message });
            }
        }

    }
}
