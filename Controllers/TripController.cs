using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class TripController : Controller
    {
        private DBContextClass db = new DBContextClass();

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
        public ActionResult BookTrip()
        {
            return View(new TripRequestCreateViewModel());
        }

        [HttpPost]
        public ActionResult CreateTripRequest(TripRequestCreateViewModel model)
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
                    MaxStudents = model.MaxStudents,
                    Status = "Pending",
                    RequestedAt = DateTime.Now
                };
                db.TripRequests.Add(request);
                db.SaveChanges();

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
                             .OrderByDescending(r => r.RequestedAt)
                             .ToList();
            return View(requests);
        }

        // ── Teacher: schedule students after approval ─────────────────────
        public ActionResult ScheduleTrip(int tripRequestId, int? gradeFilter = null, int? subjectFilter = null)
        {
            var request = db.TripRequests.Find(tripRequestId);
            if (request == null || request.Status != "Approved")
            {
                TempData["Error"] = "Trip request not found or not approved.";
                return RedirectToAction("MyRequests");
            }

            int teacherId = GetCurrentTeacherId();

            // Get all student IDs that the teacher can take (based on subjects)
            var teacherStudentIds = GetStudentsForTeacher(teacherId).Select(s => s.StudentId).ToList();

            // Prepare filter dropdowns (using the IDs)
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

            // Build student query with filters
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

            ViewBag.TripRequest = request;
            ViewBag.TripRequestId = request.Id;
            ViewBag.Grades = availableGrades;
            ViewBag.Subjects = availableSubjects;
            ViewBag.SelectedGrade = gradeFilter;
            ViewBag.SelectedSubject = subjectFilter;

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

            // Check if a schedule already exists for this trip request
            var existingSchedule = db.TripSchedules.FirstOrDefault(ts => ts.TripRequestId == tripRequestId.Value);
            TripSchedule schedule;
            if (existingSchedule != null)
            {
                // If exists and still in Draft, update it
                if (existingSchedule.Status == "Draft")
                {
                    schedule = existingSchedule;
                    schedule.ScheduledDate = scheduledDate;
                    // Remove existing students and add new ones (or merge)
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

            // Add students
            foreach (var sid in selectedStudentIds)
            {
                db.TripStudents.Add(new TripStudent
                {
                    TripScheduleId = schedule.Id,
                    StudentId = sid
                });
            }
            db.SaveChanges();

            var req = db.TripRequests.Find(tripRequestId.Value);
            NotificationHelper.NotifyTransportManagers(db, $"Trip schedule updated for '{req.Title}'. Assignment needed.");
            TempData["Success"] = "Schedule saved. Transport Manager will assign driver & vehicle.";
            return RedirectToAction("UpcomingTrips");
        }

        public ActionResult UpcomingTrips()
        {
            int teacherId = GetCurrentTeacherId();
            var trips = db.TripSchedules
                .Include(ts => ts.TripRequest)
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
                return HttpNotFound();
            }
            schedule.Status = "Cancelled";
            db.SaveChanges();

            TempData["Success"] = "Trip cancelled successfully.";
            return RedirectToAction("UpcomingTrips");
        }

        // ── Manifest marking (before/after) ──────────────────────────────
        public ActionResult Manifest(int scheduleId)
        {
            var schedule = db.TripSchedules
                             .Include(s => s.TripRequest)
                             .Include(s => s.TripStudents)
                             .FirstOrDefault(s => s.Id == scheduleId);
            if (schedule == null) return HttpNotFound();

            int currentTeacherId = GetCurrentTeacherId();
            int? currentDriverId = (int?)Session["DriverId"]; // if driver logged in (add session variable)
            if (schedule.TeacherId != currentTeacherId && (currentDriverId == null || schedule.DriverId != currentDriverId))
                return new HttpUnauthorizedResult();

            ViewBag.Schedule = schedule;
            return View(schedule.TripStudents);
        }

        [HttpPost]
        public ActionResult MarkManifest(int scheduleId, List<int> studentIds, string markType) // "before" or "after"
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return HttpNotFound();

            int userId = GetCurrentUserId();
            foreach (var sid in studentIds)
            {
                var ts = db.TripStudents.FirstOrDefault(t => t.TripScheduleId == scheduleId && t.StudentId == sid);
                if (ts != null)
                {
                    if (markType == "before")
                    {
                        ts.IsPresentBefore = true;
                        ts.MarkedBeforeBy = userId.ToString();
                        ts.MarkedBeforeAt = DateTime.Now;
                    }
                    else
                    {
                        ts.IsPresentAfter = true;
                        ts.MarkedAfterBy = userId.ToString();
                        ts.MarkedAfterAt = DateTime.Now;
                    }
                }
            }
            db.SaveChanges();

            if (markType == "before")
            {
                var studentIdsList = GetStudentIds(scheduleId);
                NotificationHelper.NotifyParents(db, studentIdsList, $"Your child has been marked present for departure of '{schedule.TripRequest.Title}'.");
                NotificationHelper.NotifyTeacher(db, schedule.TeacherId, "Pre‑trip manifest completed.");
            }
            else
            {
                var studentIdsList = GetStudentIds(scheduleId);
                NotificationHelper.NotifyParents(db, studentIdsList, $"Your child has returned safely from '{schedule.TripRequest.Title}'.");
                NotificationHelper.NotifyTeacher(db, schedule.TeacherId, "Post‑trip manifest completed.");
                schedule.Status = "Completed";
                db.SaveChanges();
            }
            return RedirectToAction("UpcomingTrips");
        }

        public ActionResult GetFilteredStudents(int tripRequestId, int? gradeFilter = null, int? subjectFilter = null)
        {
            var request = db.TripRequests.Find(tripRequestId);
            if (request == null)
                return Content("<div class='alert alert-danger'>Trip not found</div>");

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




        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}