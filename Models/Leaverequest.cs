using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Status values for a LeaveRequest. Kept as string constants (not an enum)
    /// to match the string-status convention already used elsewhere in this
    /// codebase (e.g. ResidenceAssignment.Status).
    /// </summary>
    public static class LeaveRequestStatus
    {
        public const string PendingParentApproval = "PendingParentApproval";
        public const string PendingHouseMasterApproval = "PendingHouseMasterApproval";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    /// <summary>
    /// Use Case 27 - Request Permission to Leave Residence.
    /// A student-initiated request that must be approved by the linked
    /// Parent/Guardian and then by the House Master of the residence the
    /// student is currently allocated to.
    /// </summary>
    public class LeaveRequest
    {
        [Key]
        public int LeaveRequestId { get; set; }

        // ─── Who is leaving ───────────────────────────────────────────────
        [Required]
        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        // ─── Resolved automatically at submission time ───────────────────
        // The parent linked to the student (Student.ParentId -> Parent).
        [Required]
        public int ParentId { get; set; }
        public virtual Parent Parent { get; set; }

        // The residence the student is currently (actively) assigned to,
        // and that residence's house master. Both are resolved server-side
        // from the student's active ResidenceAssignment - never trust a
        // client-submitted HouseMasterId/ResidenceId for this.
        public int? ResidenceId { get; set; }
        public virtual Residence Residence { get; set; }

        public int? HouseMasterId { get; set; }
        public virtual HouseMaster HouseMaster { get; set; }

        // ─── Request details (entered by the student) ────────────────────
        [Required, StringLength(300)]
        [Display(Name = "Destination")]
        public string Destination { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Departure Date & Time")]
        public DateTime DepartureDateTime { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Expected Return Date & Time")]
        public DateTime ExpectedReturnDateTime { get; set; }

        [Required, StringLength(500)]
        [Display(Name = "Reason for Leaving")]
        public string Reason { get; set; }

        // ─── Workflow state ────────────────────────────────────────────────
        [Required, StringLength(40)]
        public string Status { get; set; } = LeaveRequestStatus.PendingParentApproval;

        // ─── Parent/Guardian decision ──────────────────────────────────────
        [StringLength(20)]
        public string ParentDecision { get; set; } // "Approved" / "Rejected"

        [StringLength(500)]
        [Display(Name = "Parent/Guardian Comments")]
        public string ParentComments { get; set; }

        public DateTime? ParentDecisionAt { get; set; }

        // ─── House Master decision ─────────────────────────────────────────
        [StringLength(20)]
        public string HouseMasterDecision { get; set; } // "Approved" / "Rejected"

        [StringLength(500)]
        [Display(Name = "House Master Comments")]
        public string HouseMasterComments { get; set; }

        public DateTime? HouseMasterDecisionAt { get; set; }

        // ─── Audit ──────────────────────────────────────────────────────────
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        public bool IsArchived { get; set; }

        // ─── Convenience (not mapped) ──────────────────────────────────────
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string StatusLabel
        {
            get
            {
                switch (Status)
                {
                    case LeaveRequestStatus.PendingParentApproval: return "Pending Parent/Guardian Approval";
                    case LeaveRequestStatus.PendingHouseMasterApproval: return "Pending House Master Approval";
                    case LeaveRequestStatus.Approved: return "Approved";
                    case LeaveRequestStatus.Rejected: return "Rejected";
                    default: return Status;
                }
            }
        }
    }
}