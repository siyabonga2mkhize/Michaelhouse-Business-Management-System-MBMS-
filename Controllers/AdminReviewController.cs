using Microsoft.AspNetCore.Mvc.Rendering;
using Michaelhouse.Infrastructure;
﻿using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Michaelhouse.Models;

namespace Michaelhouse.Controllers
{
    public class AdminReviewController : BaseController
    {
        private DBContextClass db = DbContextFactory.Create();

        // GET: AdminReview
        public ActionResult Dashboard()
        {
            return View();
        }
        public ActionResult Index()
        {
            var adminReviews = db.AdminReviews.Include(a => a.Application);
            return View(adminReviews.ToList());
        }

        // GET: AdminReview/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            AdminReview adminReview = db.AdminReviews.Find(id);
            if (adminReview == null)
            {
                return NotFound();
            }
            return View(adminReview);
        }

        // GET: AdminReview/Create
        public ActionResult Create()
        {
            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary");
            return View();
        }

        // POST: AdminReview/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind("ReviewId,AppId,AdminId,Date,Decision,AdminNotes,AgreedWithAi")] AdminReview adminReview)
        {
            if (ModelState.IsValid)
            {
                db.AdminReviews.Add(adminReview);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary", adminReview.AppId);
            return View(adminReview);
        }

        // GET: AdminReview/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            AdminReview adminReview = db.AdminReviews.Find(id);
            if (adminReview == null)
            {
                return NotFound();
            }
            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary", adminReview.AppId);
            return View(adminReview);
        }

        // POST: AdminReview/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind("ReviewId,AppId,AdminId,Date,Decision,AdminNotes,AgreedWithAi")] AdminReview adminReview)
        {
            if (ModelState.IsValid)
            {
                db.Entry(adminReview).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary", adminReview.AppId);
            return View(adminReview);
        }

        // GET: AdminReview/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            AdminReview adminReview = db.AdminReviews.Find(id);
            if (adminReview == null)
            {
                return NotFound();
            }
            return View(adminReview);
        }

        // POST: AdminReview/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            AdminReview adminReview = db.AdminReviews.Find(id);
            db.AdminReviews.Remove(adminReview);
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
