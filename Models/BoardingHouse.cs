using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// A Residence (boarding house) containing rooms and beds. Holds aggregate occupancy metrics used by the AI allocation engine.
    /// </summary>
    public class Residence
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int ResidenceId { get; set; }

        /// <summary>
        /// Display name of the residence (e.g. Founders House)
        /// </summary>
        [Required, Display(Name = "House Name")]
        [StringLength(100)]
        public string Name { get; set; }

        [NotMapped]
        public string ResidenceName
        {
            get { return Name; }
            set { Name = value; }
        }

        [StringLength(20)]
        public string Gender { get; set; }

        /// <summary>
        /// Total capacity across all rooms (computed by operations but writable for seeding)
        /// </summary>
        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        /// <summary>
        /// Current aggregate occupied beds
        /// </summary>
        [Display(Name = "Occupied Beds")]
        public int OccupiedBeds { get; set; }

        [NotMapped]
        public int CurrentOccupancy
        {
            get { return OccupiedBeds; }
            set { OccupiedBeds = value; }
        }

        /// <summary>
        /// Foreign key to the house master
        /// </summary>
        public int? HouseMasterId { get; set; }

        /// <summary>
        /// Computed available beds (Capacity - OccupiedBeds)
        /// </summary>
        [Display(Name = "Available Beds")]
        public int AvailableBeds { get { return Capacity - OccupiedBeds; } }

        /// <summary>
        /// Optional categorisation used by allocation rules (Junior/Middle/Senior)
        /// </summary>
        public string GradeCategory { get; set; }

        /// <summary>
        /// Flags used by the scoring engine
        /// </summary>
        public bool NearMedicalFacility { get; set; }
        public bool NearHouseMasterOffice { get; set; }

        /// <summary>
        /// Navigation: rooms belonging to this residence
        /// </summary>
        public virtual ICollection<Room> Rooms { get; set; }

        /// <summary>
        /// Navigation: house master for this residence
        /// </summary>
        public virtual HouseMaster HouseMaster { get; set; }
        public bool IsArchived { get; internal set; }
        public DateTime? ArchivedDate { get; set; }
        public string ArchivedBy { get; internal set; }

        [NotMapped]
        public bool IsActive
        {
            get { return !IsArchived; }
            set { IsArchived = !value; }
        }
    }

    /// <summary>
    /// A room within a residence. Contains beds and occupancy metadata.
    /// </summary>
    public class Room
    {
        public int RoomId { get; set; }

        [Display(Name = "Room Number")]
        [StringLength(50)]
        public string RoomNumber { get; set; }

        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Occupied Beds")]
        public int OccupiedBeds { get; set; }

        [Display(Name = "Available Beds")]
        public int AvailableBeds { get { return Capacity - OccupiedBeds; } }

        /// <summary>
        /// Indicates whether room is currently at or above capacity
        /// </summary>
        public bool IsFull { get; set; }

        [Display(Name = "Floor")]
        public int Floor { get; set; }

        // Accessibility and suitability flags
        public bool IsGroundFloor { get; set; }
        public bool IsWheelchairAccessible { get; set; }
        public bool NearBathroom { get; set; }
        public bool IsQuietStudyRoom { get; set; }
        public bool NeedsMaintenance { get; set; }

        /// <summary>
        /// Foreign key to parent residence
        /// </summary>
        public int ResidenceId { get; set; }

        /// <summary>
        /// Navigation: parent residence
        /// </summary>
        public virtual Residence Residence { get; set; }

        /// <summary>
        /// Navigation: beds in this room
        /// </summary>
        public virtual ICollection<Bed> Beds { get; set; }
        public bool IsArchived { get; internal set; }

        [NotMapped]
        public bool IsActive
        {
            get { return !IsArchived; }
            set { IsArchived = !value; }
        }
    }

    /// <summary>
    /// A physical bed inside a room. Tracks occupancy and optional student link.
    /// </summary>
    public class Bed
    {
        public int BedId { get; set; }

        [NotMapped]
        public int BedSpaceId
        {
            get { return BedId; }
            set { BedId = value; }
        }

        /// <summary>
        /// Parent room
        /// </summary>
        public int RoomId { get; set; }

        [StringLength(50)]
        public string BedNumber { get; set; }

        /// <summary>
        /// Is this bed currently occupied
        /// </summary>
        public bool IsOccupied { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Available";

        /// <summary>
        /// Optional student currently assigned to this bed
        /// </summary>
        public int? OccupiedByStudentId { get; set; }

        public virtual Room Room { get; set; }

        [ForeignKey("OccupiedByStudentId")]
        public virtual Student OccupiedByStudent { get; set; }

        public bool IsArchived { get; set; }
    }

    public class RoomScoreAudit
    {
        public int RoomScoreAuditId { get; set; }
        public int RoomId { get; set; }
        public int ResidenceId { get; set; }
        public int StudentId { get; set; }
        public double Score { get; set; }
        public System.DateTime CalculatedAt { get; set; }
        public string Details { get; set; }
    }

    public class ResidenceAssignment
    {
        public int ResidenceAssignmentId { get; set; }

        [NotMapped]
        public int StudentResidenceId
        {
            get { return ResidenceAssignmentId; }
            set { ResidenceAssignmentId = value; }
        }

        public int StudentId { get; set; }
        public int ResidenceId { get; set; }
        public int RoomId { get; set; }
        public int BedId { get; set; }
        public bool IsActive { get; set; }
        public System.DateTime MoveInDate { get; set; }
        public System.DateTime? VacatedDate { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        [NotMapped]
        public System.DateTime AllocatedDate
        {
            get { return MoveInDate; }
            set { MoveInDate = value; }
        }

        public virtual Student Student { get; set; }
        public virtual Residence Residence { get; set; }
        public virtual Room Room { get; set; }
        public virtual Bed Bed { get; set; }

        public bool IsArchived { get; set; }
    }

    public class DisciplinaryConflict
    {
        public int DisciplinaryConflictId { get; set; }
        public int StudentAId { get; set; }
        public int StudentBId { get; set; }
        public string Reason { get; set; }
        public System.DateTime ReportedAt { get; set; }
    }
}
