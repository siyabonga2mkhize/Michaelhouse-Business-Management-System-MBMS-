using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Michaelhouse.Models;

namespace Michaelhouse
{
    public class DriverAvailabilitiesController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // GET: DriverAvailabilities
        public ActionResult Index()
        {
            return View(db.DriverAvailabilities.ToList());
        }

        // GET: DriverAvailabilities/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DriverAvailability driverAvailability = db.DriverAvailabilities.Find(id);
            if (driverAvailability == null)
            {
                return HttpNotFound();
            }
            return View(driverAvailability);
        }

        // GET: DriverAvailabilities/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: DriverAvailabilities/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,DriverId,StartDate,EndDate,Reason,DateCreated")] DriverAvailability driverAvailability)
        {
            // 1. Validate date logic first
            if (driverAvailability.EndDate < driverAvailability.StartDate)
            {
                ModelState.AddModelError("", "End Date cannot be before Start Date");
            }

            // 2. Only check overlap if dates are valid
            if (ModelState.IsValid)
            {
                var overlap = db.DriverAvailabilities.Any(a =>
                    a.DriverId == driverAvailability.DriverId &&
                    driverAvailability.StartDate <= a.EndDate &&
                    driverAvailability.EndDate >= a.StartDate
                );

                if (overlap)
                {
                    ModelState.AddModelError("", "This availability period overlaps with an existing one.");
                }
            }

            // 3. Final save
            if (ModelState.IsValid)
            {
                int loggedInDriverId = Convert.ToInt32(Session["DriverId"]);

                driverAvailability.DateCreated = DateTime.Now;

                db.DriverAvailabilities.Add(driverAvailability);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(driverAvailability);
        }

        // GET: DriverAvailabilities/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DriverAvailability driverAvailability = db.DriverAvailabilities.Find(id);
            if (driverAvailability == null)
            {
                return HttpNotFound();
            }
            return View(driverAvailability);
        }

        // POST: DriverAvailabilities/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,DriverId,StartDate,EndDate,Reason,DateCreated")] DriverAvailability driverAvailability)
        {
            if (ModelState.IsValid)
            {
                db.Entry(driverAvailability).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(driverAvailability);
        }

        // GET: DriverAvailabilities/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            DriverAvailability driverAvailability = db.DriverAvailabilities.Find(id);
            if (driverAvailability == null)
            {
                return HttpNotFound();
            }
            return View(driverAvailability);
        }

        // POST: DriverAvailabilities/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            DriverAvailability driverAvailability = db.DriverAvailabilities.Find(id);
            db.DriverAvailabilities.Remove(driverAvailability);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
