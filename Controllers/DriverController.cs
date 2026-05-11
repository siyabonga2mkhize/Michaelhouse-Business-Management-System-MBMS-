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
    public class DriverController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // ─────────────────────────────────────────────────────────────────
        // Dashboard
        // ─────────────────────────────────────────────────────────────────
        public ActionResult Index()
        {
            int userId = (int)Session["UserId"];
            var driver = db.Drivers.FirstOrDefault(d => d.UserId == userId);
            if (driver == null)
            {
                TempData["Error"] = "Driver profile not found.";
                return RedirectToAction("Login", "Account");
            }

            Session["DriverId"] = driver.Id; // ensure session variable is set
            ViewBag.DriverName = driver.FullName;
            return View();
        }

        // ─────────────────────────────────────────────────────────────────
        // My assigned trips (manifest)
        // ─────────────────────────────────────────────────────────────────
        public ActionResult MyTrips()
        {
            int driverId = (int)Session["DriverId"];
            var trips = db.TripSchedules
                .Include(ts => ts.TripRequest)
                .Include(ts => ts.TripStudents)
                .Where(ts => ts.DriverId == driverId && ts.Status != "Completed")
                .OrderBy(ts => ts.ScheduledDate)
                .ToList();
            return View(trips);
        }

        // ─────────────────────────────────────────────────────────────────
        // Availability Management
        // ─────────────────────────────────────────────────────────────────
        public ActionResult Availabilities()
        {
            int driverId = (int)Session["DriverId"];
            var availabilities = db.DriverAvailabilities
                .Where(a => a.DriverId == driverId)
                .OrderBy(a => a.StartDate)
                .ToList();
            return View(availabilities);
        }

        public ActionResult AddAvailability()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddAvailability(DriverAvailability model)
        {

            int driverId = (int)Session["DriverId"];

            // Basic date validation
            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("EndDate", "End Date cannot be before Start Date.");
            }

            if (ModelState.IsValid)
            {
                // Overlap check
                bool overlap = db.DriverAvailabilities.Any(a =>
                    a.DriverId == driverId &&
                    model.StartDate <= a.EndDate &&
                    model.EndDate >= a.StartDate);
                if (overlap)
                {
                    ModelState.AddModelError("", "This period overlaps with an existing unavailability.");
                }
                else
                {
                    model.DriverId = driverId;
                    model.DateCreated = DateTime.Now;
                    db.DriverAvailabilities.Add(model);
                    try
                    {
                        db.SaveChanges();
                        TempData["Success"] = "Availability period added successfully.";
                        return RedirectToAction("Availabilities");
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Database error: {ex.Message}");
                    }
                }
            }
            else
            {
                // Log why ModelState is invalid (e.g., required fields missing)
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var err in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"ModelState error: {err.ErrorMessage}");
                }
                TempData["Error"] = "Please correct the errors in the form.";
            }

            return View(model);
        }

        public ActionResult EditAvailability(int id)
        {
            int driverId = (int)Session["DriverId"];
            var availability = db.DriverAvailabilities.FirstOrDefault(a => a.Id == id && a.DriverId == driverId);
            if (availability == null) return HttpNotFound();
            return View(availability);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditAvailability(DriverAvailability model)
        {
            int driverId = (int)Session["DriverId"];
            var availability = db.DriverAvailabilities.FirstOrDefault(a => a.Id == model.Id && a.DriverId == driverId);
            if (availability == null) return HttpNotFound();

            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("", "End Date cannot be before Start Date.");
            }

            if (ModelState.IsValid)
            {
                bool overlap = db.DriverAvailabilities.Any(a =>
                    a.DriverId == driverId &&
                    a.Id != model.Id &&
                    model.StartDate <= a.EndDate &&
                    model.EndDate >= a.StartDate);
                if (overlap)
                {
                    ModelState.AddModelError("", "This period overlaps with an existing unavailability.");
                }
                else
                {
                    availability.StartDate = model.StartDate;
                    availability.EndDate = model.EndDate;
                    availability.Reason = model.Reason;
                    db.Entry(availability).State = EntityState.Modified;
                    db.SaveChanges();
                    TempData["Success"] = "Availability updated.";
                    return RedirectToAction("Availabilities");
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteAvailability(int id)
        {
            int driverId = (int)Session["DriverId"];
            var availability = db.DriverAvailabilities.FirstOrDefault(a => a.Id == id && a.DriverId == driverId);
            if (availability != null)
            {
                db.DriverAvailabilities.Remove(availability);
                db.SaveChanges();
                TempData["Success"] = "Availability deleted.";
            }
            return RedirectToAction("Availabilities");
        }


        // Trip Manifest — view students for a specific assigned trip
        public ActionResult TripManifest(int? scheduleId)
        {
            if (scheduleId == null)
            {
                TempData["Error"] = "No trip specified.";
                return RedirectToAction("MyTrips");
            }

            int driverId = (int)Session["DriverId"];

            var schedule = db.TripSchedules
                .Include("TripRequest")
                .Include("TripStudents.Student.Parent")
                .FirstOrDefault(s => s.Id == scheduleId.Value && s.DriverId == driverId);
            // ... rest stays the same

            if (schedule == null)
            {
                TempData["Error"] = "Trip not found or you are not assigned to this trip.";
                return RedirectToAction("MyTrips");
            }

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

        // Mark students present/absent — before departure or after return
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkManifest(int? scheduleId, List<ManifestStudentViewModel> students, string markType)
        {
            if (scheduleId == null)
            {
                TempData["Error"] = "Schedule ID missing.";
                return RedirectToAction("MyTrips");
            }

            int driverId = (int)Session["DriverId"];

            var schedule = db.TripSchedules
                .Include(s => s.TripRequest)
                .FirstOrDefault(s => s.Id == scheduleId.Value && s.DriverId == driverId);

            if (schedule == null)
            {
                TempData["Error"] = "Trip not found or access denied.";
                return RedirectToAction("MyTrips");
            }

            // Only allow marking attendance on the scheduled date
            if (System.Data.Entity.DbFunctions.TruncateTime(schedule.ScheduledDate) !=
                System.Data.Entity.DbFunctions.TruncateTime(DateTime.Today))
            {
                TempData["Error"] = "Attendance can only be marked on the scheduled trip date.";
                return RedirectToAction("MyTrips");
            }

            int userId = (int)Session["UserId"];

            foreach (var vm in students)
            {
                var ts = db.TripStudents
                    .FirstOrDefault(t => t.TripScheduleId == scheduleId.Value && t.StudentId == vm.StudentId);
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
                var presentIds = students.Where(s => s.IsPresentBefore).Select(s => s.StudentId).ToList();
                if (presentIds.Any())
                    NotificationHelper.NotifyParents(db, presentIds,
                        $"Your child has been marked PRESENT for departure of trip '{schedule.TripRequest.Title}'.");

                NotificationHelper.NotifyTeacher(db, schedule.TeacherId,
                    $"Driver has saved pre-trip attendance for '{schedule.TripRequest.Title}'.");

                TempData["Success"] = "Pre-trip attendance saved.";
            }
            else if (markType == "after")
            {
                var presentIds = students.Where(s => s.IsPresentAfter).Select(s => s.StudentId).ToList();
                if (presentIds.Any())
                    NotificationHelper.NotifyParents(db, presentIds,
                        $"Your child has returned safely from trip '{schedule.TripRequest.Title}'.");

                NotificationHelper.NotifyTeacher(db, schedule.TeacherId,
                    $"Driver has saved post-trip attendance. Trip '{schedule.TripRequest.Title}' completed.");

                schedule.Status = "Completed";
                db.SaveChanges();

                TempData["Success"] = "Post-trip attendance saved. Trip marked as Completed.";
            }
            else
            {
                TempData["Error"] = "Invalid mark type.";
            }

            return RedirectToAction("MyTrips");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}