namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC12 rework — Sports nutrition archetypes.
    //
    // Every sport falls into one of four nutrition profiles.
    // The generator uses this to pick items that fit each group.
    // ============================================================

    public enum SportArchetype
    {
        None = 0,

        /// <summary>
        /// Rugby, Water Polo, Hockey, Basketball.
        /// High protein, high carb. Match-day recovery focus.
        /// </summary>
        Power = 1,

        /// <summary>
        /// Athletics, Swimming, Cross Country, Cycling.
        /// High carb throughout. Carb loading is important.
        /// </summary>
        Endurance = 2,

        /// <summary>
        /// Cricket, Tennis, Squash.
        /// Balanced. Lighter meals on match day.
        /// </summary>
        Skill = 3,

        /// <summary>
        /// Sprinting, Sprint Swimming.
        /// Quick energy, hydration focus.
        /// </summary>
        Speed = 4
    }
}