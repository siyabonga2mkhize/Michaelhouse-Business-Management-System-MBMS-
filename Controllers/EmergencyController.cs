using Michaelhouse.Helpers;
using Michaelhouse.Models;
using Michaelhouse.Models.ViewModels;
using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class EmergencyController : Controller
    {
        private DBContextClass db = new DBContextClass();
        private EmergencyService _emergencyService;

        public EmergencyController()
        {
            _emergencyService = new EmergencyService(db);
        }

        // ============= PARAMETERLESS: FIND LATEST ACTIVE ALERT =============
        // Renamed to avoid conflict with enum AlertStatus
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> LatestAlertStatus()
        {
            var activeAlert = await db.EmergencyAlerts
                .Where(a => a.Status == AlertStatus.Active)   // fully qualified if needed
                .OrderByDescending(a => a.AlertTime)
                .FirstOrDefaultAsync();

            if (activeAlert != null)
                return RedirectToAction("ViewAlertStatus", new { id = activeAlert.AlertId });
            else
            {
                ViewBag.Message = "No active emergency alert at this time.";
                return View("NoActiveAlert");
            }
        }

        // ============= STAFF: TRIGGER EMERGENCY =============
        [HttpGet]
        [AllowAnonymous]
        public ActionResult TriggerAlert()
        {
            var model = new EmergencyAlert
            {
                AlertTime = DateTime.Now,
                AssemblyLatitude = -29.8587,
                AssemblyLongitude = 30.8762,
                AssemblyPointName = "Main Assembly Point",
                GeofenceRadiusMeters = 150,
                Status = AlertStatus.Active
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<ActionResult> TriggerAlert(EmergencyAlert alert)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(alert);

                var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name);
                if (currentUser == null)
                {
                    ModelState.AddModelError("", "User not found");
                    return View(alert);
                }

                alert.InitiatedByStaffId = currentUser.UserId;
                alert.AlertTime = DateTime.Now;
                alert.CreatedDate = DateTime.Now;
                alert.Status = AlertStatus.Active;

                db.EmergencyAlerts.Add(alert);
                await db.SaveChangesAsync();

                // Get boarding students – using IsBoarding and IsActive from Student model
                var students = db.Students
                    .Where(s => s.IsActive && s.IsBoarding)
                    .ToList();

                // Create pending confirmations
                foreach (var student in students)
                {
                    var confirmation = new StudentSafetyConfirmation
                    {
                        AlertId = alert.AlertId,
                        StudentId = student.StudentId,
                        Status = SafetyStatus.Pending,
                        ConfirmationTime = DateTime.Now,
                        StudentLatitude = 0,
                        StudentLongitude = 0,
                        WithinGeofence = false
                    };
                    db.StudentSafetyConfirmations.Add(confirmation);
                }
                await db.SaveChangesAsync();

                // Send notifications – student.UserId is int, not nullable
                foreach (var student in students)
                {
                    if (student.UserId > 0)
                    {
                        try
                        {
                            NotificationHelper.Send(
                                db,
                                student.UserId.Value,
                                $"🚨 EMERGENCY ALERT: {alert.AlertMessage}",
                                "EmergencyAlert",
                                alert.AlertId
                            );
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error notifying student {student.StudentId}: {ex.Message}");
                        }
                    }
                }

                TempData["SuccessMessage"] = $"✓ Emergency alert triggered successfully. {students.Count} students notified.";
                return RedirectToAction("ViewAlertStatus", new { id = alert.AlertId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return View(alert);
            }
        }

        // ============= VIEW: REAL-TIME DASHBOARD =============
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> ViewAlertStatus(int id)   // non-nullable int
        {
            var alert = await db.EmergencyAlerts
                .FirstOrDefaultAsync(a => a.AlertId == id);
            if (alert == null)
                return HttpNotFound();

            // Get boarding students – using IsBoarding and IsActive
            var boardingStudents = await db.Students
                .Where(s => s.IsActive && s.IsBoarding)
                .ToListAsync();

            var confirmations = await db.StudentSafetyConfirmations
                .Include(c => c.Student)
                .Where(c => c.AlertId == id)
                .ToListAsync();

            var confirmationLookup = confirmations
                .ToDictionary(c => c.StudentId);

            var viewModel = new AlertAccountabilityViewModel
            {
                Alert = alert,
                TotalStudents = boardingStudents.Count,
                SafeCount = confirmations.Count(c => c.Status == SafetyStatus.Confirmed),
                OutsideCount = confirmations.Count(c => c.Status == SafetyStatus.OutsideZone),
                PendingCount = boardingStudents.Count - confirmations.Count,
                Confirmations = new List<StudentSafetyConfirmationViewModel>()
            };

            foreach (var student in boardingStudents)
            {
                if (confirmationLookup.TryGetValue(student.StudentId, out var confirmation))
                {
                    viewModel.Confirmations.Add(new StudentSafetyConfirmationViewModel
                    {
                        StudentId = student.StudentId,
                        StudentName = $"{student.FirstName} {student.LastName}",
                        Status = confirmation.Status,
                        DistanceFromAssemblyPointMeters = confirmation.DistanceFromAssemblyPointMeters,
                        ConfirmationTime = confirmation.ConfirmationTime,
                        WithinGeofence = confirmation.WithinGeofence,
                        StudentLatitude = confirmation.StudentLatitude,   // add this
                        StudentLongitude = confirmation.StudentLongitude  // add this
                    });
                }
                else
                {
                    viewModel.Confirmations.Add(new StudentSafetyConfirmationViewModel
                    {
                        StudentId = student.StudentId,
                        StudentName = $"{student.FirstName} {student.LastName}",
                        Status = SafetyStatus.Pending,
                        DistanceFromAssemblyPointMeters = 0,
                        ConfirmationTime = DateTime.MinValue,
                        WithinGeofence = false,
                        StudentLatitude = 0,
                        StudentLongitude = 0
                    });
                }
            }

            return View(viewModel);
        }

        // ============= STUDENT: CONFIRM SAFETY =============
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmSafety()
        {
            var alert = await db.EmergencyAlerts
                .Where(a => a.Status == AlertStatus.Active)
                .OrderByDescending(a => a.AlertTime)
                .FirstOrDefaultAsync();

            if (alert == null)
                return View("NoActiveAlert");

            return View(alert);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmSafe(ConfirmSafeRequest request)
        {
            try
            {
                var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name);
                if (currentUser == null)
                    return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);

                var student = db.Students.FirstOrDefault(s => s.UserId == currentUser.UserId);
                if (student == null)
                    return Json(new { success = false, message = "Student record not found" }, JsonRequestBehavior.AllowGet);

                var alert = db.EmergencyAlerts
                    .Where(a => a.Status == AlertStatus.Active)
                    .OrderByDescending(a => a.AlertTime)
                    .FirstOrDefault();

                if (alert == null)
                    return Json(new { success = false, message = "No active emergency alert" }, JsonRequestBehavior.AllowGet);

                // Get or create confirmation
                var confirmation = db.StudentSafetyConfirmations
                    .FirstOrDefault(c => c.AlertId == alert.AlertId && c.StudentId == student.StudentId);

                if (confirmation == null)
                {
                    confirmation = new StudentSafetyConfirmation
                    {
                        AlertId = alert.AlertId,
                        StudentId = student.StudentId
                    };
                    db.StudentSafetyConfirmations.Add(confirmation);
                }

                // Geofencing check
                var studentLocation = new GeofencingService.Location(request.Latitude, request.Longitude);
                var assemblyLocation = new GeofencingService.Location(alert.AssemblyLatitude, alert.AssemblyLongitude);

                bool isWithinGeofence = GeofencingService.IsWithinRadius(
                    assemblyLocation,
                    studentLocation,
                    alert.GeofenceRadiusMeters
                );
                double distance = GeofencingService.CalculateDistance(assemblyLocation, studentLocation);

                // Update confirmation
                confirmation.StudentLatitude = request.Latitude;
                confirmation.StudentLongitude = request.Longitude;
                confirmation.ConfirmationTime = DateTime.Now;
                confirmation.WithinGeofence = isWithinGeofence;
                confirmation.DistanceFromAssemblyPointMeters = distance;
                confirmation.Status = isWithinGeofence ? SafetyStatus.Confirmed : SafetyStatus.OutsideZone;

                await db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    withinGeofence = isWithinGeofence,
                    distance = Math.Round(distance, 2),
                    message = isWithinGeofence
                        ? "✓ Safety confirmed – you are accounted for"
                        : $"⚠ You are {Math.Round(distance, 0)}m outside. Move closer.",
                    confirmationId = confirmation.ConfirmationId
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============= AUTOMATION: TRIGGER SIMULATION =============
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> SimulateEmergency(string message = "Fire in Building A", double radius = 150)
        {
            try
            {
                var currentUser = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name);
                if (currentUser == null)
                    return Content("User not found");

                var alert = new EmergencyAlert
                {
                    InitiatedByStaffId = currentUser.UserId,
                    AlertTime = DateTime.Now,
                    AlertMessage = $"SIMULATED: {message}",
                    AssemblyLatitude = -29.8587,
                    AssemblyLongitude = 30.8762,
                    AssemblyPointName = "Main Assembly Point",
                    GeofenceRadiusMeters = radius,
                    Status = AlertStatus.Active,
                    CreatedDate = DateTime.Now
                };

                db.EmergencyAlerts.Add(alert);
                await db.SaveChangesAsync();

                var students = db.Students.Where(s => s.IsActive && s.IsBoarding).ToList();

                foreach (var student in students)
                {
                    var confirmation = new StudentSafetyConfirmation
                    {
                        AlertId = alert.AlertId,
                        StudentId = student.StudentId,
                        Status = SafetyStatus.Pending,
                        ConfirmationTime = DateTime.Now,
                        StudentLatitude = 0,
                        StudentLongitude = 0,
                        WithinGeofence = false
                    };
                    db.StudentSafetyConfirmations.Add(confirmation);
                }
                await db.SaveChangesAsync();

                return Content($"✓ SIMULATED EMERGENCY TRIGGERED!\n" +
                    $"Alert ID: {alert.AlertId}\n" +
                    $"Message: {alert.AlertMessage}\n" +
                    $"Students notified: {students.Count}\n\n" +
                    $"View dashboard: /Emergency/ViewAlertStatus/{alert.AlertId}");
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ============= RESOLVE ALERT =============
        [HttpPost]
        [Authorize(Roles = "Admin,HouseMaster")]
        public async Task<ActionResult> ResolveAlert(int id, string notes = "")
        {
            try
            {
                var alert = await db.EmergencyAlerts.FindAsync(id);
                if (alert == null)
                    return HttpNotFound();

                alert.Status = AlertStatus.Resolved;
                alert.ResolvedDate = DateTime.Now;
                alert.ResolvedNotes = notes;

                db.Entry(alert).State = EntityState.Modified;
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = "✓ Emergency alert resolved.";
                return RedirectToAction("ViewAlertStatus", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return RedirectToAction("ViewAlertStatus", new { id });
            }
        }

        // ============= AJAX: LIVE UPDATES =============
        [HttpGet]
        [AllowAnonymous]
        public ActionResult GetAlertUpdate(int alertId)
        {
            var confirmations = db.StudentSafetyConfirmations
                .Where(c => c.AlertId == alertId)
                .ToList();

            var safeCount = confirmations.Count(c => c.Status == SafetyStatus.Confirmed);
            var outsideCount = confirmations.Count(c => c.Status == SafetyStatus.OutsideZone);
            var pendingCount = confirmations.Count(c => c.Status == SafetyStatus.Pending);

            return Json(new
            {
                safe = safeCount,
                outside = outsideCount,
                pending = pendingCount,
                total = safeCount + outsideCount + pendingCount,
                refreshTime = DateTime.Now.ToString("HH:mm:ss")
            }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class ConfirmSafeRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}