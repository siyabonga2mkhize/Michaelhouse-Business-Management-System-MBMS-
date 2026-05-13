using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class ParentsController : Controller
    {
        private int GetCurrentParentId() => (int)Session["ParentId"];
        private DBContextClass db = new DBContextClass();

        // GET: Parents
        public ActionResult Index()
        {
            var parents = db.Parents.Include(p => p.User);
            return View(parents.ToList());
        }

        // GET: Parents/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Parent parent = db.Parents.Find(id);
            if (parent == null)
            {
                return HttpNotFound();
            }
            return View(parent);
        }

        // GET: Parents/Create
        public ActionResult Create()
        {
            ViewBag.UserId = new SelectList(db.Users, "UserId", "Name");
            return View();
        }

        // POST: Parents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ParentId,Name,Contact,UserId")] Parent parent)
        {
            if (ModelState.IsValid)
            {
                db.Parents.Add(parent);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.UserId = new SelectList(db.Users, "UserId", "Name", parent.UserId);
            return View(parent);
        }

        // GET: Parents/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Parent parent = db.Parents.Find(id);
            if (parent == null)
            {
                return HttpNotFound();
            }
            ViewBag.UserId = new SelectList(db.Users, "UserId", "Name", parent.UserId);
            return View(parent);
        }

        // POST: Parents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ParentId,Name,Contact,UserId")] Parent parent)
        {
            if (ModelState.IsValid)
            {
                db.Entry(parent).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.UserId = new SelectList(db.Users, "UserId", "Name", parent.UserId);
            return View(parent);
        }

        // GET: Parents/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Parent parent = db.Parents.Find(id);
            if (parent == null)
            {
                return HttpNotFound();
            }
            return View(parent);
        }

        // POST: Parents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Parent parent = db.Parents.Find(id);
            db.Parents.Remove(parent);
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
            using (var db = new DBContextClass())
            {
                int parentId = GetCurrentParentId();

                var parent = db.Parents.Find(parentId);

                // ADD .Include("Student") HERE to fix the text error
                var applications = db.Applications
                    .Include("Student")
                    .Include("AdminReviews")
                    .Where(a => a.ParentId == parentId)
                    .OrderByDescending(a => a.Date)
                    .ToList();

                var invoices = db.Invoices
                    .Include("Student")
                    .Include("Payments")
                    .Where(i => i.ParentId == parentId)
                    .OrderByDescending(i => i.CreatedDate)
                    .ToList();

                ViewBag.Parent = parent;
                ViewBag.Applications = applications;
                ViewBag.Invoices = invoices;

                return View();
            }
        }

        [RequireLogin]
        public ActionResult ChildrenAttendance(int? studentId, int? subjectId, DateTime? fromDate, DateTime? toDate)
        {
            using (var db = new DBContextClass())
            {
                int parentId = (int)Session["ParentId"];
                var children = db.Students.Where(s => s.ParentId == parentId).ToList();
                if (!children.Any())
                {
                    ViewBag.NoChildren = true;
                    return View(new List<ParentAttendanceViewModel>());
                }

                var childIds = children.Select(c => c.StudentId).ToList();
                var query = db.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.Subject)
                    .Where(a => childIds.Contains(a.StudentId))
                    .AsQueryable();

                // Apply filters
                if (studentId.HasValue)
                    query = query.Where(a => a.StudentId == studentId.Value);
                if (subjectId.HasValue)
                    query = query.Where(a => a.SubjectId == subjectId.Value);
                if (fromDate.HasValue)
                    query = query.Where(a => a.Date >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(a => a.Date <= toDate.Value);

                // 1. SUMMARY (group by student + subject) – respects all filters
                var summaryRaw = query
                    .GroupBy(a => new { a.StudentId, a.SubjectId })
                    .Select(g => new
                    {
                        g.Key.StudentId,
                        g.Key.SubjectId,
                        TotalClasses = g.Count(),
                        PresentCount = g.Count(a => a.Status == AttendanceStatus.Present),
                        LateCount = g.Count(a => a.Status == AttendanceStatus.Late),
                        AbsentCount = g.Count(a => a.Status == AttendanceStatus.Absent)
                    })
                    .ToList();

                var summary = (from s in summaryRaw
                               join student in db.Students on s.StudentId equals student.StudentId
                               join subject in db.Subjects on s.SubjectId equals subject.SubjectId
                               select new ParentAttendanceSummaryViewModel
                               {
                                   StudentId = s.StudentId,
                                   StudentName = student.FirstName + " " + student.LastName,
                                   GradeLevel = student.GradeLevel,
                                   SubjectId = s.SubjectId,
                                   SubjectName = subject.Name,
                                   TotalClasses = s.TotalClasses,
                                   PresentCount = s.PresentCount,
                                   LateCount = s.LateCount,
                                   AbsentCount = s.AbsentCount
                               })
               .OrderBy(x => x.StudentName)
               .ThenBy(x => x.SubjectName)
               .ToList();
                // 2. DETAILED records
                var records = query
                    .OrderByDescending(a => a.Date)
                    .Select(a => new ParentAttendanceViewModel
                    {
                        StudentName = a.Student.FirstName + " " + a.Student.LastName,
                        Date = a.Date,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    })
                    .ToList();

                // For dropdowns: list of children + list of subjects (only those that appear in their attendance, or all subjects?)
                // To make it user-friendly, we'll provide all subjects that have any attendance record for these children.
                var availableSubjects = db.Subjects
                    .Where(s => db.Attendances.Any(a => childIds.Contains(a.StudentId) && a.SubjectId == s.SubjectId))
                    .OrderBy(s => s.Name)
                    .ToList();

                ViewBag.Students = children;
                ViewBag.Subjects = availableSubjects;
                ViewBag.SelectedStudentId = studentId;
                ViewBag.SelectedSubjectId = subjectId;
                ViewBag.AttendanceSummary = summary;

                return View(records);
            }
        }

        public ActionResult GetChildSubjectAttendanceDetails(int studentId, int subjectId)
        {
            using (var db = new DBContextClass())
            {
                var records = db.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.Subject)
                    .Where(a => a.StudentId == studentId && a.SubjectId == subjectId)
                    .OrderByDescending(a => a.Date)
                    .Select(a => new ParentAttendanceViewModel
                    {
                        StudentName = a.Student.FirstName + " " + a.Student.LastName,
                        Date = a.Date,
                        SubjectName = a.Subject.Name,
                        Status = a.Status,
                        RecordedBy = a.RecordedBy
                    })
                    .ToList();

                if (!records.Any())
                    return Content("<p class='text-sm text-gray-500'>No attendance records found for this subject.</p>");

                return PartialView("_ChildSubjectAttendanceDetails", records);
            }
        }
    }
}
