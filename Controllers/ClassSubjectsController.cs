using Michaelhouse.Models;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class ClassSubjectsController : Controller
    {
        private DBContextClass _context = new DBContextClass();

        // GET: ClassSubjects/Create
        public ActionResult Create()
        {
            // Prepare dropdown data for the Admin
            ViewBag.ClassId = new SelectList(_context.SchoolClasses, "ClassId", "ClassName");
            ViewBag.SubjectId = new SelectList(_context.Subjects, "SubjectId", "SubjectName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ClassSubject classSubject)
        {
            if (ModelState.IsValid)
            {
                _context.ClassSubjects.Add(classSubject);
                await _context.SaveChangesAsync();

                // After assigning, go back to the dashboard or a list view
                return RedirectToAction("Dashboard", "Admin");
            }

            ViewBag.ClassId = new SelectList(_context.SchoolClasses, "ClassId", "ClassName", classSubject.ClassId);
            ViewBag.SubjectId = new SelectList(_context.Subjects, "SubjectId", "SubjectName", classSubject.SubjectId);
            return View(classSubject);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _context.Dispose();
            base.Dispose(disposing);
        }
    }
}