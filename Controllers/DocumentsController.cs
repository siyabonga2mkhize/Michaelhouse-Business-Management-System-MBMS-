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
    public class DocumentsController : BaseController
    {
        private DBContextClass db = DbContextFactory.Create();

        // GET: Documents
        public ActionResult Index()
        {
            var documents = db.Documents.Include(d => d.Application).Include(d => d.Student);
            return View(documents.ToList());
        }

        // GET: Documents/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Document document = db.Documents.Find(id);
            if (document == null)
            {
                return NotFound();
            }
            return View(document);
        }

        // GET: Documents/Create
        public ActionResult Create()
        {
            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary");
            ViewBag.StudentId = new SelectList(db.Students, "StudentId", "Name");
            return View();
        }

        // POST: Documents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind("DId,StudentId,AppId,Type,FilePath,FileName,ContentType,UploadedAt")] Document document)
        {
            if (ModelState.IsValid)
            {
                db.Documents.Add(document);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary", document.AppId);
            ViewBag.StudentId = new SelectList(db.Students, "StudentId", "Name", document.StudentId);
            return View(document);
        }

        // GET: Documents/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Document document = db.Documents.Find(id);
            if (document == null)
            {
                return NotFound();
            }
            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary", document.AppId);
            ViewBag.StudentId = new SelectList(db.Students, "StudentId", "Name", document.StudentId);
            return View(document);
        }

        // POST: Documents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind("DId,StudentId,AppId,Type,FilePath,FileName,ContentType,UploadedAt")] Document document)
        {
            if (ModelState.IsValid)
            {
                db.Entry(document).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.AppId = new SelectList(db.Applications, "AppId", "AiReviewSummary", document.AppId);
            ViewBag.StudentId = new SelectList(db.Students, "StudentId", "Name", document.StudentId);
            return View(document);
        }

        // GET: Documents/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Document document = db.Documents.Find(id);
            if (document == null)
            {
                return NotFound();
            }
            return View(document);
        }

        // POST: Documents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Document document = db.Documents.Find(id);
            db.Documents.Remove(document);
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
