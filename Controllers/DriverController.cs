using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}