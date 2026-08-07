using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Authorization;
using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System.Net;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using static Michaelhouse.Filters.TransportManagerOrAdminOnlyAttribute;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class TripController : BaseController
    {
        private DBContextClass db = DbContextFactory.Create();

        private int GetCurrentTeacherId()
        {
            return (int)(Session["TeacherId"] ?? 0);
        }

        private int GetCurrentUserId()
        {
            return (int)(Session["UserId"] ?? 0);
        }

        private List<Student> GetStudentsForTeacher(int teacherId)
        {
            // Get all students that the teacher teaches (via subjects)
            var subjectIds = db.TeacherSubjectGrades
                               .Where(tsg => tsg.TeacherId == teacherId)
                               .Select(tsg => tsg.SubjectId)
                               .ToList();
            var studentIds = db.StudentSubjects
                               .Where(ss => subjectIds.Contains(ss.SubjectId))
                               .Select(ss => ss.StudentId)
                               .Distinct()
                               .ToList();
            return db.Students.Where(s => studentIds.Contains(s.StudentId)).ToList();
        }

        private List<int> GetStudentIds(int scheduleId)
        {
            return db.TripStudents.Where(ts => ts.TripScheduleId == scheduleId)
                                  .Select(ts => ts.StudentId)
                                  .ToList();
        }

        // ── Teacher: Book a trip ─────────────────────────────────────────
        public ActionResult BookTrip(int? rebookId = null)
        {
            var model = new TripRequestCreateViewModel();
            if (rebookId.HasValue)
            {
                var old = db.TripRequests.Find(rebookId.Value);
                if (old != null && old.Status == "Rejected")
                {
                    model.Title = old.Title;
                    model.Description = old.Description;
                    model.DepartureTime = old.DepartureTime;
                    model.ReturnTime = old.ReturnTime;
                    model.Destination = old.Destination;
                    model.DestinationLat = old.DestinationLat;
                    model.DestinationLng = old.DestinationLng;
                    model.MaxStudents = old.MaxStudents;
                    ViewBag.RebookId = rebookId.Value;  // pass to view for hidden field
                }
            }
            return View(model);
        }

        [HttpPost]
        public ActionResult CreateTripRequest(TripRequestCreateViewModel model, int? rebookId = null)
        {
            if (model.DepartureTime <= DateTime.MinValue || model.ReturnTime <= DateTime.MinValue ||
    model.DepartureTime.Year < 2000 || model.ReturnTime.Year < 2000)
            {
                ModelState.AddModelError("", "Please select valid departure and return times.");
                return View(model);
            }
            // Validate that dates are not default (MinValue)
            if (model.DepartureTime <= DateTime.MinValue || model.ReturnTime <= DateTime.MinValue)
            {
                ModelState.AddModelError("", "Please select both departure and return times.");
                return View(model);
            }

            // Also ensure dates are not too old (optional)
            if (model.DepartureTime < DateTime.Today)
            {
                ModelState.AddModelError("DepartureTime", "Departure time cannot be in the past.");
                return View(model);
            }
            if (ModelState.IsValid)
            {
                var request = new TripRequest
                {
                    TeacherId = GetCurrentTeacherId(),
                    Title = model.Title,
                    Description = model.Description,
                    DepartureTime = model.DepartureTime,
                    ReturnTime = model.ReturnTime,
                    Destination = model.Destination,
                    DestinationLat = model.DestinationLat,
                    DestinationLng = model.DestinationLng,
                    MaxStudents = model.MaxStudents,
                    Status = "Pending",
                    RequestedAt = DateTime.Now
                };
                db.TripRequests.Add(request);
                db.SaveChanges();

                // If this is a rebook, delete the original rejected request
                if (rebookId.HasValue)
                {
                    var old = db.TripRequests.Find(rebookId.Value);
                    if (old != null && old.Status == "Rejected")
                        db.TripRequests.Remove(old);
                    db.SaveChanges();
                }

                NotificationHelper.NotifyTransportManagers(db, $"New trip request: {request.Title} (ID:{request.Id})");
                TempData["Success"] = "Trip request sent to Transport Manager.";
                return RedirectToAction("MyRequests");
            }
            return View(model);
        }

        public ActionResult MyRequests()
        {
            int teacherId = GetCurrentTeacherId();
            var requests = db.TripRequests
                             .Where(r => r.TeacherId == teacherId)
                             // REMOVE any .Where(r => r.Status != "Rejected")
                             .OrderByDescending(r => r.RequestedAt)
                             .ToList();
            return View(requests);
        }

        public ActionResult RebookTrip(int requestId)
        {
            var oldRequest = db.TripRequests.Find(requestId);
            if (oldRequest == null || oldRequest.Status != "Rejected")
                return NotFound();

            return RedirectToAction("BookTrip", new { rebookId = requestId });
        }


        // ── Teacher: schedule students after approval ─────────────────────
        public ActionResult ScheduleTrip(int tripRequestId, int? gradeFilter = null, int? subjectFilter = null)
        {
            System.Diagnostics.Debug.WriteLine($"ScheduleTrip called with ID: {tripRequestId}");
            var request = db.TripRequests.Find(tripRequestId);
            if (request == null || request.Status != "Approved")
            {
                TempData["Error"] = "Trip request not found or not approved.";
                return RedirectToAction("MyRequests");
            }

            // Find existing schedule for this trip (if any)
            var existingSchedule = db.TripSchedules.FirstOrDefault(s => s.TripRequestId == tripRequestId);
            int alreadyScheduled = existingSchedule != null
                ? db.TripStudents.Count(ts => ts.TripScheduleId == existingSchedule.Id)
                : 0;
            int remaining = request.MaxStudents - alreadyScheduled;
            if (remaining < 0) remaining = 0;

            // Get IDs of students already scheduled
            List<int> alreadyScheduledIds = new List<int>();
            if (existingSchedule != null)
            {
                alreadyScheduledIds = db.TripStudents
                    .Where(ts => ts.TripScheduleId == existingSchedule.Id)
                    .Select(ts => ts.StudentId)
                    .ToList();
            }
            ViewBag.AlreadyScheduledStudentIds = alreadyScheduledIds;

            int teacherId = GetCurrentTeacherId();
            var teacherStudentIds = GetStudentsForTeacher(teacherId).Select(s => s.StudentId).ToList();

            var studentsQuery = db.Students.Where(s => teacherStudentIds.Contains(s.StudentId));
            if (gradeFilter.HasValue)
                studentsQuery = studentsQuery.Where(s => s.GradeLevel == gradeFilter.Value);
            if (subjectFilter.HasValue)
            {
                var studentIdsInSubject = db.StudentSubjects
                    .Where(ss => ss.SubjectId == subjectFilter.Value)
                    .Select(ss => ss.StudentId)
                    .ToList();
                studentsQuery = studentsQuery.Where(s => studentIdsInSubject.Contains(s.StudentId));
            }
            var students = studentsQuery.OrderBy(s => s.GradeLevel).ThenBy(s => s.LastName).ToList();

            // Get grades and subjects for dropdowns
            var availableGrades = db.Students
                .Where(s => teacherStudentIds.Contains(s.StudentId))
                .Select(s => s.GradeLevel)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            var availableSubjects = db.TeacherSubjectGrades
                .Where(tsg => tsg.TeacherId == teacherId)
                .Select(tsg => tsg.Subject)
                .Distinct()
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.TripRequest = request;
            ViewBag.TripRequestId = request.Id;
            ViewBag.RemainingCapacity = remaining;
            ViewBag.MaxStudents = request.MaxStudents;
            ViewBag.Grades = availableGrades;
            ViewBag.Subjects = availableSubjects;
            ViewBag.SelectedGrade = gradeFilter;
            ViewBag.SelectedSubject = subjectFilter;
            ViewBag.ExistingSchedule = existingSchedule;

            return View(students);
        }

        [HttpPost]
        public ActionResult CreateSchedule(int? tripRequestId, List<int> selectedStudentIds, DateTime scheduledDate)
        {
            if (tripRequestId == null)
            {
                TempData["Error"] = "Trip request ID is missing.";
                return RedirectToAction("MyRequests");
            }

            if (selectedStudentIds == null || !selectedStudentIds.Any())
            {
                TempData["Error"] = "Please select at least one student.";
                return RedirectToAction("ScheduleTrip", new { tripRequestId = tripRequestId.Value });
            }

            var tripRequest = db.TripRequests.Find(tripRequestId.Value);
            if (tripRequest == null)
            {
                TempData["Error"] = "Trip request not found.";
                return RedirectToAction("MyRequests");
            }

            var existingSchedule = db.TripSchedules.FirstOrDefault(ts => ts.TripRequestId == tripRequestId.Value);
            int existingCount = existingSchedule != null
                ? db.TripStudents.Count(ts => ts.TripScheduleId == existingSchedule.Id)
                : 0;

            if (selectedStudentIds.Count + existingCount > tripRequest.MaxStudents)
            {
                TempData["Error"] = $"Cannot schedule more than {tripRequest.MaxStudents} students. " +
                                    $"Already scheduled: {existingCount}, you tried to add {selectedStudentIds.Count}.";
                return RedirectToAction("ScheduleTrip", new { tripRequestId = tripRequestId.Value });
            }

            TripSchedule schedule;
            if (existingSchedule != null)
            {
                if (existingSchedule.Status == "Draft")
                {
                    schedule = existingSchedule;
                    schedule.ScheduledDate = scheduledDate;
                    var existingStudents = db.TripStudents.Where(ts => ts.TripScheduleId == schedule.Id).ToList();
                    db.TripStudents.RemoveRange(existingStudents);
                    db.SaveChanges();
                }
                else
                {
                    TempData["Error"] = "A schedule already exists for this trip and cannot be modified.";
                    return RedirectToAction("MyRequests");
                }
            }
            else
            {
                schedule = new TripSchedule
                {
                    TripRequestId = tripRequestId.Value,
                    TeacherId = GetCurrentTeacherId(),
                    ScheduledDate = scheduledDate,
                    Status = "Draft"
                };
                db.TripSchedules.Add(schedule);
                db.SaveChanges();
            }

            foreach (var sid in selectedStudentIds)
            {
                db.TripStudents.Add(new TripStudent
                {
                    TripScheduleId = schedule.Id,
                    StudentId = sid
                });
            }
            db.SaveChanges();

            NotificationHelper.NotifyTransportManagers(db, $"Trip schedule updated for '{tripRequest.Title}'. Assignment needed.");
            TempData["Success"] = "Schedule saved. Transport Manager will assign driver & vehicle.";
            return RedirectToAction("UpcomingTrips");
        }

        public ActionResult UpcomingTrips()
        {
            int teacherId = GetCurrentTeacherId();
            var trips = db.TripSchedules
                .Include(ts => ts.TripRequest)
                .Include(ts => ts.Driver)
                .Include(ts => ts.Vehicle)
                .Include("VehicleAssignments.Vehicle")
                .Include(ts => ts.TripStudents)
                .Where(ts => ts.TeacherId == teacherId && ts.Status != "Completed")
                .OrderBy(ts => ts.ScheduledDate)
                .ToList();

            return View(trips);
        }
        [HttpPost]
        public ActionResult CancelSchedule(int scheduleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null || schedule.TeacherId != GetCurrentTeacherId())
            {
                return NotFound();
            }
            schedule.Status = "Cancelled";
            db.SaveChanges();

            TempData["Success"] = "Trip cancelled successfully.";
            return RedirectToAction("UpcomingTrips");
        }

        // ── Manifest marking (before/after) ──────────────────────────────
        public ActionResult Manifest(int? scheduleId)
        {
            var schedule = db.TripSchedules
                .Include("TripRequest")
                .Include("TripStudents.Student.Parent")
                .FirstOrDefault(s => s.Id == scheduleId);
            if (schedule == null) return NotFound();

            int currentTeacherId = GetCurrentTeacherId();
            int? currentDriverId = (int?)Session["DriverId"];

            // Security: only assigned teacher or driver
            if (schedule.TeacherId != currentTeacherId && (currentDriverId == null || schedule.DriverId != currentDriverId))
                return StatusCode(403);

            // Generate tokens and QR codes for each student
            var students = new List<ManifestStudentViewModel>();
            foreach (var tripStudent in schedule.TripStudents)
            {
                int studentId = tripStudent.StudentId;
                var student = tripStudent.Student;

                // --- Departure token (Before) ---
                var beforeToken = db.StudentAttendanceTokens
                    .FirstOrDefault(t => t.TripScheduleId == schedule.Id && t.StudentId == studentId && t.Type == "Before");
                if (beforeToken == null)
                {
                    beforeToken = new StudentAttendanceToken
                    {
                        StudentId = studentId,
                        TripScheduleId = schedule.Id,
                        Token = Guid.NewGuid().ToString("N"),
                        Type = "Before",
                        IsUsed = false,
                        CreatedAt = DateTime.Now,
                        ExpiryDate = schedule.ScheduledDate.AddDays(1)
                    };
                    db.StudentAttendanceTokens.Add(beforeToken);
                }

                // --- Return token (After) ---
                var afterToken = db.StudentAttendanceTokens
                    .FirstOrDefault(t => t.TripScheduleId == schedule.Id && t.StudentId == studentId && t.Type == "After");
                if (afterToken == null)
                {
                    afterToken = new StudentAttendanceToken
                    {
                        StudentId = studentId,
                        TripScheduleId = schedule.Id,
                        Token = Guid.NewGuid().ToString("N"),
                        Type = "After",
                        IsUsed = false,
                        CreatedAt = DateTime.Now,
                        ExpiryDate = schedule.ScheduledDate.AddDays(1)
                    };
                    db.StudentAttendanceTokens.Add(afterToken);
                }
                db.SaveChanges();

                // Generate QR code data URLs
                string beforeUrl = Url.Action("MarkAttendance", "Trip", new { token = beforeToken.Token }, new Uri(Request.GetDisplayUrl()).Scheme);
                string afterUrl = Url.Action("MarkAttendance", "Trip", new { token = afterToken.Token }, new Uri(Request.GetDisplayUrl()).Scheme);

                students.Add(new ManifestStudentViewModel
            {
                    StudentId = studentId,
                    StudentName = student.FirstName + " " + student.LastName,
                    ParentName = student.Parent?.Name ?? "N/A",
                    EmergencyContactName = student.Parent?.EmergencyContactName ?? "Not provided",
                    EmergencyContactPhone = student.Parent?.EmergencyContactPhone ?? "",
                    AlreadyPresentBefore = tripStudent.IsPresentBefore ?? false,
                    AlreadyPresentAfter = tripStudent.IsPresentAfter ?? false,
                    IsPresentBefore = tripStudent.IsPresentBefore ?? false,
                    IsPresentAfter = tripStudent.IsPresentAfter ?? false,
                    BeforeQR = GenerateQRCodeDataUrl(beforeUrl),
                    AfterQR = GenerateQRCodeDataUrl(afterUrl)
                });
            }

            ViewBag.Schedule = schedule;
            return View(students);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkManifest(int? scheduleId, List<ManifestStudentViewModel> students, string markType)
        {
            System.Diagnostics.Debug.WriteLine($"Received scheduleId: {scheduleId}");
            if (scheduleId == null || scheduleId == 0)
            {
                TempData["Error"] = "Schedule ID missing or invalid.";
                return RedirectToAction("UpcomingTrips");
            }

            var schedule = db.TripSchedules.Include(s => s.TripRequest).FirstOrDefault(s => s.Id == scheduleId.Value);
            if (schedule == null)
            {
                TempData["Error"] = "Trip schedule not found.";
                return RedirectToAction("UpcomingTrips");
            }

            int userId = GetCurrentUserId();

            foreach (var vm in students)
            {
                var ts = db.TripStudents.FirstOrDefault(t => t.TripScheduleId == scheduleId.Value && t.StudentId == vm.StudentId);
                if (ts == null) continue;

                if (markType == "before")
                {
                    ts.IsPresentBefore = vm.IsPresentBefore;
                    if (vm.IsPresentBefore)
                    {
                        ts.MarkedBeforeBy = userId.ToString();
                        ts.MarkedBeforeAt = DateTime.Now;
                    }
                }
                else if (markType == "after")
                {
                    ts.IsPresentAfter = vm.IsPresentAfter;
                    if (vm.IsPresentAfter)
                    {
                        ts.MarkedAfterBy = userId.ToString();
                        ts.MarkedAfterAt = DateTime.Now;
                    }
                }
            }
            db.SaveChanges();

            if (markType == "before")
            {
                var studentIds = students.Where(s => s.IsPresentBefore).Select(s => s.StudentId).ToList();
                if (studentIds.Any())
                    NotificationHelper.NotifyParents(db, studentIds, $"Your child has been marked PRESENT for departure of trip '{schedule.TripRequest.Title}'.");
                NotificationHelper.NotifyTeacher(db, schedule.TeacherId, "Pre‑trip manifest saved.");
                TempData["Success"] = "Pre‑trip attendance saved.";
            }
            else if (markType == "after")
            {
                var studentIds = students.Where(s => s.IsPresentAfter).Select(s => s.StudentId).ToList();
                if (studentIds.Any())
                    NotificationHelper.NotifyParents(db, studentIds, $"Your child has returned safely from trip '{schedule.TripRequest.Title}'.");
                NotificationHelper.NotifyTeacher(db, schedule.TeacherId, "Post‑trip manifest saved. Trip completed.");
                schedule.Status = "Completed";
                db.SaveChanges();
                TempData["Success"] = "Post‑trip attendance saved. Trip marked as completed.";
            }
            else
            {
                TempData["Error"] = "Invalid mark type.";
            }

            return RedirectToAction("UpcomingTrips");
        }

        private void NotifyParentForStudent(int studentId, string message)
        {
            using (var db = DbContextFactory.Create())
            {
                var student = db.Students.Include(s => s.Parent).FirstOrDefault(s => s.StudentId == studentId);
                if (student?.Parent?.UserId != null)
                {
                    NotificationHelper.Send(db, student.Parent.UserId.Value, message, "Trip", 0);
                }
            }
        }

        public ActionResult GetFilteredStudents(int tripRequestId, int? gradeFilter = null, int? subjectFilter = null)
        {
            var request = db.TripRequests.Find(tripRequestId);
            if (request == null)
                return Content("<div class='alert alert-danger'>Trip not found</div>");

            // Find existing schedule and already scheduled student IDs
            var existingSchedule = db.TripSchedules.FirstOrDefault(s => s.TripRequestId == tripRequestId);
            List<int> alreadyScheduledIds = new List<int>();
            if (existingSchedule != null)
            {
                alreadyScheduledIds = db.TripStudents
                    .Where(ts => ts.TripScheduleId == existingSchedule.Id)
                    .Select(ts => ts.StudentId)
                    .ToList();
            }
            ViewBag.AlreadyScheduledStudentIds = alreadyScheduledIds;

            int teacherId = GetCurrentTeacherId();
            var teacherStudentIds = GetStudentsForTeacher(teacherId).Select(s => s.StudentId).ToList();

            var studentsQuery = db.Students.Where(s => teacherStudentIds.Contains(s.StudentId));
            if (gradeFilter.HasValue)
                studentsQuery = studentsQuery.Where(s => s.GradeLevel == gradeFilter.Value);
            if (subjectFilter.HasValue)
            {
                var studentIdsInSubject = db.StudentSubjects
                    .Where(ss => ss.SubjectId == subjectFilter.Value)
                    .Select(ss => ss.StudentId)
                    .ToList();
                studentsQuery = studentsQuery.Where(s => studentIdsInSubject.Contains(s.StudentId));
            }

            var students = studentsQuery.OrderBy(s => s.GradeLevel).ThenBy(s => s.LastName).ToList();
            return PartialView("_StudentCheckboxList", students);
        }

        public ActionResult Create()
        {
            return View();
        }
        // GET: Trip/GenerateQRCodes/5
        [AdminOrTransportManagerOnly]  // or TeacherOnly – adjust to your security
        public ActionResult GenerateQRCodes(int scheduleId)
        {
            var schedule = db.TripSchedules
                .Include(s => s.TripRequest)
                .Include(s => s.TripStudents.Select(ts => ts.Student))
                .FirstOrDefault(s => s.Id == scheduleId);

            if (schedule == null) return NotFound();

            // Optional: ensure teacher is the one who created the schedule
            if (schedule.TeacherId != GetCurrentTeacherId() && !User.IsInRole("Admin"))
                return StatusCode(403);

            var tokens = new List<StudentAttendanceToken>();

            foreach (var tripStudent in schedule.TripStudents)
            {
                int studentId = tripStudent.StudentId;

                // Generate departure token (Before) if not already generated and not used
                var existingBefore = db.StudentAttendanceTokens
                    .FirstOrDefault(t => t.TripScheduleId == scheduleId && t.StudentId == studentId && t.Type == "Before");
                if (existingBefore == null)
                {
                    var beforeToken = new StudentAttendanceToken
                    {
                        StudentId = studentId,
                        TripScheduleId = scheduleId,
                        Token = Guid.NewGuid().ToString("N"),
                        Type = "Before",
                        IsUsed = false,
                        CreatedAt = DateTime.Now,
                        ExpiryDate = schedule.ScheduledDate.AddDays(1)  // valid until day after trip
                    };
                    db.StudentAttendanceTokens.Add(beforeToken);
                    tokens.Add(beforeToken);
                }
                else
                {
                    tokens.Add(existingBefore);
                }

                // Generate return token (After) similarly
                var existingAfter = db.StudentAttendanceTokens
                    .FirstOrDefault(t => t.TripScheduleId == scheduleId && t.StudentId == studentId && t.Type == "After");
                if (existingAfter == null)
                {
                    var afterToken = new StudentAttendanceToken
                    {
                        StudentId = studentId,
                        TripScheduleId = scheduleId,
                        Token = Guid.NewGuid().ToString("N"),
                        Type = "After",
                        IsUsed = false,
                        CreatedAt = DateTime.Now,
                        ExpiryDate = schedule.ScheduledDate.AddDays(1)
                    };
                    db.StudentAttendanceTokens.Add(afterToken);
                    tokens.Add(afterToken);
                }
                else
                {
                    tokens.Add(existingAfter);
                }
            }
            db.SaveChanges();

            // Build view model with student info and QR codes
            var qrViewModel = schedule.TripStudents.Select(ts => new Michaelhouse.Models.ViewModels.QRCodeViewModel
            {
                StudentName = ts.Student.FirstName + " " + ts.Student.LastName,
                StudentId = ts.StudentId,
                BeforeToken = tokens.FirstOrDefault(t => t.StudentId == ts.StudentId && t.Type == "Before")?.Token,
                AfterToken = tokens.FirstOrDefault(t => t.StudentId == ts.StudentId && t.Type == "After")?.Token,
                BeforeQR = tokens.FirstOrDefault(t => t.StudentId == ts.StudentId && t.Type == "Before")?.Token != null
                    ? GenerateQRCodeDataUrl(Url.Action("MarkAttendance", "Trip", new { token = tokens.First(t => t.StudentId == ts.StudentId && t.Type == "Before").Token }, new Uri(Request.GetDisplayUrl()).Scheme))
                    : null,
                AfterQR = tokens.FirstOrDefault(t => t.StudentId == ts.StudentId && t.Type == "After")?.Token != null
                    ? GenerateQRCodeDataUrl(Url.Action("MarkAttendance", "Trip", new { token = tokens.First(t => t.StudentId == ts.StudentId && t.Type == "After").Token }, new Uri(Request.GetDisplayUrl()).Scheme))
                    : null
            }).ToList();

            ViewBag.Schedule = schedule;
            return View(qrViewModel);
        }

        // Helper: generate QR code as base64 image data URL
        private string GenerateQRCodeDataUrl(string url)
        {
            var encoded = Uri.EscapeDataString(url ?? string.Empty);
            var chartUrl = $"https://chart.googleapis.com/chart?cht=qr&chs=300x300&chl={encoded}&chld=Q|1";

            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "MichaelhouseQRCodeGenerator/1.0");
                var bytes = wc.DownloadData(chartUrl);
                return "data:image/png;base64," + Convert.ToBase64String(bytes);
            }
        }

        // GET: Trip/MarkAttendance?token=abc123
        [AllowAnonymous]
        public ActionResult MarkAttendance(string token)
        {
            if (string.IsNullOrEmpty(token))
                return View("AttendanceError", new { message = "Invalid QR code." });

            var tokenRecord = db.StudentAttendanceTokens
                .Include(t => t.Student)
                .Include(t => t.TripSchedule)
                .FirstOrDefault(t => t.Token == token);

            if (tokenRecord == null)
                return View("AttendanceError", new { message = "QR code not recognised." });

            if (tokenRecord.IsUsed)
                return View("AttendanceError", new { message = "This QR code has already been used." });

            if (tokenRecord.ExpiryDate.HasValue && tokenRecord.ExpiryDate < DateTime.Now)
                return View("AttendanceError", new { message = "This QR code has expired." });

            // Find the TripStudent record
            var tripStudent = db.TripStudents
                .FirstOrDefault(ts => ts.TripScheduleId == tokenRecord.TripScheduleId && ts.StudentId == tokenRecord.StudentId);

            if (tripStudent == null)
                return View("AttendanceError", new { message = "Student not assigned to this trip." });

            // Mark attendance based on token type
            if (tokenRecord.Type == "Before")
            {
                if (tripStudent.IsPresentBefore == true)
                    return View("AttendanceError", new { message = "Departure already marked for this student." });

                tripStudent.IsPresentBefore = true;
                tripStudent.MarkedBeforeAt = DateTime.Now;
                tripStudent.MarkedBeforeBy = "Student-" + tokenRecord.StudentId; // or store student name
            }
            else // "After"
            {
                if (tripStudent.IsPresentAfter == true)
                    return View("AttendanceError", new { message = "Return already marked for this student." });

                tripStudent.IsPresentAfter = true;
                tripStudent.MarkedAfterAt = DateTime.Now;
                tripStudent.MarkedAfterBy = "Student-" + tokenRecord.StudentId;
            }

            tokenRecord.IsUsed = true;
            db.SaveChanges();

            // Optional: send confirmation email or push notification
            return View("AttendanceSuccess", new { studentName = tokenRecord.Student.FirstName + " " + tokenRecord.Student.LastName, type = tokenRecord.Type });
        }
        // GET: Trip/StudentCheckIn?scheduleId=5
        [AllowAnonymous]
        public ActionResult StudentCheckIn(int scheduleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return NotFound();

            var students = db.TripStudents
                .Where(ts => ts.TripScheduleId == scheduleId)
                .Select(ts => ts.Student)
                .ToList();

            ViewBag.ScheduleId = scheduleId;
            return View(students);
        }
        [HttpPost]
        [AllowAnonymous]
        public JsonResult MarkAttendanceByStudent(int scheduleId, int studentId, string type)
        {
            try
            {
                var tripStudent = db.TripStudents
                    .FirstOrDefault(ts => ts.TripScheduleId == scheduleId && ts.StudentId == studentId);
                if (tripStudent == null)
                    return Json(new { success = false, message = "Student not assigned to this trip." });

                if (type == "before")
                {
                    if (tripStudent.IsPresentBefore == true)
                        return Json(new { success = false, message = "Already marked departure." });
                    tripStudent.IsPresentBefore = true;
                    tripStudent.MarkedBeforeAt = DateTime.Now;
                    tripStudent.MarkedBeforeBy = $"Student-{studentId}";
                }
                else if (type == "after")
                {
                    if (tripStudent.IsPresentAfter == true)
                        return Json(new { success = false, message = "Already marked return." });
                    tripStudent.IsPresentAfter = true;
                    tripStudent.MarkedAfterAt = DateTime.Now;
                    tripStudent.MarkedAfterBy = $"Student-{studentId}";
                }
                else
                    return Json(new { success = false, message = "Invalid type." });

                db.SaveChanges();
                return Json(new { success = true, message = $"Attendance marked for {type}." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [RequireLogin]
        public ActionResult StudentAutoCheckIn(int scheduleId, string type)
        {
            if (type != "before" && type != "after")
            {
                ViewBag.Message = "Invalid check‑in type.";
                return View("AttendanceError");
            }

            int currentUserId = GetCurrentUserId();
            var student = db.Students.FirstOrDefault(s => s.UserId == currentUserId);
            if (student == null)
            {
                ViewBag.Message = "Your account is not linked to a student profile.";
                return View("AttendanceError");
            }

            var tripStudent = db.TripStudents
                .FirstOrDefault(ts => ts.TripScheduleId == scheduleId && ts.StudentId == student.StudentId);
            if (tripStudent == null)
            {
                ViewBag.Message = "You are not assigned to this trip.";
                return View("AttendanceError");
            }

            if (type == "before")
            {
                if (tripStudent.IsPresentBefore == true)
                {
                    ViewBag.Message = "You already marked departure for this trip.";
                    return View("AttendanceError");
                }
                tripStudent.IsPresentBefore = true;
                tripStudent.MarkedBeforeAt = DateTime.Now;
                tripStudent.MarkedBeforeBy = $"Student-{student.StudentId}";
            }
            else // after
            {
                if (tripStudent.IsPresentAfter == true)
                {
                    ViewBag.Message = "You already marked return for this trip.";
                    return View("AttendanceError");
                }
                tripStudent.IsPresentAfter = true;
                tripStudent.MarkedAfterAt = DateTime.Now;
                tripStudent.MarkedAfterBy = $"Student-{student.StudentId}";
            }

            db.SaveChanges();

            // Success view
            ViewBag.StudentName = student.FirstName + " " + student.LastName;
            ViewBag.Type = type;
            return View("AttendanceSuccess");
        }

        // GET: Trip/DepartureQRCode/5
        //[AdminOrTransportManagerOnly]
        public ActionResult DepartureQRCode(int scheduleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return NotFound();

            string qrUrl = Url.Action("StudentAutoCheckIn", "Trip", new { scheduleId, type = "before" }, new Uri(Request.GetDisplayUrl()).Scheme);
            string qrImage = GenerateQRCodeDataUrl(qrUrl);

            ViewBag.Schedule = schedule;
            ViewBag.QRCodeImage = qrImage;
            ViewBag.Type = "Departure";
            ViewBag.Instruction = "Scan to mark your departure";
            return View("SingleQRCode");
        }

        // GET: Trip/ReturnQRCode/5
        //[AdminOrTransportManagerOnly]
        public ActionResult ReturnQRCode(int scheduleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return NotFound();

            string qrUrl = Url.Action("StudentAutoCheckIn", "Trip", new { scheduleId, type = "after" }, new Uri(Request.GetDisplayUrl()).Scheme);
            string qrImage = GenerateQRCodeDataUrl(qrUrl);

            ViewBag.Schedule = schedule;
            ViewBag.QRCodeImage = qrImage;
            ViewBag.Type = "Return";
            ViewBag.Instruction = "Scan after the trip to mark your return";
            return View("SingleQRCode");
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }


    }
}