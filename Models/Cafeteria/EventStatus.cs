namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — Lifecycle of an event.
    //   Draft              : being composed, not yet visible
    //   Confirmed          : saved and locked to the calendar
    //   RsvpOpen           : invitations sent (UC18 running)
    //   RsvpClosed         : headcount frozen
    //   FeastPlanGenerated : UC19 has produced the kitchen plan
    //   InProgress         : day of event
    //   Completed          : event is over
    //   Cancelled          : called off (soft, kept for audit)
    // ============================================================
    public enum EventStatus
    {
        Draft = 0,
        Confirmed = 1,
        RsvpOpen = 2,
        RsvpClosed = 3,
        FeastPlanGenerated = 4,
        InProgress = 5,
        Completed = 6,
        Cancelled = 7
    }
}