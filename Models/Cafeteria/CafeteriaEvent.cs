using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — A scheduled event (buffet, dinner, tea, braai, etc.)
    //
    // Parent/child: a Master event (Founders' Day) can have
    // sub-events (Pre-drinks, Lunch, Tea). Sub-events set
    // ParentEventId to the master's Id.
    // ============================================================
    public class CafeteriaEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string EventName { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [Required]
        public EventType EventType { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        // ── Venue ────────────────────────────────────────────────
        // Optional: not every event (e.g. a holiday meal) has one
        [ForeignKey("Venue")]
        public int? VenueId { get; set; }
        public virtual EventVenue Venue { get; set; }

        // ── Headcounts ───────────────────────────────────────────
        // Expected   : what the coordinator guesses at scheduling time
        // Guaranteed : firmed up after RSVPs close (UC18 → UC19)
        public int ExpectedHeadcount { get; set; }
        public int? GuaranteedHeadcount { get; set; }

        // ── Status ───────────────────────────────────────────────
        [Required]
        public EventStatus Status { get; set; }

        // ── Menu ─────────────────────────────────────────────────
        // Which EventMenuTemplate is being used (Sports Day Braai, etc.)
        // Optional — coordinator can also pick dishes manually.
        [ForeignKey("MenuTemplate")]
        public int? MenuTemplateId { get; set; }
        public virtual EventMenuTemplate MenuTemplate { get; set; }

        // ── Free text ────────────────────────────────────────────
        [StringLength(1000)]
        public string SpecialInstructions { get; set; }

        [StringLength(1000)]
        public string InternalNotes { get; set; }

        // ── Ownership ────────────────────────────────────────────
        // Who is running the event (a Coordinator / CafeteriaManager user)
        public int? AssignedToUserId { get; set; }

        [Required]
        public int CreatedByUserId { get; set; }

        // ── Master / Sub-event hierarchy ─────────────────────────
        // A master event (Founders' Day) has IsMaster = true and
        // ParentEventId = null. Its sub-events have IsMaster = false
        // and ParentEventId = the master's Id.
        public bool IsMaster { get; set; }

        [ForeignKey("ParentEvent")]
        public int? ParentEventId { get; set; }
        public virtual CafeteriaEvent ParentEvent { get; set; }
        public virtual ICollection<CafeteriaEvent> SubEvents { get; set; }

        // ── Staff ────────────────────────────────────────────────
        public virtual ICollection<EventStaffAssignment> StaffAssignments { get; set; }

        // ── RSVPs (UC18) ─────────────────────────────────────────
        public virtual ICollection<EventRsvp> Rsvps { get; set; }

        // ── Buffet meals (from the Dietitian's meal library) ─────
        public virtual ICollection<CafeteriaEventMenuItem> BuffetItems { get; set; }

        // Attendees pick one of the buffet meals on their RSVP;
        // otherwise everyone helps themselves from the buffet
        public bool OffersMealChoice { get; set; }

        // Parents and staff may bring up to this many guests each
        public bool AllowGuests { get; set; }
        public int MaxGuestsPerInvitee { get; set; }

        // ── Audit ────────────────────────────────────────────────

        // ── RSVP lifecycle (UC18) ────────────────────────────────
        // ── Audience (who gets invited) ──────────────────────────
        public bool InviteParents { get; set; }
        public bool InviteStaff { get; set; }
        public bool InviteStudents { get; set; }

        // If set, restrict parent invitations to these houses.
        // CSV of ResidenceIds, e.g. "1,3". Null or empty = all parents.
        [StringLength(200)]
        public string InviteParentResidenceIds { get; set; }

        // If set, only students living in these houses are invited.
        // CSV of ResidenceIds. Null or empty = all registered students.
        [StringLength(200)]
        public string InviteStudentResidenceIds { get; set; }

        // When RSVPs open automatically (school time). Null = only
        // when the manager opens them. Separate from EventDate.
        public DateTime? RsvpOpensAt { get; set; }

        public DateTime? RsvpDeadline { get; set; }
        public DateTime? RsvpOpenedAt { get; set; }
        public DateTime? RsvpClosedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public CafeteriaEvent()
        {
            Status = EventStatus.Draft;
            SubEvents = new HashSet<CafeteriaEvent>();
            StaffAssignments = new HashSet<EventStaffAssignment>();
            Rsvps = new HashSet<EventRsvp>();
            BuffetItems = new HashSet<CafeteriaEventMenuItem>();
            CreatedAt = DateTime.UtcNow;
        }
    }
}