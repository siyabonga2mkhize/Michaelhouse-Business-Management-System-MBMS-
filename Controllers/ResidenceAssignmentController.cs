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
    public class ResidenceAssignmentController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        public ActionResult Index(string search, bool archived = false)
        {
            var query = db.ResidenceAssignments.Include(a => a.Student).Include(a => a.Residence).Include(a => a.Room).Include(a => a.Bed).AsQueryable();
            query = query.Where(a => a.IsArchived == archived);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(a => a.Student.FirstName.Contains(search) || a.Student.LastName.Contains(search) || a.Residence.Name.Contains(search) || a.Room.RoomNumber.Contains(search));
            ViewBag.Search = search;
            ViewBag.Archived = archived;
            return View(query.OrderByDescending(a => a.MoveInDate).ToList());
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var assignment = db.ResidenceAssignments.Include(a => a.Student).Include(a => a.Residence).Include(a => a.Room).Include(a => a.Bed).FirstOrDefault(a => a.ResidenceAssignmentId == id);
            if (assignment == null) return HttpNotFound();
            return View(assignment);
        }

        public ActionResult Create()
        {
            PopulateLists();
            return View(new ResidenceAssignment { MoveInDate = DateTime.Now, Status = "Active", IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "StudentId,ResidenceId,RoomId,BedId,MoveInDate,Status,IsActive")] ResidenceAssignment model)
        {
            ValidateAssignment(model, true);
            if (!ModelState.IsValid)
            {
                PopulateLists(model);
                return View(model);
            }

            db.ResidenceAssignments.Add(model);
            SetBedOccupied(model.BedId, model.StudentId, true);
            Recalculate(model.RoomId, model.ResidenceId);
            db.SaveChanges();
            TempData["Success"] = "Residence assignment created.";
            return RedirectToAction("Details", new { id = model.ResidenceAssignmentId });
        }

        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var assignment = db.ResidenceAssignments.Find(id);
            if (assignment == null) return HttpNotFound();
            PopulateLists(assignment);
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ResidenceAssignmentId,StudentId,ResidenceId,RoomId,BedId,MoveInDate,VacatedDate,Status,IsActive")] ResidenceAssignment model)
        {
            var assignment = db.ResidenceAssignments.Find(model.ResidenceAssignmentId);
            if (assignment == null) return HttpNotFound();
            ValidateAssignment(model, false);
            if (!ModelState.IsValid)
            {
                PopulateLists(model);
                return View(model);
            }

            var oldRoomId = assignment.RoomId;
            var oldResidenceId = assignment.ResidenceId;
            var oldBedId = assignment.BedId;

            assignment.StudentId = model.StudentId;
            assignment.ResidenceId = model.ResidenceId;
            assignment.RoomId = model.RoomId;
            assignment.BedId = model.BedId;
            assignment.MoveInDate = model.MoveInDate;
            assignment.VacatedDate = model.VacatedDate;
            assignment.Status = model.Status;
            assignment.IsActive = model.IsActive;

            if (oldBedId != model.BedId) SetBedOccupied(oldBedId, null, false);
            SetBedOccupied(model.BedId, model.StudentId, model.IsActive);
            Recalculate(oldRoomId, oldResidenceId);
            Recalculate(model.RoomId, model.ResidenceId);
            db.SaveChanges();
            TempData["Success"] = "Residence assignment updated.";
            return RedirectToAction("Details", new { id = assignment.ResidenceAssignmentId });
        }

        public ActionResult Archive(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var assignment = db.ResidenceAssignments.Include(a => a.Student).Include(a => a.Residence).FirstOrDefault(a => a.ResidenceAssignmentId == id);
            if (assignment == null) return HttpNotFound();
            return View(assignment);
        }

        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(int id)
        {
            var assignment = db.ResidenceAssignments.Find(id);
            if (assignment == null) return HttpNotFound();
            if (assignment.IsActive)
            {
                TempData["Error"] = "Cannot archive an active assignment. Check out or transfer the student first.";
                return RedirectToAction("Details", new { id });
            }
            assignment.IsArchived = true;
            db.SaveChanges();
            TempData["Success"] = "Assignment archived.";
            return RedirectToAction("Index");
        }

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var assignment = db.ResidenceAssignments.Find(id);
            if (assignment == null) return HttpNotFound();
            assignment.IsArchived = false;
            db.SaveChanges();
            TempData["Success"] = "Assignment restored.";
            return RedirectToAction("Index", new { archived = true });
        }

        private void ValidateAssignment(ResidenceAssignment model, bool isNew)
        {
            if (db.ResidenceAssignments.Any(a => a.ResidenceAssignmentId != model.ResidenceAssignmentId && a.StudentId == model.StudentId && a.IsActive && !a.IsArchived)) ModelState.AddModelError("StudentId", "Student already has an active residence assignment.");
            if (db.Beds.Any(b => b.BedId == model.BedId && (b.IsArchived || b.IsOccupied) && (isNew || b.OccupiedByStudentId != model.StudentId))) ModelState.AddModelError("BedId", "Bed is not available.");
            if (db.Rooms.Any(r => r.RoomId == model.RoomId && (r.IsArchived || r.IsFull || r.OccupiedBeds >= r.Capacity || r.Residence.IsArchived))) ModelState.AddModelError("RoomId", "Room is not available.");
            if (db.Beds.Any(b => b.BedId == model.BedId && b.RoomId != model.RoomId)) ModelState.AddModelError("BedId", "Bed must belong to the selected room.");
            if (db.Rooms.Any(r => r.RoomId == model.RoomId && r.ResidenceId != model.ResidenceId)) ModelState.AddModelError("RoomId", "Room must belong to the selected residence.");
        }

        private void PopulateLists(ResidenceAssignment selected = null)
        {
            ViewBag.Students = new SelectList(db.Students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList(), "StudentId", "Name", selected?.StudentId);
            ViewBag.Residences = new SelectList(db.Residences.Where(r => !r.IsArchived).OrderBy(r => r.Name).ToList(), "ResidenceId", "Name", selected?.ResidenceId);
            ViewBag.Rooms = new SelectList(db.Rooms.Where(r => !r.IsArchived && !r.Residence.IsArchived).OrderBy(r => r.RoomNumber).ToList(), "RoomId", "RoomNumber", selected?.RoomId);
            ViewBag.Beds = new SelectList(db.Beds.Where(b => !b.IsArchived && (!b.IsOccupied || b.BedId == selected.BedId)).OrderBy(b => b.BedNumber).ToList(), "BedId", "BedNumber", selected?.BedId);
        }

        private void SetBedOccupied(int bedId, int? studentId, bool occupied)
        {
            var bed = db.Beds.Find(bedId);
            if (bed == null) return;
            bed.IsOccupied = occupied;
            bed.Status = occupied ? "Occupied" : "Available";
            bed.OccupiedByStudentId = occupied ? studentId : null;
        }

        private void Recalculate(int roomId, int residenceId)
        {
            var room = db.Rooms.Find(roomId);
            if (room != null)
            {
                room.OccupiedBeds = db.ResidenceAssignments.Count(a => a.RoomId == roomId && a.IsActive && !a.IsArchived);
                room.IsFull = room.OccupiedBeds >= room.Capacity;
            }
            var residence = db.Residences.Find(residenceId);
            if (residence != null) residence.OccupiedBeds = db.ResidenceAssignments.Count(a => a.ResidenceId == residenceId && a.IsActive && !a.IsArchived);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
