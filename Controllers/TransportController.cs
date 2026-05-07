using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    //[Authorize(Roles = "Admin,TransportManager")]
    [TransportManagerOnly]
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
            if (role != "TransportManager" && role != "Admin")
                return new HttpUnauthorizedResult();

            // Stats
            ViewBag.PendingRequests = db.TripRequests.Count(r => r.Status == "Pending");
            ViewBag.ApprovedTrips = db.TripRequests.Count(r => r.Status == "Approved");
            ViewBag.UnassignedSchedules = db.TripSchedules.Count(s => s.DriverId == null && s.Status != "Cancelled");
            ViewBag.TotalVehicles = db.Vehicles.Count(v => v.IsActive);
            ViewBag.UpcomingTripsCount = db.TripSchedules.Count(s => s.ScheduledDate >= System.DateTime.Today && s.Status != "Completed");

            // Recent trip requests (last 5)
            var recentRequests = db.TripRequests
                .Include(r => r.Teacher)
                .OrderByDescending(r => r.RequestedAt)
                .Take(5)
                .ToList();

            // Upcoming schedules (next 5)
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
                .Include(s => s.TripRequest)
                .FirstOrDefault(s => s.Id == scheduleId);
            if (schedule == null) return HttpNotFound();

            ViewBag.Drivers = db.Drivers.Where(d => d.IsActive).OrderBy(d => d.FullName).ToList();
            ViewBag.Vehicles = db.Vehicles.Where(v => v.IsActive).OrderBy(v => v.VehicleNumber).ToList();
            ViewBag.Schedule = schedule;
            return View();
        }

        // POST: Save assignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignDriverVehicle(int scheduleId, int driverId, int vehicleId)
        {
            var schedule = db.TripSchedules.Find(scheduleId);
            if (schedule == null) return HttpNotFound();

            schedule.DriverId = driverId;
            schedule.VehicleId = vehicleId;
            schedule.Status = "Confirmed";  // Optional: change status once assigned
            db.SaveChanges();

            // Notify teacher and driver
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

        // All Trips (filterable)
        public ActionResult AllTrips(string status = null)
        {
            var trips = db.TripSchedules
                .Include(s => s.TripRequest)
                .Include(s => s.Teacher)
                .Include(s => s.Driver)
                .Include(s => s.Vehicle)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                trips = trips.Where(s => s.Status == status);

            var list = trips.OrderByDescending(s => s.ScheduledDate).ToList();
            ViewBag.CurrentStatus = status;
            return View(list);
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

        public ActionResult Drivers()
        {
            var drivers = db.Drivers.Where(d => d.IsActive).ToList();
            return View(drivers);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}