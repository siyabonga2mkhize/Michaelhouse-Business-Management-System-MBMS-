using Michaelhouse.Filters;
using Michaelhouse.Models;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [RequireLogin]
    [TransportManagerOnly]  // or [AdminOnly] – adjust to your roles
    public class DriverController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // GET: Drivers
        public ActionResult Index()
        {
            var drivers = db.Drivers.OrderBy(d => d.FullName).ToList();
            return View(drivers);
        }

        // GET: Drivers/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Drivers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Driver driver)
        {
            if (ModelState.IsValid)
            {
                driver.DateCreated = System.DateTime.Now;
                driver.IsActive = true;
                // You may want to create a linked AppUser account for the driver
                if (string.IsNullOrEmpty(driver.PasswordHash))
                {
                    driver.PasswordHash = AccountController.HashPassword("Driver@123"); // default password
                }
                db.Drivers.Add(driver);
                db.SaveChanges();
                TempData["Success"] = "Driver added successfully.";
                return RedirectToAction("Index");
            }
            return View(driver);
        }

        // GET: Drivers/Edit/5
        public ActionResult Edit(int id)
        {
            var driver = db.Drivers.Find(id);
            if (driver == null) return HttpNotFound();
            return View(driver);
        }

        // POST: Drivers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Driver driver)
        {
            if (ModelState.IsValid)
            {
                db.Entry(driver).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = "Driver updated.";
                return RedirectToAction("Index");
            }
            return View(driver);
        }

        // GET: Drivers/Delete/5
        public ActionResult Delete(int id)
        {
            var driver = db.Drivers.Find(id);
            if (driver == null) return HttpNotFound();
            return View(driver);
        }

        // POST: Drivers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var driver = db.Drivers.Find(id);
            db.Drivers.Remove(driver);
            db.SaveChanges();
            TempData["Success"] = "Driver removed.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}