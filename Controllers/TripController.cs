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
            return View("BookTrip" ,model);
        }

        [HttpPost]
        public ActionResult CreateTripRequest(TripRequestCreateViewModel model, int? rebookId = null)
        {
            System.Diagnostics.Debug.WriteLine("=== CreateTripRequest POST called ===");
            System.Diagnostics.Debug.WriteLine($"Title: {model.Title}, Destination: {model.Destination}");
            System.Diagnostics.Debug.WriteLine($"Departure: {model.DepartureTime}, Return: {model.ReturnTime}");
            System.Diagnostics.Debug.WriteLine($"MaxStudents: {model.MaxStudents}");
            if (model.DepartureTime <= DateTime.MinValue || model.ReturnTime <= DateTime.MinValue ||
                model.DepartureTime.Year < 2000 || model.ReturnTime.Year < 2000)
            {
                ModelState.AddModelError("", "Please select valid departure and return times.");
                if (rebookId.HasValue) ViewBag.RebookId = rebookId;
                return View("BookTrip", model);
            }

            if (model.DepartureTime <= DateTime.MinValue || model.ReturnTime <= DateTime.MinValue)
            {
                ModelState.AddModelError("", "Please select both departure and return times.");
                if (rebookId.HasValue) ViewBag.RebookId = rebookId;
                return View("BookTrip", model);
            }

            if (model.DepartureTime < DateTime.Today)
            {
                ModelState.AddModelError("DepartureTime", "Departure time cannot be in the past.");
                if (rebookId.HasValue) ViewBag.RebookId = rebookId;
                return View("BookTrip", model);
            }

            if (ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var err in errors)
                    System.Diagnostics.Debug.WriteLine(err.ErrorMessage);
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

            // If we got this far, something failed, redisplay the form
            if (rebookId.HasValue) ViewBag.RebookId = rebookId;
            return View("BookTrip", model);
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
                return HttpNotFound();

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
                return HttpNotFound();
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
                .Include("TripStudents.Student.Parent")   // string path works in EF6
                .FirstOrDefault(s => s.Id == scheduleId);
            if (schedule == null) return HttpNotFound();

            int currentTeacherId = GetCurrentTeacherId();
            int? currentDriverId = (int?)Session["DriverId"];

            // Security: only the assigned teacher or driver can view the manifest
            if (schedule.TeacherId != currentTeacherId && (currentDriverId == null || schedule.DriverId != currentDriverId))
                return new HttpUnauthorizedResult();

            var students = schedule.TripStudents.Select(ts => new ManifestStudentViewModel
            {
                StudentId = ts.StudentId,
                StudentName = ts.Student.FirstName + " " + ts.Student.LastName,
                ParentName = ts.Student.Parent?.Name ?? "N/A",
                EmergencyContactName = ts.Student.Parent?.EmergencyContactName ?? "Not provided",
                EmergencyContactPhone = ts.Student.Parent?.EmergencyContactPhone ?? "",
                AlreadyPresentBefore = ts.IsPresentBefore,
                AlreadyPresentAfter = ts.IsPresentAfter,
                IsPresentBefore = ts.IsPresentBefore ?? false,
                IsPresentAfter = ts.IsPresentAfter ?? false
            }).ToList();

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
            using (var db = new DBContextClass())
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




        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}