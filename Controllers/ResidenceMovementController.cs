using Michaelhouse.Filters;
using Michaelhouse.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOrHouseMasterOnly]
    public class ResidenceMovementController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        public ActionResult Index(string search, bool archived = false)
        {
            var query = db.ResidenceMovements.Include(m => m.Student).Include(m => m.FromResidence).Include(m => m.ToResidence).Include(m => m.FromRoom).Include(m => m.ToRoom).AsQueryable();
            query = query.Where(m => m.IsArchived == archived);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(m => m.Student.FirstName.Contains(search) || m.Student.LastName.Contains(search) || m.Reason.Contains(search));
            ViewBag.Search = search;
            ViewBag.Archived = archived;
            return View(query.OrderByDescending(m => m.PerformedAt).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var movement = db.ResidenceMovements.Include(m => m.Student).Include(m => m.FromResidence).Include(m => m.ToResidence).Include(m => m.FromRoom).Include(m => m.ToRoom).FirstOrDefault(m => m.ResidenceMovementId == id);
            if (movement == null) return HttpNotFound();
            return View(movement);
        }

        public ActionResult Create()
        {
            PopulateLists();
            return View(new ResidenceMovement { PerformedAt = DateTime.UtcNow, PerformedByName = Session["UserName"] as string });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "StudentId,FromResidenceId,FromRoomId,ToResidenceId,ToRoomId,PerformedAt,Reason")] ResidenceMovement model)
        {
            if (!ModelState.IsValid)
            {
                PopulateLists(model);
                return View(model);
            }
            model.PerformedByUserId = (int?)Session["UserId"];
            model.PerformedByName = Session["UserName"] as string ?? "system";
            db.ResidenceMovements.Add(model);
            db.SaveChanges();
            TempData["Success"] = "Residence movement recorded.";
            return RedirectToAction("Details", new { id = model.ResidenceMovementId });
        }

        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var movement = db.ResidenceMovements.Find(id);
            if (movement == null) return HttpNotFound();
            PopulateLists(movement);
            return View(movement);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ResidenceMovementId,StudentId,FromResidenceId,FromRoomId,ToResidenceId,ToRoomId,PerformedAt,Reason")] ResidenceMovement model)
        {
            var movement = db.ResidenceMovements.Find(model.ResidenceMovementId);
            if (movement == null) return HttpNotFound();
            if (!ModelState.IsValid)
            {
                PopulateLists(model);
                return View(model);
            }
            movement.StudentId = model.StudentId;
            movement.FromResidenceId = model.FromResidenceId;
            movement.FromRoomId = model.FromRoomId;
            movement.ToResidenceId = model.ToResidenceId;
            movement.ToRoomId = model.ToRoomId;
            movement.PerformedAt = model.PerformedAt;
            movement.Reason = model.Reason;
            db.SaveChanges();
            TempData["Success"] = "Residence movement updated.";
            return RedirectToAction("Details", new { id = movement.ResidenceMovementId });
        }

        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var movement = db.ResidenceMovements.Include(m => m.Student).FirstOrDefault(m => m.ResidenceMovementId == id);
            if (movement == null) return HttpNotFound();
            return View(movement);
        }

        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(int id)
        {
            var movement = db.ResidenceMovements.Find(id);
            if (movement == null) return HttpNotFound();
            movement.IsArchived = true;
            db.SaveChanges();
            TempData["Success"] = "Movement archived.";
            return RedirectToAction("Index");
        }

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var movement = db.ResidenceMovements.Find(id);
            if (movement == null) return HttpNotFound();
            movement.IsArchived = false;
            db.SaveChanges();
            TempData["Success"] = "Movement restored.";
            return RedirectToAction("Index", new { archived = true });
        }

        private void PopulateLists(ResidenceMovement selected = null)
        {
            ViewBag.Students = new SelectList(db.Students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList(), "StudentId", "Name", selected?.StudentId);
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name");
            ViewBag.Rooms = new SelectList(db.Rooms.Where(r => !r.IsArchived && !r.Residence.IsArchived).OrderBy(r => r.RoomNumber).ToList(), "RoomId", "RoomNumber");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
