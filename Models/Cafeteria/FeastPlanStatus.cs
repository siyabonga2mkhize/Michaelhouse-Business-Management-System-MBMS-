namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC19 — Lifecycle of a feast plan (EventFeastPlan).
    //   Proposed         : generated from the RSVPs (step 4); the
    //                      Meal Coordinator reviews it (step 5)
    //   AwaitingApproval : sent to the Cafeteria Manager (step 6)
    //   ChangesRequested : the Cafeteria Manager asked for changes;
    //                      the coordinator edits and recalculates,
    //                      then reviews again (step 7 → step 5)
    //   Approved         : saved, locked and sent to the Cafeteria
    //                      Manager and kitchen (step 8)
    //   Rejected         : turned down; no longer the event's live
    //                      plan, so a new one may be generated
    // ============================================================
    public enum FeastPlanStatus
    {
        Proposed = 0,
        AwaitingApproval = 1,
        ChangesRequested = 2,
        Approved = 3,
        Rejected = 4
    }
}
