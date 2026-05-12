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
            var issues = db.VehicleIssues
                .Include("Driver")
                .Include("Vehicle")
                .ToList();

            return View(issues);
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
            var vehicles = db.Vehicles
                .Where(v => v.IsActive)
                .ToList()
                .Select(v => new
                {
                    v.Id,
                    DisplayName = v.Model + " - " + v.VehicleNumber
                });

            ViewBag.VehicleId = new SelectList(vehicles, "Id", "DisplayName");

            return View();
        }

        // POST: VehicleIssues/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,DriverId,VehicleId,Description,DateReported,Status")] VehicleIssue vehicleIssue)
        {
            vehicleIssue.DriverId = (int)Session["DriverId"];
            
            vehicleIssue.DateReported = DateTime.Now;
            vehicleIssue.Status = "Pending";
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status)
        {
            var issue = db.VehicleIssues.Find(id);

            if (issue == null)
            {
                return HttpNotFound();
            }

            issue.Status = status;
            db.SaveChanges();

            return RedirectToAction("Details", new { id = id });
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
