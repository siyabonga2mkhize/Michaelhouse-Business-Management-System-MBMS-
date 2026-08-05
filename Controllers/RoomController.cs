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
    public class RoomController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        public ActionResult Index(string search, int? residenceId, bool archived = false)
        {
            var query = db.Rooms.Include(r => r.Residence).Include(r => r.Beds).AsQueryable();
            query = query.Where(r => r.IsArchived == archived);
            if (residenceId.HasValue) query = query.Where(r => r.ResidenceId == residenceId.Value);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(r => r.RoomNumber.Contains(search) || r.Residence.Name.Contains(search));
            ViewBag.Search = search;
            ViewBag.Archived = archived;
            ViewBag.ResidenceId = residenceId;
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", residenceId);
            return View(query.OrderBy(r => r.Residence.Name).ThenBy(r => r.RoomNumber).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var room = db.Rooms.Include(r => r.Residence).Include(r => r.Beds.Select(b => b.OccupiedByStudent)).FirstOrDefault(r => r.RoomId == id);
            if (room == null) return HttpNotFound();
            return View(room);
        }

        public ActionResult Create(int? residenceId)
        {
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", residenceId);
            return View(new Room { ResidenceId = residenceId ?? 0, Capacity = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ResidenceId,RoomNumber,Capacity,Floor,IsGroundFloor,IsWheelchairAccessible,NearBathroom,IsQuietStudyRoom,NeedsMaintenance")] Room room)
        {
            ValidateRoom(room);
            if (!ModelState.IsValid)
            {
                ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", room.ResidenceId);
                return View(room);
            }

            db.Rooms.Add(room);
            db.SaveChanges();
            RecalculateResidence(room.ResidenceId);
            TempData["Success"] = "Room created.";
            return RedirectToAction("Details", new { id = room.RoomId });
        }

        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", room.ResidenceId);
            return View(room);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "RoomId,ResidenceId,RoomNumber,Capacity,Floor,IsGroundFloor,IsWheelchairAccessible,NearBathroom,IsQuietStudyRoom,NeedsMaintenance")] Room model)
        {
            ValidateRoom(model);
            var room = db.Rooms.Find(model.RoomId);
            if (room == null) return HttpNotFound();
            if (model.Capacity < room.OccupiedBeds) ModelState.AddModelError("Capacity", "Capacity cannot be lower than occupied beds.");
            if (!ModelState.IsValid)
            {
                ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", model.ResidenceId);
                return View(model);
            }

            var oldResidenceId = room.ResidenceId;
            room.ResidenceId = model.ResidenceId;
            room.RoomNumber = model.RoomNumber;
            room.Capacity = model.Capacity;
            room.Floor = model.Floor;
            room.IsGroundFloor = model.IsGroundFloor;
            room.IsWheelchairAccessible = model.IsWheelchairAccessible;
            room.NearBathroom = model.NearBathroom;
            room.IsQuietStudyRoom = model.IsQuietStudyRoom;
            room.NeedsMaintenance = model.NeedsMaintenance;
            room.IsFull = room.OccupiedBeds >= room.Capacity;
            db.SaveChanges();
            RecalculateResidence(oldResidenceId);
            RecalculateResidence(room.ResidenceId);
            TempData["Success"] = "Room updated.";
            return RedirectToAction("Details", new { id = room.RoomId });
        }

        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var room = db.Rooms.Include(r => r.Residence).FirstOrDefault(r => r.RoomId == id);
            if (room == null) return HttpNotFound();
            return View(room);
        }

        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();
            if (room.OccupiedBeds > 0 || db.Beds.Any(b => b.RoomId == id && b.IsOccupied))
            {
                TempData["Error"] = "Cannot archive a room with occupied beds.";
                return RedirectToAction("Details", new { id });
            }
            room.IsArchived = true;
            db.SaveChanges();
            RecalculateResidence(room.ResidenceId);
            TempData["Success"] = "Room archived.";
            return RedirectToAction("Index");
        }

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();
            room.IsArchived = false;
            db.SaveChanges();
            RecalculateResidence(room.ResidenceId);
            TempData["Success"] = "Room restored.";
            return RedirectToAction("Index", new { archived = true });
        }

        private void ValidateRoom(Room room)
        {
            if (room.Capacity <= 0) ModelState.AddModelError("Capacity", "Capacity must be at least 1.");
            if (db.Residences.Any(r => r.ResidenceId == room.ResidenceId && r.IsArchived)) ModelState.AddModelError("ResidenceId", "Cannot use an archived residence.");
            if (db.Rooms.Any(r => r.RoomId != room.RoomId && r.ResidenceId == room.ResidenceId && r.RoomNumber == room.RoomNumber && !r.IsArchived)) ModelState.AddModelError("RoomNumber", "Room number already exists in this residence.");
        }

        private void RecalculateResidence(int residenceId)
        {
            var residence = db.Residences.Find(residenceId);
            if (residence == null) return;
            var rooms = db.Rooms.Where(r => r.ResidenceId == residenceId && !r.IsArchived).ToList();
            residence.Capacity = rooms.Sum(r => r.Capacity);
            residence.OccupiedBeds = rooms.Sum(r => r.OccupiedBeds);
            db.SaveChanges();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
