using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Michaelhouse.Models;

namespace Michaelhouse.Controllers
{
    public class StudentsController : Controller
    {
        private DBContextClass db = new DBContextClass();
        private int GetCurrentStudentId()
        {
            if (Session["StudentId"] == null)
                return 0;
            return (int)Session["StudentId"];
        }

        // GET: Students
        public ActionResult Index()
        {
            var students = db.Students.Include(s => s.Parent);
            return View(students.ToList());
        }

        // GET: Students/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }

        // GET: Students/Create
        public ActionResult Create()
        {
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name");
            return View();
        }

        // POST: Students/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "StudentId,Name,DOB,ParentId")] Student student)
        {
            if (ModelState.IsValid)
            {
                db.Students.Add(student);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name", student.ParentId);
            return View(student);
        }

        // GET: Students/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name", student.ParentId);
            return View(student);
        }

        // POST: Students/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "StudentId,Name,DOB,ParentId")] Student student)
        {
            if (ModelState.IsValid)
            {
                db.Entry(student).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.ParentId = new SelectList(db.Parents, "ParentId", "Name", student.ParentId);
            return View(student);
        }

        // GET: Students/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Student student = db.Students.Find(id);
            if (student == null)
            {
                return HttpNotFound();
            }
            return View(student);
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Student student = db.Students.Find(id);
            db.Students.Remove(student);
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
		public ActionResult Dashboard()
		{
			int userId = (int)Session["UserId"];
			using (var db = new DBContextClass())
			{
				// CRITICAL: .Include("Parent") ensures the Guardian data is loaded
				var student = db.Students
					.Include("Parent")
					.FirstOrDefault(s => s.UserId == userId);

				if (student == null) return RedirectToAction("Login", "Account");

				var reg = db.Registrations
					.FirstOrDefault(r => r.StudentId == student.StudentId);

				// CRITICAL: .Include("Subject") ensures the Subject names are loaded
				var subjects = db.StudentSubjects
					.Include("Subject")
					.Where(ss => ss.StudentId == student.StudentId)
					.ToList();

				var streamEnr = db.StreamEnrolments
					.FirstOrDefault(se => se.StudentId == student.StudentId);

				ViewBag.Student = student;
				ViewBag.Registration = reg;
				ViewBag.Subjects = subjects;
				ViewBag.StreamEnrolment = streamEnr;

				return View();
			}
		}
	
    }
}
