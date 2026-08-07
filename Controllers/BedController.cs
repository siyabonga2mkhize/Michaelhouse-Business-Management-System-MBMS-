using Michaelhouse.Filters;
using Michaelhouse.Models;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    [AdminOrHouseMasterOnly]
    public class BedController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        public ActionResult Index(string search, int? roomId, bool archived = false)
        {
            var query = db.Beds.Include(b => b.Room.Residence).Include(b => b.OccupiedByStudent).AsQueryable();
            query = query.Where(b => b.IsArchived == archived);
            if (roomId.HasValue) query = query.Where(b => b.RoomId == roomId.Value);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(b => b.BedNumber.Contains(search) || b.Room.RoomNumber.Contains(search) || b.Room.Residence.Name.Contains(search));
            ViewBag.Search = search;
            ViewBag.RoomId = roomId;
            ViewBag.Archived = archived;
            ViewBag.Rooms = new SelectList(db.Rooms.Where(r => !r.IsArchived && !r.Residence.IsArchived).Include(r => r.Residence).OrderBy(r => r.Residence.Name).ThenBy(r => r.RoomNumber).ToList(), "RoomId", "RoomNumber", roomId);
            return View(query.OrderBy(b => b.Room.Residence.Name).ThenBy(b => b.Room.RoomNumber).ThenBy(b => b.BedNumber).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var bed = db.Beds.Include(b => b.Room.Residence).Include(b => b.OccupiedByStudent).FirstOrDefault(b => b.BedId == id);
            if (bed == null) return HttpNotFound();
            return View(bed);
        }

        public ActionResult Create(int? roomId)
        {
            ViewBag.Rooms = RoomList(roomId);
            return View(new Bed { RoomId = roomId ?? 0, Status = "Available" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "RoomId,BedNumber,Status")] Bed bed)
        {
            ValidateBed(bed);
            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = RoomList(bed.RoomId);
                return View(bed);
            }

            bed.IsOccupied = bed.Status == "Occupied";
            db.Beds.Add(bed);
            db.SaveChanges();
            TempData["Success"] = "Bed created.";
            return RedirectToAction("Details", new { id = bed.BedId });
        }

        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var bed = db.Beds.Find(id);
            if (bed == null) return HttpNotFound();
            ViewBag.Rooms = RoomList(bed.RoomId);
            return View(bed);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "BedId,RoomId,BedNumber,Status")] Bed model)
        {
            ValidateBed(model);
            var bed = db.Beds.Find(model.BedId);
            if (bed == null) return HttpNotFound();
            if (bed.IsOccupied && model.Status != "Occupied") ModelState.AddModelError("Status", "Release the active assignment before changing an occupied bed status.");
            if (!ModelState.IsValid)
            {
                ViewBag.Rooms = RoomList(model.RoomId);
                return View(model);
            }

            bed.RoomId = model.RoomId;
            bed.BedNumber = model.BedNumber;
            bed.Status = model.Status;
            bed.IsOccupied = model.Status == "Occupied";
            db.SaveChanges();
            TempData["Success"] = "Bed updated.";
            return RedirectToAction("Details", new { id = bed.BedId });
        }

        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var bed = db.Beds.Include(b => b.Room.Residence).FirstOrDefault(b => b.BedId == id);
            if (bed == null) return HttpNotFound();
            return View(bed);
        }

        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(int id)
        {
            var bed = db.Beds.Find(id);
            if (bed == null) return HttpNotFound();
            if (bed.IsOccupied || db.ResidenceAssignments.Any(a => a.BedId == id && a.IsActive))
            {
                TempData["Error"] = "Cannot archive an occupied bed.";
                return RedirectToAction("Details", new { id });
            }
            bed.IsArchived = true;
            bed.Status = "Maintenance";
            db.SaveChanges();
            TempData["Success"] = "Bed archived.";
            return RedirectToAction("Index");
        }

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var bed = db.Beds.Find(id);
            if (bed == null) return HttpNotFound();
            bed.IsArchived = false;
            bed.Status = bed.IsOccupied ? "Occupied" : "Available";
            db.SaveChanges();
            TempData["Success"] = "Bed restored.";
            return RedirectToAction("Index", new { archived = true });
        }

        private void ValidateBed(Bed bed)
        {
            if (string.IsNullOrWhiteSpace(bed.BedNumber)) ModelState.AddModelError("BedNumber", "Bed number is required.");
            if (db.Rooms.Any(r => r.RoomId == bed.RoomId && (r.IsArchived || r.Residence.IsArchived))) ModelState.AddModelError("RoomId", "Cannot use an archived room or residence.");
            if (db.Beds.Any(b => b.BedId != bed.BedId && b.RoomId == bed.RoomId && b.BedNumber == bed.BedNumber && !b.IsArchived)) ModelState.AddModelError("BedNumber", "Bed number already exists in this room.");
        }

        private SelectList RoomList(int? selected)
        {
            var rooms = db.Rooms.Include(r => r.Residence).Where(r => !r.IsArchived && !r.Residence.IsArchived).OrderBy(r => r.Residence.Name).ThenBy(r => r.RoomNumber).ToList();
            return new SelectList(rooms.Select(r => new { r.RoomId, Label = r.Residence.Name + " - Room " + r.RoomNumber }), "RoomId", "Label", selected);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
