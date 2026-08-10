using Michaelhouse.Filters;
using Michaelhouse.Models;
using Michaelhouse.Services;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOrHouseMasterOnly]
    public class QRScanRecordController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();
        private readonly BoardingAccessService boardingAccess = new BoardingAccessService();

        public ActionResult Index(string search, string actionType, int? studentId, bool archived = false)
        {
            var query = db.QRScanRecords.Include(q => q.Student).Include(q => q.Residence).Include(q => q.HouseMaster).AsQueryable();
            query = query.Where(q => q.IsArchived == archived);
            var residenceIds = boardingAccess.GetAccessibleResidenceIds(this, db);
            query = query.Where(q => q.ResidenceId.HasValue && residenceIds.Contains(q.ResidenceId.Value));
            if (studentId.HasValue) query = query.Where(q => q.StudentId == studentId.Value);
            if (!string.IsNullOrWhiteSpace(actionType)) query = query.Where(q => q.Action == actionType);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(q => q.Student.FirstName.Contains(search) || q.Student.LastName.Contains(search) || q.Action.Contains(search) || q.Scanner.Contains(search));
            ViewBag.Search = search;
            ViewBag.ActionType = actionType;
            ViewBag.StudentId = studentId;
            ViewBag.Archived = archived;
            return View(query.OrderByDescending(q => q.ScannedAt).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var record = db.QRScanRecords.Include(q => q.Student).Include(q => q.Residence).Include(q => q.HouseMaster).FirstOrDefault(q => q.QRScanRecordId == id);
            if (record == null) return HttpNotFound();
            if (!record.ResidenceId.HasValue || !boardingAccess.CanAccessResidence(this, db, record.ResidenceId.Value)) return new HttpUnauthorizedResult();
            return View(record);
        }

        public ActionResult ScanHistory()
        {
            return RedirectToAction("Index");
        }

        public ActionResult StudentScanHistory(int studentId)
        {
            return RedirectToAction("Index", new { studentId });
        }

        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var record = db.QRScanRecords.Include(q => q.Student).FirstOrDefault(q => q.QRScanRecordId == id);
            if (record == null) return HttpNotFound();
            if (!record.ResidenceId.HasValue || !boardingAccess.CanAccessResidence(this, db, record.ResidenceId.Value)) return new HttpUnauthorizedResult();
            return View(record);
        }

        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(int id)
        {
            var record = db.QRScanRecords.Find(id);
            if (record == null) return HttpNotFound();
            if (!record.ResidenceId.HasValue || !boardingAccess.CanAccessResidence(this, db, record.ResidenceId.Value)) return new HttpUnauthorizedResult();
            record.IsArchived = true;
            db.SaveChanges();
            TempData["Success"] = "QR scan record archived.";
            return RedirectToAction("Index");
        }

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var record = db.QRScanRecords.Find(id);
            if (record == null) return HttpNotFound();
            record.IsArchived = false;
            db.SaveChanges();
            TempData["Success"] = "QR scan record restored.";
            return RedirectToAction("Index", new { archived = true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
