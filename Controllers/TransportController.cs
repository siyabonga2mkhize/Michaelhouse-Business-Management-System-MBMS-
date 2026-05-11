using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    //[Authorize(Roles = "Admin,TransportManager")
    public class TransportController : Controller
    {
        private DBContextClass db = new DBContextClass();

        private int GetCurrentUserId()
        {
            return (int)(Session["UserId"] ?? 0);
        }

        // Dashboard with statistics
        public ActionResult Dashboard()
        {
            var role = Session["UserRole"]?.ToString();

            // FIX: handle BOTH naming styles safely
            bool isTransportManager =
                role == "TransportManager" ||
                role == "Transport Manager";

            if (!isTransportManager && role != "Admin")
                return new HttpUnauthorizedResult();

            // Stats
            ViewBag.PendingRequests = db.TripRequests.Count(r => r.Status == "Pending");
            ViewBag.ApprovedTrips = db.TripRequests.Count(r => r.Status == "Approved");
            ViewBag.UnassignedSchedules = db.TripSchedules.Count(s => s.DriverId == null && s.Status != "Cancelled");
            ViewBag.TotalVehicles = db.Vehicles.Count(v => v.IsActive);
            ViewBag.UpcomingTripsCount = db.TripSchedules.Count(s => s.ScheduledDate >= System.DateTime.Today && s.Status != "Completed");

            var recentRequests = db.TripRequests
                .Include(r => r.Teacher)
                .OrderByDescending(r => r.RequestedAt)
                .Take(5)
                .ToList();

            var upcomingSchedules = db.TripSchedules
                .Include(s => s.TripRequest)
                .Include(s => s.Teacher)
                .Where(s => s.ScheduledDate >= System.DateTime.Today && s.Status != "Completed")
                .OrderBy(s => s.ScheduledDate)
                .Take(5)
                .ToList();

            ViewBag.RecentRequests = recentRequests;
            ViewBag.UpcomingSchedules = upcomingSchedules;

            return View();
        }

        // Pending Requests (full list)
        public ActionResult PendingRequests()
        {
            var pending = db.TripRequests
                .Include(r => r.Teacher)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.RequestedAt)
                .ToList();
            return View(pending);
        }

        [HttpPost]
        public ActionResult ApproveRequest(int id, string rejectionReason = null)
        {
            var req = db.TripRequests.Find(id);
            if (req == null) return HttpNotFound();

            if (!string.IsNullOrEmpty(rejectionReason))
            {
                req.Status = "Rejected";
                req.RejectionReason = rejectionReason;
                NotificationHelper.NotifyTeacher(db, req.TeacherId, $"Trip '{req.Title}' rejected. Reason: {rejectionReason}");
            }
            else
            {
                req.Status = "Approved";
                req.ApprovedByAdminId = GetCurrentUserId();
                req.ApprovedAt = System.DateTime.Now;
                NotificationHelper.NotifyTeacher(db, req.TeacherId, $"Trip '{req.Title}' approved. You can now schedule students.");
            }
            db.SaveChanges();
            return RedirectToAction("PendingRequests");
        }

        // GET: Unassigned schedules (no driver or vehicle)
        public ActionResult UnassignedSchedules()
        {
            var schedules = db.TripSchedules
                .Include(s => s.TripRequest)
                .Include(s => s.TripStudents)
                .Where(s => (s.DriverId == null || s.VehicleId == null) && s.Status != "Cancelled")
                .OrderBy(s => s.ScheduledDate)
                .ToList();
            return View(schedules);
        }

        // GET: Form to assign driver & vehicle to a schedule
        public ActionResult AssignDriverVehicle(int scheduleId)
        {
            var schedule = db.TripSchedules
                .Include("TripRequest")
                .Include("VehicleAssignments.Driver")
                .Include("VehicleAssignments.Vehicle")
                .FirstOrDefault(s => s.Id == scheduleId);
            if (schedule == null) return HttpNotFound();

            int studentCount = db.TripStudents.Count(ts => ts.TripScheduleId == scheduleId);
            int totalAllocated = schedule.VehicleAssignments?.Sum(a => a.AllocatedSeats) ?? 0;

            // Get IDs of drivers and vehicles already assigned to this schedule
            var assignedDriverIds = schedule.VehicleAssignments?.Select(a => a.DriverId).ToArray() ?? new int[0];
            var assignedVehicleIds = schedule.VehicleAssignments?.Select(a => a.VehicleId).ToArray() ?? new int[0];

            ViewBag.Drivers = db.Drivers
                .Where(d => d.IsActive && !assignedDriverIds.Contains(d.Id))
                .OrderBy(d => d.FullName)
                .ToList();

            ViewBag.Vehicles = db.Vehicles
                .Where(v => v.IsActive && !assignedVehicleIds.Contains(v.Id))
                .OrderBy(v => v.VehicleNumber)
                .ToList();

            ViewBag.StudentCount = studentCount;
            ViewBag.TotalAllocated = totalAllocated;

            return View(schedule);
        }

        // POST: Save assignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignDriverVehicle(int scheduleId, int driverId, int vehicleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return HttpNotFound();

            // Count students already scheduled
            int studentCount = db.TripStudents.Count(ts => ts.TripScheduleId == scheduleId);
            var vehicle = db.Vehicles.Find(vehicleId);
            if (vehicle == null) return HttpNotFound();

            if (vehicle.Capacity < studentCount)
            {
                TempData["Error"] = $"Vehicle capacity ({vehicle.Capacity}) is insufficient for {studentCount} students. Please add another vehicle or choose a larger vehicle.";
                return RedirectToAction("AssignDriverVehicle", new { scheduleId });
            }

            schedule.DriverId = driverId;
            schedule.VehicleId = vehicleId;
            schedule.Status = "Confirmed";
            db.SaveChanges();

            var tripReq = db.TripRequests.Find(schedule.TripRequestId);
            NotificationHelper.NotifyTeacher(db, schedule.TeacherId, $"Trip '{tripReq.Title}' has been assigned a driver and vehicle.");
            var driver = db.Drivers.Find(driverId);
            if (driver?.UserId != null)
                NotificationHelper.Send(db, driver.UserId.Value, $"You have been assigned to drive trip '{tripReq.Title}' on {schedule.ScheduledDate:d}.", "Trip", scheduleId);

            TempData["Success"] = "Driver and vehicle assigned successfully.";
            return RedirectToAction("UnassignedSchedules");
        }
        [HttpPost]
        public ActionResult Assign(int scheduleId, int driverId, int vehicleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            schedule.DriverId = driverId;
            schedule.VehicleId = vehicleId;
            schedule.Status = "Confirmed";
            db.SaveChanges();

            var req = db.TripRequests.Find(schedule.TripRequestId);
            NotificationHelper.NotifyTeacher(db, schedule.TeacherId, $"Driver & vehicle assigned for '{req.Title}'.");
            NotificationHelper.NotifyDriver(db, driverId, $"You are assigned to trip '{req.Title}' on {schedule.ScheduledDate:d}.");
            return RedirectToAction("UnassignedSchedules");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddVehicleAssignment(int scheduleId, int driverId, int vehicleId, int allocatedSeats)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return HttpNotFound();

            var vehicle = db.Vehicles.Find(vehicleId);
            if (vehicle == null) return HttpNotFound();

            if (allocatedSeats > vehicle.Capacity)
            {
                TempData["Error"] = $"Allocated seats ({allocatedSeats}) cannot exceed vehicle capacity ({vehicle.Capacity}).";
                return RedirectToAction("AssignDriverVehicle", new { scheduleId });
            }

            int studentCount = db.TripStudents.Count(ts => ts.TripScheduleId == scheduleId);
            int currentTotal = db.TripVehicleAssignments
                .Where(a => a.TripScheduleId == scheduleId)
                .Sum(a => (int?)a.AllocatedSeats) ?? 0;
            int newTotal = currentTotal + allocatedSeats;

            if (newTotal > studentCount)
            {
                TempData["Error"] = $"Total allocated seats ({newTotal}) would exceed number of students ({studentCount}).";
                return RedirectToAction("AssignDriverVehicle", new { scheduleId });
            }

            var assignment = new TripVehicleAssignment
            {
                TripScheduleId = scheduleId,
                DriverId = driverId,
                VehicleId = vehicleId,
                AllocatedSeats = allocatedSeats,
                CreatedAt = DateTime.Now
            };
            db.TripVehicleAssignments.Add(assignment);
            db.SaveChanges();

            TempData["Success"] = "Vehicle/driver assignment added.";
            return RedirectToAction("AssignDriverVehicle", new { scheduleId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveAssignment(int assignmentId)
        {
            var assignment = db.TripVehicleAssignments.Find(assignmentId);
            if (assignment == null) return HttpNotFound();

            int scheduleId = assignment.TripScheduleId;
            db.TripVehicleAssignments.Remove(assignment);
            db.SaveChanges();

            TempData["Success"] = "Assignment removed.";
            return RedirectToAction("AssignDriverVehicle", new { scheduleId });
        }

        // All Trips (filterable)
        public ActionResult AllTrips(string statusFilter = null)
        {
            var query = db.TripSchedules
                .Include(s => s.TripRequest)
                .Include(s => s.TripRequest.Teacher)
                .Include(s => s.Driver)          // <-- ensure Driver is loaded
                .Include(s => s.Vehicle)         // <-- ensure Vehicle is loaded
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(s => s.Status == statusFilter);

            var schedules = query.OrderByDescending(s => s.ScheduledDate).ToList();
            return View(schedules);
        }

        // Vehicle Management (basic CRUD)
        public ActionResult Vehicles()
        {
            var vehicles = db.Vehicles.OrderBy(v => v.VehicleNumber).ToList();
            return View(vehicles);
        }

        [HttpPost]
        public ActionResult AddVehicle(Vehicle vehicle)
        {
            if (ModelState.IsValid)
            {
                vehicle.DateAdded = System.DateTime.Now;
                db.Vehicles.Add(vehicle);
                db.SaveChanges();
                TempData["Success"] = "Vehicle added successfully.";
            }
            return RedirectToAction("Vehicles");
        }
        [RequireLogin]
        public ActionResult DriverDetails(int id)
        {
            var driver = db.Drivers.Find(id);
            if (driver == null) return Content("Driver not found");
            return PartialView("_DriverDetails", driver);
        }
        [RequireLogin]
        public ActionResult VehicleDetails(int id)
        {
            var vehicle = db.Vehicles.Find(id);
            if (vehicle == null) return Content("Vehicle not found");
            return PartialView("_VehicleDetails", vehicle);
        }

        public ActionResult Drivers()
        {
            var drivers = db.Drivers.Where(d => d.IsActive).ToList();
            return View(drivers);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmTripAssignment(int scheduleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return HttpNotFound();

            int studentCount = db.TripStudents.Count(ts => ts.TripScheduleId == scheduleId);
            int totalAllocated = db.TripVehicleAssignments
                .Where(a => a.TripScheduleId == scheduleId)
                .Sum(a => (int?)a.AllocatedSeats) ?? 0;

            if (totalAllocated < studentCount)
            {
                TempData["Error"] = "Cannot confirm trip: insufficient seats allocated.";
                return RedirectToAction("AssignDriverVehicle", new { scheduleId });
            }

            schedule.Status = "Confirmed";
            db.SaveChanges();

            // Notify the teacher
            var teacherId = schedule.TeacherId;
            var tripReq = db.TripRequests.Find(schedule.TripRequestId);
            NotificationHelper.NotifyTeacher(db, teacherId, $"Trip '{tripReq.Title}' is confirmed and ready for student manifest.");

            TempData["Success"] = "Trip confirmed. The teacher can now mark the manifest.";
            return RedirectToAction("UnassignedSchedules");
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}

