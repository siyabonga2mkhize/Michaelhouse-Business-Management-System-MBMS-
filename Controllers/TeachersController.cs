using Michaelhouse.Models;
using Michaelhouse.Filters;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class TeachersController : Controller
    {
        private DBContextClass db = new DBContextClass();

        [RequireLogin]
        public ActionResult Dashboard()
        {
            int teacherId = (int)(Session["TeacherId"] ?? 0);
            var teacher = db.Teachers.FirstOrDefault(t => t.TeacherId == teacherId);

            if (teacher == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var today = System.DateTime.Today;
            var todayAttendance = db.TeacherAttendances
                .FirstOrDefault(ta => ta.TeacherId == teacherId && DbFunctions.TruncateTime(ta.Date) == today);

            var subjectCount = db.Subjects.Count(s => s.TeacherId == teacherId);
            var recentAttendance = db.Attendances
                .Where(a => a.RecordedBy == teacher.Email)
                .OrderByDescending(a => a.Date)
                .Take(5)
                .ToList();

            ViewBag.TeacherName = teacher.FullName;
            ViewBag.TodayAttendance = todayAttendance;
            ViewBag.SubjectCount = subjectCount;
            ViewBag.RecentAttendance = recentAttendance;

            return View();
        }

        // GET: Teachers
        public ActionResult Index()
        {
            return View(db.Teachers.ToList());
        }

        // GET: Teachers/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Teacher teacher = db.Teachers.Find(id);
            if (teacher == null)
            {
                return HttpNotFound();
            }
            return View(teacher);
        }

        // GET: Teachers/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Teachers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "FirstName,LastName,Email,EmployeeNumber")] Teacher teacher)
        {
            if (ModelState.IsValid)
            {
                using (var db = new DBContextClass())
                {
                    // Optional: Auto-create a User account for the teacher so they can login
                    var user = new AppUser
                    {
                        Name = $"{teacher.FirstName} {teacher.LastName}",
                        Email = teacher.Email,
                        PasswordHash = AccountController.HashPassword("Teacher@123"), // Default password
                        Role = "Teacher"
                    };

                    db.Users.Add(user);
                    db.SaveChanges(); // Save user first to get UserId

                    teacher.UserId = user.UserId;
                    db.Teachers.Add(teacher);
                    db.SaveChanges();

                    return RedirectToAction("Index");
                }
            }
            return View(teacher);
        }
        // GET: Teachers/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Teacher teacher = db.Teachers.Find(id);
            if (teacher == null)
            {
                return HttpNotFound();
            }
            return View(teacher);
        }

        // POST: Teachers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "TeacherId,FirstName,LastName,Email,Phone,EmployeeNumber,Department,Specialization,HireDate")] Teacher teacher)
        {
            if (ModelState.IsValid)
            {
                db.Entry(teacher).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(teacher);
        }

        // GET: Teachers/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Teacher teacher = db.Teachers.Find(id);
            if (teacher == null)
            {
                return HttpNotFound();
            }
            return View(teacher);
        }

        // POST: Teachers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Teacher teacher = db.Teachers.Find(id);
            db.Teachers.Remove(teacher);
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
