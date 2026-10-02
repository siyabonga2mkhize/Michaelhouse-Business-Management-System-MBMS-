using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC19 — Generate Feast Plan
    //
    // A plan of dish quantities for a scheduled event, worked out
    // from its accepted RSVPs (CafeteriaEvent + EventRsvp, read
    // through EventRsvpService.GetAttendees). Reviewed by the Meal
    // Coordinator, approved by the Cafeteria Manager and then sent
    // to the kitchen.
    //
    // One live plan per event: every plan that isn't Rejected.
    // An Approved plan is locked.
    //
    // Separate from the kitchen view at Event/FeastPlan, which is
    // built on the fly when RSVPs close and is not stored.
    // ============================================================
    public class EventFeastPlan
    {
        public const decimal DefaultBufferPercent = 5m;

        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Event")]
        public int EventId { get; set; }
        public virtual CafeteriaEvent Event { get; set; }

        [Required]
        public FeastPlanStatus Status { get; set; }

        // Goes up each time the plan is sent back for approval
        public int Revision { get; set; }

        // ── Headcount from the accepted RSVPs ───────────────────
        // Standard = Total - Vegetarian - Halal
        public int TotalGuests { get; set; }
        public int VegetarianGuests { get; set; }   // includes vegans
        public int VeganGuests { get; set; }        // shown to the kitchen
        public int HalalGuests { get; set; }
        public int StandardGuests { get; set; }

        // "Other" dietary requirements — counted under Standard
        public int OtherRequirementGuests { get; set; }

        // Accepted RSVP responses the counts came from
        public int RsvpResponses { get; set; }

        // ── Buffer added to every dish, rounded up ──────────────
        [Range(0, 100)]
        public decimal BufferPercent { get; set; }

        // ── Review / approval ────────────────────────────────────
        [StringLength(1000)]
        public string CoordinatorNotes { get; set; }

        // The Cafeteria Manager's latest decision comment
        [StringLength(1000)]
        public string ManagerComments { get; set; }

        public int GeneratedByUserId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime CalculatedAt { get; set; }

        public int? SubmittedByUserId { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public int? DecidedByUserId { get; set; }
        public DateTime? DecidedAt { get; set; }

        // When the approved plan was sent to the manager and kitchen
        public DateTime? SentAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Stops two people approving / changing the plan at once
        [Timestamp]
        public byte[] RowVersion { get; set; }

        public virtual ICollection<EventFeastPlanItem> Items { get; set; }
        public virtual ICollection<EventFeastPlanHistory> History { get; set; }

        [NotMapped]
        public bool IsLocked
        {
            get { return Status == FeastPlanStatus.Approved; }
        }

        [NotMapped]
        public bool IsEditable
        {
            get { return Status == FeastPlanStatus.Proposed || Status == FeastPlanStatus.ChangesRequested; }
        }

        public EventFeastPlan()
        {
            Status = FeastPlanStatus.Proposed;
            Revision = 1;
            BufferPercent = DefaultBufferPercent;
            GeneratedAt = DateTime.UtcNow;
            CalculatedAt = DateTime.UtcNow;
            Items = new HashSet<EventFeastPlanItem>();
            History = new HashSet<EventFeastPlanHistory>();
        }
    }
}
