namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC17 — Roles a staff member can take at an event.
    // ============================================================
    public enum EventStaffRole
    {
        Coordinator = 1,
        HeadChef = 2,
        Chef = 3,
        KitchenHand = 4,
        Server = 5,
        Cleanup = 6,
        Other = 99
    }
}