using Michaelhouse.Models;
using Michaelhouse.Filters;
using Michaelhouse.Services;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOrHouseMasterOnly]
    public class StudentQRCodeController : Controller
    {
        private DBContextClass db = new DBContextClass();
        private StudentQRCodeService service = new StudentQRCodeService();

        public ActionResult Index(string search)
        {
            var query = db.StudentQRCodes.Include("Student").AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(q => q.Student.FirstName.Contains(search) || q.Student.LastName.Contains(search) || q.Student.StudentNumber.Contains(search));
            ViewBag.Search = search;
            return View(query.OrderByDescending(q => q.IsActive).ThenBy(q => q.Student.LastName).ToList());
        }

        [AdminOnly]
        public ActionResult Generate(int studentId)
        {
            service.GenerateQRCode(studentId);
            TempData["Success"] = "QR code generated.";
            return RedirectToAction("Details", new { studentId });
        }

        // -----------------------------------------------
        // View / print card
        // -----------------------------------------------
        public ActionResult Details(int studentId)
        {
            var student = db.Students.Find(studentId);
            if (student == null)
                return HttpNotFound();

            var qr = service.GetActiveQRCode(studentId)
                      ?? service.GenerateQRCode(studentId); // auto-generate if missing

            ViewBag.Student = student;
            return View(qr);
        }

        public ActionResult Print(int studentId)
        {
            var student = db.Students.Find(studentId);
            if (student == null)
                return HttpNotFound();

            var qr = service.GetActiveQRCode(studentId)
                      ?? service.GenerateQRCode(studentId);

            ViewBag.Student = student;
            return View(qr);
        }

        // -----------------------------------------------
        // Download raw PNG
        // -----------------------------------------------
        public FileResult Download(int studentId)
        {
            var student = db.Students.Find(studentId);
            var qr = service.GetActiveQRCode(studentId);

            if (qr == null || qr.QRImage == null)
                return null;

            string fileName = $"{qr.QRCodeValue}.png";
            return File(qr.QRImage, "image/png", fileName);
        }

        // Inline version — used as the src of an <img> tag so the
        // QR renders directly on Details/Print without a separate download
        public FileResult Image(int studentId)
        {
            var qr = service.GetActiveQRCode(studentId);

            if (qr == null || qr.QRImage == null)
                return null;

            return File(qr.QRImage, "image/png");
        }

        // -----------------------------------------------
        // Regenerate — Admin only
        // -----------------------------------------------
        [AdminOnly]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Regenerate(int studentId)
        {
            service.RegenerateQRCode(studentId);

            TempData["Success"] = "QR code regenerated successfully.";
            return RedirectToAction("Details", new { studentId });
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
