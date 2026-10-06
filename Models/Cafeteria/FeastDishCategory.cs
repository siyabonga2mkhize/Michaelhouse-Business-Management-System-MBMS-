namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC19 — Which guests a dish on a feast plan is prepared for.
    //   Standard   : guests without a vegetarian / halal requirement
    //   Vegetarian : vegetarian (and vegan) guests, from the RSVPs
    //   Halal      : halal guests, from the RSVPs
    //   Common     : served to everyone (dessert, sides, drinks …)
    // ============================================================
    public enum FeastDishCategory
    {
        Standard = 0,
        Vegetarian = 1,
        Halal = 2,
        Common = 3,
        // A guest's cultural favourite added by the Meal Coordinator (step 3)
        Favourite = 4
    }
}
