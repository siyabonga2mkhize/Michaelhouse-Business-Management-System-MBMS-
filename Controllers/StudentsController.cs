using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
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
            if (Session["StudentId"] == null) return 0;
            return (int)Session["StudentId"];
        }

        private bool IsStudent()
        {
            return Session["UserRole"]?.ToString() == "Student";
        }

        // ══════════════════════════════════════════════════════════════
        // CRUD (unchanged)
        // ══════════════════════════════════════════════════════════════
        public ActionResult Index()
        {
            var students = db.Students.Include(s => s.Parent);
            return View(students.ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Student student = db.Students.Find(id);
            if (student == null) return HttpNotFound();
            return View(student);
        }

        public ActionResult Create()
        {
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name");
            return View();
        }

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

        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Student student = db.Students.Find(id);
            if (student == null) return HttpNotFound();
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name", student.ParentId);
            return View(student);
        }

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

        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Student student = db.Students.Find(id);
            if (student == null) return HttpNotFound();
            return View(student);
        }

        // ══════════════════════════════════════════════════════════════
        // QR CODE (unchanged)
        // ══════════════════════════════════════════════════════════════
        [RequireLogin]
        public ActionResult MyQRCode()
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();
            var studentId = Session["StudentId"] as int?;
            if (!studentId.HasValue) return RedirectToAction("Login", "Account");

            var student = db.Students.Find(studentId.Value);
            if (student == null) return HttpNotFound();

            var qrService = new StudentQRCodeService();
            var qr = qrService.GenerateQRCode(student.StudentId);

            ViewBag.Student = student;
            return View("~/Views/StudentQRCode/Details.cshtml", qr);
        }

        [RequireLogin]
        public ActionResult MyQRCodeImage()
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();
            var studentId = Session["StudentId"] as int?;
            if (!studentId.HasValue) return new HttpUnauthorizedResult();

            var qrService = new StudentQRCodeService();
            var qr = qrService.GetActiveQRCode(studentId.Value);
            if (qr == null) qr = qrService.GenerateQRCode(studentId.Value);

            if (qr.QRImage == null || qr.QRImage.Length == 0)
            {
                qr.QRImage = qrService.GenerateQRImage(qr.QRCodeValue);
                using (var db2 = new DBContextClass())
                {
                    var existing = db2.StudentQRCodes.Find(qr.QRCodeId);
                    if (existing != null)
                    {
                        existing.QRImage = qr.QRImage;
                        db2.SaveChanges();
                    }
                }
            }
            return File(qr.QRImage, "image/png");
        }

        [RequireLogin]
        public ActionResult DownloadMyQRCode()
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();
            var studentId = Session["StudentId"] as int?;
            if (!studentId.HasValue) return RedirectToAction("Login", "Account");

            using (var db2 = new DBContextClass())
            {
                var student = db2.Students.Find(studentId.Value);
                if (student == null) return HttpNotFound();

                var qrService = new StudentQRCodeService();
                var qr = qrService.GetActiveQRCode(studentId.Value);
                if (qr == null) qr = qrService.GenerateQRCode(studentId.Value);

                if (qr.QRImage == null || qr.QRImage.Length == 0)
                {
                    qr.QRImage = qrService.GenerateQRImage(qr.QRCodeValue);
                    var existing = db2.StudentQRCodes.Find(qr.QRCodeId);
                    if (existing != null)
                    {
                        existing.QRImage = qr.QRImage;
                        db2.SaveChanges();
                    }
                }

                var fileName = $"Michaelhouse-QR-{student.FirstName}-{student.LastName}.png";
                return File(qr.QRImage, "image/png", fileName);
            }
        }

        // ══════════════════════════════════════════════════════════════
        // DASHBOARD (unchanged)
        // ══════════════════════════════════════════════════════════════
        public ActionResult Dashboard()
        {
            int userId = (int)Session["UserId"];
            using (var db2 = new DBContextClass())
            {
                var student = db2.Students.Include("Parent").FirstOrDefault(s => s.UserId == userId);
                if (student == null) return RedirectToAction("Login", "Account");

                var activeAlert = db2.EmergencyAlerts
                    .Where(a => a.Status == AlertStatus.Active)
                    .OrderByDescending(a => a.AlertTime)
                    .FirstOrDefault();

                if (activeAlert != null)
                {
                    bool alreadyConfirmed = db2.StudentSafetyConfirmations
                        .Any(c => c.AlertId == activeAlert.AlertId
                              && c.StudentId == student.StudentId
                              && (c.Status == SafetyStatus.Confirmed || c.Status == SafetyStatus.OutsideZone));
                    if (!alreadyConfirmed)
                        return RedirectToAction("ConfirmSafety", "Emergency");
                }

                var reg = db2.Registrations.FirstOrDefault(r => r.StudentId == student.StudentId);
                var subjects = db2.StudentSubjects.Include("Subject")
                    .Where(ss => ss.StudentId == student.StudentId).ToList();
                var streamEnr = db2.StreamEnrolments.FirstOrDefault(se => se.StudentId == student.StudentId);

                ViewBag.Student = student;
                ViewBag.Registration = reg;
                ViewBag.Subjects = subjects;
                ViewBag.StreamEnrolment = streamEnr;

                return View();
            }
        }

        // ══════════════════════════════════════════════════════════════
        // MY PROFILE (extended — reads structured records)
        // ══════════════════════════════════════════════════════════════
        [RequireLogin]
        public ActionResult MyProfile()
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();

            int studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var student = db.Students.Include("User").FirstOrDefault(s => s.StudentId == studentId);
            if (student == null)
            {
                TempData["Error"] = "Student record not found.";
                return RedirectToAction("Dashboard", "Students");
            }

            var profile = db.StudentProfiles.FirstOrDefault(p => p.StudentId == studentId);

            var residence = student.ResidenceId.HasValue
                ? db.Residences.FirstOrDefault(r => r.ResidenceId == student.ResidenceId.Value)
                : null;

            var assignment = db.ResidenceAssignments
                .Include("Room").Include("Bed")
                .FirstOrDefault(a => a.StudentId == studentId && a.IsActive);

            var dietaryRecords = db.StudentDietaryRecords
                .Include("Allergen").Include("DietaryCategory")
                .Where(r => r.StudentId == studentId
                         && r.IsActive
                         && r.Status == DietaryRequestStatus.Approved)
                .OrderBy(r => r.RecordType).ThenBy(r => r.RequestedAt)
                .ToList();

            var pendingRequests = db.DietaryChangeRequests
                .Where(r => r.StudentId == studentId && r.Status == DietaryRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            var memberships = db.TeamMemberships
                .Include("Team").Include("Team.Sport")
                .Where(m => m.StudentId == studentId && m.EndDate == null)
                .OrderBy(m => m.Team.Name)
                .ToList();

            var vm = new StudentMyProfileViewModel
            {
                FullName = student.FirstName + " " + student.LastName,
                StudentNumber = student.StudentNumber,
                GradeLevel = student.GradeLevel,
                HouseName = residence != null ? residence.Name : "Not assigned",
                RoomNumber = assignment?.Room?.RoomNumber ?? "—",
                BedNumber = assignment?.Bed?.BedNumber ?? "—",
                Email = student.User?.Email ?? "",
                DateOfBirth = student.DOB,
                Gender = profile?.Gender ?? "—",
                BoardingStatus = student.IsBoarding ? "Boarder" : "Day Student",

                PreferredName = profile?.PreferredName,
                HomeLanguage = profile?.HomeLanguage ?? student.HomeLanguage,
                PhoneNumber = profile?.PhoneNumber,

                Allergies = profile?.Allergies,
                MedicalConditions = profile?.MedicalConditions ?? student.MedicalConditions,
                EmergencyMedication = profile?.EmergencyMedication,
                DietaryPreferences = profile?.DietaryPreferences,
                SpecialDietaryNeeds = profile?.SpecialDietaryNeeds,
                Sports = profile?.Sports,

                DietaryRecords = dietaryRecords,
                SportMemberships = memberships,
                PendingRequests = pendingRequests
            };

            return View(vm);
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveMyProfile(StudentMyProfileViewModel vm)
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();

            int studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var student = db.Students.FirstOrDefault(s => s.StudentId == studentId);
            if (student == null) return RedirectToAction("Login", "Account");

            var profile = db.StudentProfiles.FirstOrDefault(p => p.StudentId == studentId);
            if (profile == null)
            {
                profile = new StudentProfile
                {
                    StudentId = studentId,
                    Grade = student.GradeLevel.ToString(),
                    CompletedStep = 0,
                    IsProfileComplete = false
                };
                db.StudentProfiles.Add(profile);
                db.SaveChanges();
            }

            // Only non-sensitive fields are saved directly.
            // Sensitive fields (allergies, medical, dietary, sports) go through
            // /Students/RequestDietaryChange and /Students/MySports.
            profile.PreferredName = vm.PreferredName;
            profile.HomeLanguage = vm.HomeLanguage;
            profile.PhoneNumber = vm.PhoneNumber;

            if (!string.IsNullOrEmpty(vm.HomeLanguage))
                student.HomeLanguage = vm.HomeLanguage;

            db.Entry(profile).State = EntityState.Modified;
            db.Entry(student).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = "Profile saved. " +
                "To change allergies, medical, dietary or sports information, " +
                "use the Manage buttons on the profile page.";
            return RedirectToAction("MyProfile");
        }

        // ══════════════════════════════════════════════════════════════
        // MY DIETARY (student)
        // ══════════════════════════════════════════════════════════════
        [RequireLogin]
        public ActionResult MyDietary()
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var records = db.StudentDietaryRecords
                .Include("Allergen").Include("DietaryCategory")
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.IsActive)
                .ThenByDescending(r => r.RequestedAt)
                .ToList();

            var pending = db.DietaryChangeRequests
                .Where(r => r.StudentId == studentId && r.Status == DietaryRequestStatus.Pending)
                .OrderByDescending(r => r.SubmittedAt)
                .ToList();

            ViewBag.Student = db.Students.FirstOrDefault(s => s.StudentId == studentId);
            ViewBag.PendingRequests = pending;
            return View(records);
        }

        // ══════════════════════════════════════════════════════════════
        // MY SPORTS (student)
        // ══════════════════════════════════════════════════════════════
        [RequireLogin]
        public ActionResult MySports()
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var memberships = db.TeamMemberships
                .Include("Team").Include("Team.Sport").Include("Team.Coach")
                .Where(m => m.StudentId == studentId)
                .OrderByDescending(m => m.EndDate == null)
                .ThenBy(m => m.StartDate)
                .ToList();

            ViewBag.Student = db.Students.FirstOrDefault(s => s.StudentId == studentId);
            return View(memberships);
        }

        // ══════════════════════════════════════════════════════════════
        // REQUEST DIETARY CHANGE
        // ══════════════════════════════════════════════════════════════
        [RequireLogin]
        public ActionResult RequestDietaryChange()
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();

            ViewBag.Allergens = db.Allergens.Where(a => a.IsActive).OrderBy(a => a.Name).ToList();
            ViewBag.Categories = db.DietaryCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
            return View();
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestDietaryChange(int recordType, int? allergenId,
            int? categoryId, string customDescription, string reason)
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            if (recordType < 0 || recordType > 4)
            {
                TempData["Error"] = "Invalid record type.";
                return RedirectToAction("RequestDietaryChange");
            }

            var type = (DietaryRecordType)recordType;

            if (type == DietaryRecordType.Allergy && !allergenId.HasValue && string.IsNullOrWhiteSpace(customDescription))
            {
                TempData["Error"] = "Please specify which allergen or describe the allergy.";
                return RedirectToAction("RequestDietaryChange");
            }
            if (type == DietaryRecordType.DietaryPreference && !categoryId.HasValue && string.IsNullOrWhiteSpace(customDescription))
            {
                TempData["Error"] = "Please select a dietary category or describe your preference.";
                return RedirectToAction("RequestDietaryChange");
            }

            bool safetyReview = type == DietaryRecordType.Allergy || type == DietaryRecordType.MedicalRestriction;

            var request = new DietaryChangeRequest
            {
                StudentId = studentId,
                RecordType = type,
                ChangeType = DietaryChangeType.Add,
                RequestedAllergenId = allergenId,
                RequestedDietaryCategoryId = categoryId,
                RequestedDescription = customDescription,
                Reason = reason,
                Status = DietaryRequestStatus.Pending,
                SubmittedAt = DateTime.Now,
                SafetyReviewRequired = safetyReview
            };

            db.DietaryChangeRequests.Add(request);
            db.SaveChanges();

            new CafeteriaAuditService(db).LogDietaryChangeRequest(request.Id, studentId, type.ToString());

            TempData["Success"] = safetyReview
                ? "Request submitted. A Dietitian will review it. Until then, your current approved restrictions remain active."
                : "Request submitted for review.";

            return RedirectToAction("MyDietary");
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult WithdrawDietaryRequest(int id)
        {
            if (!IsStudent()) return new HttpUnauthorizedResult();
            int studentId = GetCurrentStudentId();
            if (studentId == 0) return RedirectToAction("Login", "Account");

            var req = db.DietaryChangeRequests.FirstOrDefault(r => r.Id == id && r.StudentId == studentId);
            if (req == null) return HttpNotFound();

            if (req.Status == DietaryRequestStatus.Pending)
            {
                req.Status = DietaryRequestStatus.Withdrawn;
                db.SaveChanges();
                new CafeteriaAuditService(db).Log("DietaryRequestWithdrawn", "DietaryChangeRequest", id, "Pending", "Withdrawn");
                TempData["Success"] = "Request withdrawn.";
            }
            return RedirectToAction("MyDietary");
        }

        // ══════════════════════════════════════════════════════════════
        // ATTENDANCE (unchanged)
        // ══════════════════════════════════════════════════════════════
        [RequireLogin]
        public ActionResult MyAttendance(DateTime? fromDate, DateTime? toDate)
        {
            int studentId = (int)(Session["StudentId"] ?? 0);
            if (studentId == 0) return RedirectToAction("Login", "Account");

            using (var db2 = new DBContextClass())
            {
                var query = db2.Attendances.Where(a => a.StudentId == studentId)
                    .Include(a => a.Subject).AsQueryable();

                if (fromDate.HasValue) query = query.Where(a => a.Date >= fromDate.Value);
                if (toDate.HasValue) query = query.Where(a => a.Date <= toDate.Value);

                var summaryRaw = query
                    .GroupBy(a => a.SubjectId)
                    .Select(g => new
                    {
                        SubjectId = g.Key,
                        TotalClasses = g.Count(),
                        PresentCount = g.Count(a => a.Status == AttendanceStatus.Present),
                        LateCount = g.Count(a => a.Status == AttendanceStatus.Late),
                        AbsentCount = g.Count(a => a.Status == AttendanceStatus.Absent)
                    }).ToList();

                var summary = (from s in summaryRaw
                               join subject in db2.Subjects on s.SubjectId equals subject.SubjectId
                               select new StudentAttendanceSummaryViewModel
                               {
                                   SubjectId = s.SubjectId,
                                   SubjectName = subject.Name,
                                   TotalClasses = s.TotalClasses,
                                   PresentCount = s.PresentCount,
                                   LateCount = s.LateCount,
                                   AbsentCount = s.AbsentCount
                               }).OrderBy(x => x.SubjectName).ToList();

                var records = query
                    .OrderByDescending(a => a.Date)
                    .Select(a => new AttendanceRecordViewModel
                    {
                        Date = a.Date,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    }).ToList();

                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
                ViewBag.AttendanceSummary = summary;
                return View(records);
            }
        }

        public ActionResult GetSubjectAttendanceDetails(int subjectId)
        {
            int studentId = (int)(Session["StudentId"] ?? 0);
            if (studentId == 0) return Content("<p class='text-sm text-red-500'>Not authenticated.</p>");

            using (var db2 = new DBContextClass())
            {
                var records = db2.Attendances.Include(a => a.Subject)
                    .Where(a => a.StudentId == studentId && a.SubjectId == subjectId)
                    .OrderByDescending(a => a.Date)
                    .Select(a => new AttendanceRecordViewModel
                    {
                        Date = a.Date,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    }).ToList();

                if (!records.Any())
                    return Content("<p class='text-sm text-gray-500'>No attendance records for this subject.</p>");
                return PartialView("_SubjectAttendanceDetails", records);
            }
        }

        [RequireLogin]
        public ActionResult ScanQRCode() { return View(); }

        [HttpPost]
        public JsonResult VerifyScan()
        {
            try
            {
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}