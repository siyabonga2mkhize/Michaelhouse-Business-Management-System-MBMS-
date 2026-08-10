namespace Michaelhouse.Services
{
    // Centralised, tunable weights — change these here, not scattered through the engine.
    public static class AllocationRuleService
    {
        public const int GradeLevelWeight = 40;
        public const int AgeCompatibilityMaxWeight = 20; // not explicitly weighted in spec; 20 chosen so age matters but doesn't dominate grade
        public const int SubjectCompatibilityWeight = 15;
        public const int SportsCompatibilityWeight = 10;
        public const int ClubsCompatibilityWeight = 5;
        public const int PreviousRoommateWeight = 15;
        public const int AccessibilityWeight = 30;
        public const int MedicalAccommodationWeight = 25;

        // Large enough to always outrank a legitimate score, without using
        // int.MinValue (which would break score display/sorting math).
        public const int DisciplinaryConflictPenalty = -1000;

        public const int MaxAgeDifferenceConsidered = 5; // beyond this, age score contributes 0
    }
}