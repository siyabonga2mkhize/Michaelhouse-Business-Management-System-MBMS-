using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public enum VisitorRequestStatus
    {
        PendingHousemaster,
        Approved,
        AutoApprovedWeekend,
        RejectedClosedWeekend,
        RejectedPolicyViolation,
        Cancelled
    }

    public enum VisitorAccessZone
    {
        [Display(Name = "Public Campus Grounds (Makan / Oval)")]
        PublicCampusGrounds,

        [Display(Name = "House Common Room (Parents/Guardians Only)")]
        HouseCommonRoom,

        [Display(Name = "Restricted House Bounds")]
        RestrictedHouseBounds
    }

    
    public class VisitorAccessRequest
    {
        [Key]
        public int RequestId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [Required, StringLength(50)]
        public string BoardingHouseName { get; set; }

        [Required, StringLength(100), Display(Name = "Visitor Full Name")]
        public string VisitorFullName { get; set; }

        [Required, StringLength(15), Display(Name = "Visitor Phone Number")]
        public string VisitorPhone { get; set; }

        [StringLength(13), Display(Name = "ID or Passport Number")]
        public string VisitorIdOrPassport { get; set; }

        [Required, StringLength(50), Display(Name = "Relationship to Student")]
        public string RelationshipToBoy { get; set; }

        [Display(Name = "Is this visitor a parent or guardian?")]
        public bool IsParentOrGuardian { get; set; }

        [Required, EmailAddress, StringLength(100), Display(Name = "Visitor Email Address")]
        public string VisitorEmail { get; set; }

        [Required, Display(Name = "Visit Date")]
        [DataType(DataType.Date)]
        public DateTime VisitDate { get; set; }

        [Required, Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public TimeSpan StartTime { get; set; }

        [Required, Display(Name = "End Time")]
        [DataType(DataType.Time)]
        public TimeSpan EndTime { get; set; }

        [Required, Display(Name = "Requested Access Zone")]
        public VisitorAccessZone RequestedZone { get; set; }

        [Display(Name = "Final Assigned Zone")]
        public VisitorAccessZone? FinalAssignedZone { get; set; }

        [Required, StringLength(250), Display(Name = "Purpose of Visit")]
        public string PurposeOfVisit { get; set; }

        public VisitorRequestStatus Status { get; set; } = VisitorRequestStatus.PendingHousemaster;

        public string HousemasterRemarks { get; set; }
        public int? ActionedByHousemasterId { get; set; }
        public DateTime? ActionedAt { get; set; }

        [Display(Name = "Access Gate Pass Code")]
        public string AccessGatePassCode { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // System Policy Evaluation Properties
        public bool HasPolicyConflict { get; set; } = false;
        public string PolicyFlagDetails { get; set; }
    }

    public class TermCalendar
    {
        [Key]
        public int CalendarId { get; set; }

        [Required, StringLength(100)]
        public string EventTitle { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsClosedWeekend { get; set; }
    }

    public class VisitorScanLog
    {
        [Key]
        public int ScanId { get; set; }

        public int RequestId { get; set; }
        [ForeignKey("RequestId")]
        public virtual VisitorAccessRequest Request { get; set; }

        public DateTime ScannedAt { get; set; }
        public int? ScannerUserId { get; set; }
        public string ScannerName { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public string Location { get; set; }
        public bool IsEntry { get; set; }
    }
}