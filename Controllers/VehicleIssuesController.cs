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
    public class VehicleIssuesController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // GET: VehicleIssues
        public ActionResult Index()
        {
            return View(db.VehicleIssues.ToList());
        }

        // GET: VehicleIssues/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            VehicleIssue vehicleIssue = db.VehicleIssues.Find(id);
            if (vehicleIssue == null)
            {
                return HttpNotFound();
            }
            return View(vehicleIssue);
        }

        // GET: VehicleIssues/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: VehicleIssues/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,DriverId,VehicleId,Description,DateReported,Status")] VehicleIssue vehicleIssue)
        {
            if (ModelState.IsValid)
            {
                db.VehicleIssues.Add(vehicleIssue);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(vehicleIssue);
        }

        // GET: VehicleIssues/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            VehicleIssue vehicleIssue = db.VehicleIssues.Find(id);
            if (vehicleIssue == null)
            {
                return HttpNotFound();
            }
            return View(vehicleIssue);
        }

        // POST: VehicleIssues/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,DriverId,VehicleId,Description,DateReported,Status")] VehicleIssue vehicleIssue)
        {
            if (ModelState.IsValid)
            {
                db.Entry(vehicleIssue).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(vehicleIssue);
        }

        // GET: VehicleIssues/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            VehicleIssue vehicleIssue = db.VehicleIssues.Find(id);
            if (vehicleIssue == null)
            {
                return HttpNotFound();
            }
            return View(vehicleIssue);
        }

        // POST: VehicleIssues/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            VehicleIssue vehicleIssue = db.VehicleIssues.Find(id);
            db.VehicleIssues.Remove(vehicleIssue);
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
