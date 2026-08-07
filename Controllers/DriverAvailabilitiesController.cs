using Michaelhouse.Controllers;
using Michaelhouse.Infrastructure;
﻿using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Michaelhouse.Models;

namespace Michaelhouse
{
    public class DriverAvailabilitiesController : BaseController
    {
        private DBContextClass db = DbContextFactory.Create();

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
                return BadRequest();
            }
            DriverAvailability driverAvailability = db.DriverAvailabilities.Find(id);
            if (driverAvailability == null)
            {
                return NotFound();
            }
            return View(driverAvailability);
        }

        // GET: DriverAvailabilities/Create
        public ActionResult Create()
        {
            // Resolve logged-in driver's profile so the view doesn't access the DB
            try
            {
                var uidObj = Session["UserId"];
                if (uidObj != null)
                {
                    var uid = (int)uidObj;
                    var driver = db.Drivers.FirstOrDefault(d => d.UserId == uid);
                    if (driver != null)
                    {
                        ViewBag.DriverName = driver.FullName;
                        ViewBag.DriverId = driver.Id;
                    }
                }
            }
            catch { }

            return View();
        }

        // POST: DriverAvailabilities/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind("Id,DriverId,StartDate,EndDate,Reason,DateCreated")] DriverAvailability driverAvailability)
        {
            // Ensure DriverId is taken from the logged-in driver (do not trust client input)
            int resolvedDriverId = 0;
            try
            {
                if (Session["DriverId"] != null)
                    resolvedDriverId = Convert.ToInt32(Session["DriverId"]);
                else if (Session["UserId"] != null)
                {
                    var uid = Convert.ToInt32(Session["UserId"]);
                    var drv = db.Drivers.FirstOrDefault(d => d.UserId == uid);
                    if (drv != null) resolvedDriverId = drv.Id;
                }
            }
            catch { }

            if (resolvedDriverId == 0)
            {
                ModelState.AddModelError("", "Could not determine driver identity. Please contact administrator.");
            }

            // set the resolved driver id regardless of what was posted
            driverAvailability.DriverId = resolvedDriverId;

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
                return BadRequest();
            }
            DriverAvailability driverAvailability = db.DriverAvailabilities.Find(id);
            if (driverAvailability == null)
            {
                return NotFound();
            }
            return View(driverAvailability);
        }

        // POST: DriverAvailabilities/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind("Id,DriverId,StartDate,EndDate,Reason,DateCreated")] DriverAvailability driverAvailability)
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
                return BadRequest();
            }
            DriverAvailability driverAvailability = db.DriverAvailabilities.Find(id);
            if (driverAvailability == null)
            {
                return NotFound();
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
