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

        // ============================================================
        // INDEX
        // ============================================================

        public ActionResult Index(string search, bool archived = false)
        {
            var query = db.ResidenceMovements
                .Include(m => m.Student)
                .Include(m => m.FromResidence)
                .Include(m => m.ToResidence)
                .Include(m => m.FromRoom)
                .Include(m => m.ToRoom)
                .Where(m => m.IsArchived == archived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(m =>
                    m.Student.FirstName.Contains(search) ||
                    m.Student.LastName.Contains(search) ||
                    m.Reason.Contains(search));
            }

            ViewBag.Search = search;
            ViewBag.Archived = archived;

            return View(
                query
                    .OrderByDescending(m => m.PerformedAt)
                    .ToList()
            );
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var movement = db.ResidenceMovements
                .Include(m => m.Student)
                .Include(m => m.FromResidence)
                .Include(m => m.ToResidence)
                .Include(m => m.FromRoom)
                .Include(m => m.ToRoom)
                .FirstOrDefault(
                    m => m.ResidenceMovementId == id.Value
                );

            if (movement == null)
                return HttpNotFound();

            return View(movement);
        }


        // ============================================================
        // CREATE / MOVE STUDENT
        // ============================================================

        [HttpGet]
        public ActionResult Create()
        {
            PopulateDestinationLists();

            return View(
                new ResidenceMovement
                {
                    PerformedAt = DateTime.UtcNow,
                    PerformedByName =
                        Session["UserName"] as string ?? "system"
                }
            );
        }


        // ============================================================
        // SEARCH STUDENTS
        // ============================================================

        [HttpGet]
        public JsonResult SearchStudents(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(
                    new object[0],
                    JsonRequestBehavior.AllowGet
                );
            }

            term = term.Trim();

            var students = db.Students
                .Where(s =>
                    s.FirstName.Contains(term) ||
                    s.LastName.Contains(term) ||
                    s.StudentNumber.Contains(term))
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Take(10)
                .Select(s => new
                {
                    id = s.StudentId,
                    name = s.FirstName + " " + s.LastName,
                    studentNumber = s.StudentNumber
                })
                .ToList();

            return Json(
                students,
                JsonRequestBehavior.AllowGet
            );
        }


        // ============================================================
        // GET CURRENT STUDENT RESIDENCE
        // ============================================================

        [HttpGet]
        public JsonResult GetStudentAssignment(int studentId)
        {
            var assignment = db.ResidenceAssignments
                .Include(a => a.Student)
                .Include(a => a.Residence)
                .Include(a => a.Room)
                .Include(a => a.Bed)
                .FirstOrDefault(a =>
                    a.StudentId == studentId &&
                    a.IsActive);

            if (assignment == null)
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "This student does not currently have an active residence assignment."
                    },
                    JsonRequestBehavior.AllowGet
                );
            }

            return Json(
                new
                {
                    success = true,

                    studentId = assignment.StudentId,

                    residenceId = assignment.ResidenceId,
                    residence = assignment.Residence.Name,

                    roomId = assignment.RoomId,
                    room = assignment.Room.RoomNumber,

                    bedId = assignment.BedId,
                    bed = assignment.Bed.BedNumber
                },
                JsonRequestBehavior.AllowGet
            );
        }


        // ============================================================
        // GET ROOMS FOR DESTINATION RESIDENCE
        // ============================================================

        [HttpGet]
        public JsonResult GetRooms(int residenceId)
        {
            var rooms = db.Rooms
                .Where(r =>
                    r.ResidenceId == residenceId &&
                    !r.IsArchived &&
                    !r.Residence.IsArchived &&
                    r.OccupiedBeds < r.Capacity)
                .OrderBy(r => r.RoomNumber)
                .Select(r => new
                {
                    id = r.RoomId,
                    name = r.RoomNumber,
                    availableBeds =
                        r.Capacity - r.OccupiedBeds
                })
                .ToList();

            return Json(
                rooms,
                JsonRequestBehavior.AllowGet
            );
        }


        // ============================================================
        // GET AVAILABLE BEDS FOR DESTINATION ROOM
        // ============================================================

        [HttpGet]
        public JsonResult GetBeds(int roomId)
        {
            var beds = db.Beds
                .Where(b =>
                    b.RoomId == roomId &&
                    !b.IsArchived &&
                    !b.IsOccupied &&
                    (b.Status == null ||
                     b.Status == "Available"))
                .OrderBy(b => b.BedNumber)
                .Select(b => new
                {
                    id = b.BedId,
                    name = b.BedNumber
                })
                .ToList();

            return Json(
                beds,
                JsonRequestBehavior.AllowGet
            );
        }


        // ============================================================
        // CREATE / ACTUALLY MOVE STUDENT
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            [Bind(Include =
                "StudentId,ToResidenceId,ToRoomId,ToBedId,Reason")]
            ResidenceMovement model)
        {
            if (!ModelState.IsValid)
            {
                PopulateDestinationLists(model);

                return View(model);
            }


            // --------------------------------------------------------
            // Find current assignment
            // --------------------------------------------------------

            var currentAssignment =
                db.ResidenceAssignments
                    .Include(a => a.Residence)
                    .Include(a => a.Room)
                    .Include(a => a.Bed)
                    .FirstOrDefault(a =>
                        a.StudentId == model.StudentId &&
                        a.IsActive);

            if (currentAssignment == null)
            {
                ModelState.AddModelError(
                    "",
                    "This student does not have an active residence assignment."
                );

                PopulateDestinationLists(model);

                return View(model);
            }


            // --------------------------------------------------------
            // Find destination
            // --------------------------------------------------------

            var destinationRoom = db.Rooms
                .Include(r => r.Residence)
                .FirstOrDefault(r =>
                    r.RoomId == model.ToRoomId &&
                    r.ResidenceId == model.ToResidenceId);

            if (destinationRoom == null)
            {
                ModelState.AddModelError(
                    "",
                    "The selected destination room is invalid."
                );

                PopulateDestinationLists(model);

                return View(model);
            }


            if (destinationRoom.OccupiedBeds >=
                destinationRoom.Capacity)
            {
                ModelState.AddModelError(
                    "",
                    "The selected room is full."
                );

                PopulateDestinationLists(model);

                return View(model);
            }


            // --------------------------------------------------------
            // Find destination bed
            // --------------------------------------------------------

            var destinationBed = db.Beds.FirstOrDefault(b =>
                b.BedId == model.ToBedId &&
                b.RoomId == model.ToRoomId &&
                !b.IsOccupied &&
                !b.IsArchived &&
                (b.Status == null ||
                 b.Status == "Available"));

            if (destinationBed == null)
            {
                ModelState.AddModelError(
                    "",
                    "The selected bed is no longer available."
                );

                PopulateDestinationLists(model);

                return View(model);
            }


            // --------------------------------------------------------
            // Prevent moving to same room/bed
            // --------------------------------------------------------

            if (currentAssignment.RoomId ==
                    model.ToRoomId &&
                currentAssignment.BedId ==
                    model.ToBedId)
            {
                ModelState.AddModelError(
                    "",
                    "The student is already assigned to this room and bed."
                );

                PopulateDestinationLists(model);

                return View(model);
            }


            // --------------------------------------------------------
            // Begin transaction
            // --------------------------------------------------------

            using (var transaction =
                db.Database.BeginTransaction())
            {
                try
                {
                    var oldResidence =
                        currentAssignment.Residence;

                    var oldRoom =
                        currentAssignment.Room;

                    var oldBed =
                        currentAssignment.Bed;


                    // ------------------------------------------------
                    // FREE OLD BED
                    // ------------------------------------------------

                    if (oldBed != null)
                    {
                        oldBed.IsOccupied = false;
                        oldBed.Status = "Available";
                        oldBed.OccupiedByStudentId = null;
                    }


                    // ------------------------------------------------
                    // UPDATE OLD ROOM
                    // ------------------------------------------------

                    if (oldRoom != null)
                    {
                        oldRoom.OccupiedBeds =
                            Math.Max(
                                0,
                                oldRoom.OccupiedBeds - 1
                            );

                        oldRoom.IsFull =
                            oldRoom.OccupiedBeds >=
                            oldRoom.Capacity;
                    }


                    // ------------------------------------------------
                    // UPDATE OLD RESIDENCE
                    // ------------------------------------------------

                    if (oldResidence != null)
                    {
                        oldResidence.OccupiedBeds =
                            Math.Max(
                                0,
                                oldResidence.OccupiedBeds - 1
                            );
                    }


                    // ------------------------------------------------
                    // MARK OLD ASSIGNMENT INACTIVE
                    // ------------------------------------------------

                    currentAssignment.IsActive = false;
                    currentAssignment.Status = "Transferred";
                    currentAssignment.VacatedDate =
                        DateTime.Now;


                    // ------------------------------------------------
                    // OCCUPY NEW BED
                    // ------------------------------------------------

                    destinationBed.IsOccupied = true;
                    destinationBed.Status = "Occupied";
                    destinationBed.OccupiedByStudentId =
                        model.StudentId;


                    // ------------------------------------------------
                    // UPDATE NEW ROOM
                    // ------------------------------------------------

                    destinationRoom.OccupiedBeds++;

                    destinationRoom.IsFull =
                        destinationRoom.OccupiedBeds >=
                        destinationRoom.Capacity;


                    // ------------------------------------------------
                    // UPDATE NEW RESIDENCE
                    // ------------------------------------------------

                    destinationRoom.Residence.OccupiedBeds++;
                    if (!model.ToResidenceId.HasValue)
                    {
                        ModelState.AddModelError(
                            "ToResidenceId",
                            "Please select a destination residence."
                        );
                    }

                    if (!model.ToRoomId.HasValue)
                    {
                        ModelState.AddModelError(
                            "ToRoomId",
                            "Please select a destination room."
                        );
                    }

                    if (!model.ToBedId.HasValue)
                    {
                        ModelState.AddModelError(
                            "ToBedId",
                            "Please select a destination bed."
                        );
                    }

                    if (!ModelState.IsValid)
                    {
                        PopulateDestinationLists(model);
                        return View(model);
                    }


                    // ------------------------------------------------
                    // CREATE NEW ASSIGNMENT
                    // ------------------------------------------------

                    var newAssignment = new ResidenceAssignment
                    {
                        StudentId = model.StudentId,

                        ResidenceId = model.ToResidenceId.Value,

                        RoomId = model.ToRoomId.Value,

                        BedId = model.ToBedId.Value,

                        IsActive = true,

                        MoveInDate = DateTime.Now,

                        Status = "Active"
                    };

                    db.ResidenceAssignments.Add(
                        newAssignment
                    );


                    // ------------------------------------------------
                    // RECORD MOVEMENT
                    // ------------------------------------------------

                    var movement =
                        new ResidenceMovement
                        {
                            StudentId =
                                model.StudentId,

                            FromResidenceId =
                                oldResidence?.ResidenceId,

                            FromRoomId =
                                oldRoom?.RoomId,

                            ToResidenceId =
                                model.ToResidenceId,

                            ToRoomId =
                                model.ToRoomId,

                            PerformedByUserId =
                                (int?)Session["UserId"],

                            PerformedByName =
                                Session["UserName"]
                                    as string ??
                                "system",

                            PerformedAt =
                                DateTime.UtcNow,

                            Reason =
                                model.Reason,

                            IsArchived = false
                        };

                    db.ResidenceMovements.Add(
                        movement
                    );


                    // ------------------------------------------------
                    // SAVE EVERYTHING
                    // ------------------------------------------------

                    db.SaveChanges();

                    transaction.Commit();


                    TempData["Success"] =
                        "Student successfully moved from " +
                        (oldResidence?.Name ?? "N/A") +
                        " / Room " +
                        (oldRoom?.RoomNumber ?? "N/A") +
                        " to " +
                        destinationRoom.Residence.Name +
                        " / Room " +
                        destinationRoom.RoomNumber +
                        " / Bed " +
                        destinationBed.BedNumber +
                        ".";


                    return RedirectToAction(
                        "Details",
                        new
                        {
                            id =
                                movement.ResidenceMovementId
                        }
                    );
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }


        // ============================================================
        // EDIT MOVEMENT RECORD
        // ============================================================

        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(
                    HttpStatusCode.BadRequest);

            var movement =
                db.ResidenceMovements.Find(id);

            if (movement == null)
                return HttpNotFound();

            PopulateDestinationLists(movement);

            return View(movement);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(
            [Bind(Include =
                "ResidenceMovementId,StudentId,FromResidenceId,FromRoomId,ToResidenceId,ToRoomId,PerformedAt,Reason")]
            ResidenceMovement model)
        {
            var movement =
                db.ResidenceMovements.Find(
                    model.ResidenceMovementId);

            if (movement == null)
                return HttpNotFound();

            if (!ModelState.IsValid)
            {
                PopulateDestinationLists(model);
                return View(model);
            }

            movement.StudentId =
                model.StudentId;

            movement.FromResidenceId =
                model.FromResidenceId;

            movement.FromRoomId =
                model.FromRoomId;

            movement.ToResidenceId =
                model.ToResidenceId;

            movement.ToRoomId =
                model.ToRoomId;

            movement.PerformedAt =
                model.PerformedAt;

            movement.Reason =
                model.Reason;

            db.SaveChanges();

            TempData["Success"] =
                "Movement record updated.";

            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        movement.ResidenceMovementId
                });
        }


        // ============================================================
        // ARCHIVE
        // ============================================================

        public ActionResult Archive(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(
                    HttpStatusCode.BadRequest);

            var movement =
                db.ResidenceMovements
                    .Include(m => m.Student)
                    .FirstOrDefault(
                        m =>
                            m.ResidenceMovementId ==
                            id.Value);

            if (movement == null)
                return HttpNotFound();

            return View(movement);
        }


        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveConfirmed(
            int id)
        {
            var movement =
                db.ResidenceMovements.Find(id);

            if (movement == null)
                return HttpNotFound();

            movement.IsArchived = true;

            db.SaveChanges();

            TempData["Success"] =
                "Movement archived.";

            return RedirectToAction(
                "Index");
        }


        // ============================================================
        // RESTORE
        // ============================================================

        [AdminOnly]
        public ActionResult Restore(int id)
        {
            var movement =
                db.ResidenceMovements.Find(id);

            if (movement == null)
                return HttpNotFound();

            movement.IsArchived = false;

            db.SaveChanges();

            TempData["Success"] =
                "Movement restored.";

            return RedirectToAction(
                "Index",
                new
                {
                    archived = true
                });
        }


        // ============================================================
        // DESTINATION LISTS
        // ============================================================

        private void PopulateDestinationLists(
            ResidenceMovement selected = null)
        {
            ViewBag.Residences =
                new SelectList(
                    db.Residences
                        .Where(r =>
                            !r.IsArchived)
                        .OrderBy(r => r.Name)
                        .ToList(),
                    "ResidenceId",
                    "Name",
                    selected?.ToResidenceId
                );

            ViewBag.Rooms =
                new SelectList(
                    db.Rooms
                        .Where(r =>
                            !r.IsArchived &&
                            !r.Residence.IsArchived &&
                            r.OccupiedBeds < r.Capacity)
                        .OrderBy(r => r.RoomNumber)
                        .ToList(),
                    "RoomId",
                    "RoomNumber",
                    selected?.ToRoomId
                );

            ViewBag.Beds =
                new SelectList(
                    db.Beds
                        .Where(b =>
                            !b.IsArchived &&
                            !b.IsOccupied &&
                            (b.Status == null ||
                             b.Status == "Available"))
                        .OrderBy(b => b.BedNumber)
                        .ToList(),
                    "BedId",
                    "BedNumber",
                    selected?.ToBedId
                );
        }


        // ============================================================
        // DISPOSE
        // ============================================================

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
