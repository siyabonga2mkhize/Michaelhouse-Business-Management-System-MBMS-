using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using System;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    //[Authorize(Roles = "Admin")]
    public class EnrollmentsController : BaseController
    {
        private DBContextClass _context = DbContextFactory.Create();

        // GET: Enrollments
        public ActionResult Index()
        {
            var enrollments = _context.Enrollments.Include(e => e.Student).Include(e => e.Subject);
            return View(enrollments.ToList());
        }

        // GET: Enrollments/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Enrollment enrollment = _context.Enrollments.Find(id);
            if (enrollment == null)
            {
                return NotFound();
            }
            return View(enrollment);
        }

        // GET: Enrollments/Create
        public ActionResult Create()
        {
            var viewModel = new EnrollmentViewModel
            {
                AcademicYear = DateTime.Now.Year.ToString(),
                StudentList = _context.Students.Select(s => new SelectListItem
                {
                    Value = s.StudentId.ToString(),
                    Text = s.FirstName + " " + s.LastName + " (" + s.StudentId + ")"
                }),
                SubjectList = _context.Subjects.Select(s => new SelectListItem
                {
                    Value = s.SubjectId.ToString(),
                    Text = s.Name + " - Grade " + s.GradeLevel
                })
            };

            return View(viewModel);
        }

        // POST: Enrollments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(EnrollmentViewModel model)
        {
            if (ModelState.IsValid)
            {
                var enrollment = new Enrollment
                {
                    StudentId = model.SelectedStudentId,
                    SubjectId = model.SelectedSubjectId,
                    EnrollmentDate = DateTime.Now,
                    AcademicYear = model.AcademicYear
                };

                _context.Enrollments.Add(enrollment);
                _context.SaveChanges();
                return RedirectToAction("Index", "Home");
            }

            return View(model);
        }



        // GET: Enrollments/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Enrollment enrollment = _context.Enrollments.Find(id);
            if (enrollment == null)
            {
                return NotFound();
            }
            ViewBag.StudentId = new SelectList(_context.Students, "StudentId", "FirstName", enrollment.StudentId);
            ViewBag.SubjectId = new SelectList(_context.Subjects, "SubjectId", "Name", enrollment.SubjectId);
            return View(enrollment);
        }

        // POST: Enrollments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind("EnrollmentId,StudentId,SubjectId,EnrollmentDate,AcademicYear")] Enrollment enrollment)
        {
            if (ModelState.IsValid)
            {
                _context.Entry(enrollment).State = EntityState.Modified;
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.StudentId = new SelectList(_context.Students, "StudentId", "FirstName", enrollment.StudentId);
            ViewBag.SubjectId = new SelectList(_context.Subjects, "SubjectId", "Name", enrollment.SubjectId);
            return View(enrollment);
        }

        // GET: Enrollments/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            Enrollment enrollment = _context.Enrollments.Find(id);
            if (enrollment == null)
            {
                return NotFound();
            }
            return View(enrollment);
        }

        // POST: Enrollments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Enrollment enrollment = _context.Enrollments.Find(id);
            _context.Enrollments.Remove(enrollment);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
