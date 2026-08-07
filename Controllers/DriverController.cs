using Microsoft.AspNetCore.Http.Extensions;
using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    public class DriverController : BaseController
    {
        private DBContextClass db = DbContextFactory.Create();

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

            var directTrips = db.TripSchedules
                .Include(ts => ts.TripRequest)
                .Include(ts => ts.TripStudents)
                .Where(ts => ts.DriverId == driverId && ts.Status != "Completed");

            var indirectTrips = db.TripVehicleAssignments
                .Include(tva => tva.TripSchedule)
                .Include(tva => tva.TripSchedule.TripRequest)
                .Include(tva => tva.TripSchedule.TripStudents)
                .Where(tva => tva.DriverId == driverId && tva.TripSchedule.Status != "Completed")
                .Select(tva => tva.TripSchedule)
                .Distinct();

            var allTrips = directTrips.Union(indirectTrips).OrderBy(t => t.ScheduledDate).ToList();

            return View(allTrips);
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
            if (availability == null) return NotFound();
            return View(availability);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditAvailability(DriverAvailability model)
        {
            int driverId = (int)Session["DriverId"];
            var availability = db.DriverAvailabilities.FirstOrDefault(a => a.Id == model.Id && a.DriverId == driverId);
            if (availability == null) return NotFound();

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

            // Try direct assignment first
            var schedule = db.TripSchedules
                .Include("TripRequest")
                .Include("TripStudents.Student.Parent")
                .Include("Vehicle")
                .FirstOrDefault(s => s.Id == scheduleId.Value && s.DriverId == driverId);

            bool isIndirect = false;
            if (schedule == null)
            {
                isIndirect = db.TripVehicleAssignments
                    .Any(tva => tva.TripScheduleId == scheduleId.Value && tva.DriverId == driverId);
                if (isIndirect)
                {
                    schedule = db.TripSchedules
                        .Include("TripRequest")
                        .Include("TripStudents.Student.Parent")
                        .Include("Vehicle")
                        .FirstOrDefault(s => s.Id == scheduleId.Value);
                }
            }

            if (schedule == null)
            {
                TempData["Error"] = "Trip not found or you are not assigned to this trip.";
                return RedirectToAction("MyTrips");
            }

            // Determine vehicle display
            string vehicleDisplay = "—";
            if (schedule.Vehicle != null)
                vehicleDisplay = schedule.Vehicle.VehicleNumber;
            else if (isIndirect)
            {
                var assignedVehicles = db.TripVehicleAssignments
                    .Where(tva => tva.TripScheduleId == scheduleId.Value && tva.DriverId == driverId)
                    .Select(tva => tva.Vehicle)
                    .ToList();
                if (assignedVehicles.Any())
                    vehicleDisplay = string.Join(", ", assignedVehicles.Select(v => v.VehicleNumber));
            }

            // --- Generate QR codes for each student (same as teacher's Manifest) ---
            var students = new List<ManifestStudentViewModel>();
            foreach (var tripStudent in schedule.TripStudents)
            {
                int studentId = tripStudent.StudentId;
                var student = tripStudent.Student;

                // Departure token (Before)
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

                // Return token (After)
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
            ViewBag.VehicleDisplay = vehicleDisplay;
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
            if (schedule.ScheduledDate.Date !=
                DateTime.Today.Date)
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

        // Add this helper inside DriverController (copy from TripController)
        private string GenerateQRCodeDataUrl(string url)
        {
            // Use the same Google Charts fallback as StudentQRCodeService
            var encoded = Uri.EscapeDataString(url ?? string.Empty);
            var chartUrl = $"https://chart.googleapis.com/chart?cht=qr&chs=300x300&chl={encoded}&chld=Q|1";

            using (var wc = new System.Net.WebClient())
            {
                wc.Headers.Add("User-Agent", "MichaelhouseQRCodeGenerator/1.0");
                var bytes = wc.DownloadData(chartUrl);
                return "data:image/png;base64," + Convert.ToBase64String(bytes);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}