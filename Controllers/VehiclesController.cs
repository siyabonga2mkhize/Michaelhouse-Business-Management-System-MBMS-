using Michaelhouse.Filters;
using Michaelhouse.Models;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    [TransportManagerOnly]   // or [AdminOnly] if you want both to have access
    public class VehicleController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // GET: Vehicles (list all active vehicles)
        public ActionResult Index()
        {
            var vehicles = db.Vehicles.OrderBy(v => v.VehicleNumber).ToList();
            return View(vehicles);
        }

        // GET: Vehicles/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Vehicles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Vehicle vehicle)
        {
            if (ModelState.IsValid)
            {
                vehicle.DateAdded = System.DateTime.Now;
                vehicle.IsActive = true;
                db.Vehicles.Add(vehicle);
                db.SaveChanges();
                TempData["Success"] = "Vehicle added successfully.";
                return RedirectToAction("Index");
            }
            return View(vehicle);
        }

        // GET: Vehicles/Edit/5
        public ActionResult Edit(int id)
        {
            var vehicle = db.Vehicles.Find(id);
            if (vehicle == null) return HttpNotFound();
            return View(vehicle);
        }

        // POST: Vehicles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Vehicle vehicle)
        {
            if (ModelState.IsValid)
            {
                db.Entry(vehicle).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = "Vehicle updated.";
                return RedirectToAction("Index");
            }
            return View(vehicle);
        }

        // GET: Vehicles/Delete/5
        public ActionResult Delete(int id)
        {
            var vehicle = db.Vehicles.Find(id);
            if (vehicle == null) return HttpNotFound();
            return View(vehicle);
        }

        // POST: Vehicles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var vehicle = db.Vehicles.Find(id);
            db.Vehicles.Remove(vehicle);
            db.SaveChanges();
            TempData["Success"] = "Vehicle removed.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}