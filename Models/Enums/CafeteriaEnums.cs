using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.Enums
{
    public enum WeeklyMenuStatus
    {
        Draft = 0, Planning = 1, ValidationRequired = 2, CoordinatorReview = 3,
        ChefReview = 4, ChangesRequired = 5, ReadyForApproval = 6,
        Approved = 7, Published = 8, Locked = 9, Completed = 10, Archived = 11
    }

    public enum SportsActivityType
    {
        Training = 0, Match = 1, Tournament = 2, Competition = 3,
        Travel = 4, RecoverySession = 5, Other = 6
    }

    public enum SportParticipationStatus
    {
        NoSport = 0, Pending = 1, Trial = 2, Participant = 3, Inactive = 4
    }

    public enum TeamMembershipStatus
    {
        Pending = 0, Active = 1, Inactive = 2, Rejected = 3, Left = 4
    }

    public enum DietaryRecordType
    {
        Allergy = 0, MedicalRestriction = 1, DietaryPreference = 2,
        ReligiousCultural = 3, Dislike = 4
    }

    public enum DietaryRequestStatus
    {
        Pending = 0, UnderReview = 1, Approved = 2, Rejected = 3, Withdrawn = 4
    }

    public enum AllergenPresenceType
    {
        Contains = 0, MayContain = 1, PreparedWith = 2, CrossContact = 3
    }

    public enum MenuMealComponentType
    {
        Main = 0, Alternative = 1, Protein = 2, Side = 3,
        Drink = 4, Dessert = 5, Starter = 6, Condiment = 7, Fruit = 8, Bread = 9
    }

    public enum MealServiceType
    {
        Normal = 0, PackedMeal = 1, TravelMeal = 2, EarlyMeal = 3,
        LateMeal = 4, SpecialMeal = 5, EventMeal = 6
    }

    public enum MealSelectionStatus
    {
        Selected = 0, Collected = 1, Served = 2, Cancelled = 3,
        NotCollected = 4, Blocked = 5, Fallback = 6
    }

    public enum MealPlanStatus
    {
        Proposed = 0, Confirmed = 1, NeedsReview = 2, Superseded = 3
    }

    public enum MealPlanItemStatus
    {
        Recommended = 0, Accepted = 1, Modified = 2, Rejected = 3, Unsafe = 4
    }

    public enum MealServiceMethod
    {
        QR = 0, Manual = 1, Fallback = 2
    }

    public enum CoachRequirementStatus
    {
        Submitted = 0, Accepted = 1, Modified = 2, Rejected = 3, Late = 4
    }

    public enum StockOrderStatus
    {
        Requested = 0, Approved = 1, Ordered = 2,
        PartiallyReceived = 3, Received = 4, Cancelled = 5
    }

    public enum StockOrderPriority
    {
        Low = 0, Normal = 1, High = 2, Critical = 3
    }

    public enum WasteReason
    {
        Overproduction = 0, Spoilage = 1, Expired = 2, Burnt = 3,
        Damaged = 4, Returned = 5, Other = 6
    }

    public enum ValidationSeverity
    {
        Info = 0, Warning = 1, Critical = 2
    }

    public enum ValidationCategory
    {
        Completeness = 0, DietarySafety = 1, Inventory = 2,
        Sports = 3, Events = 4, Kitchen = 5
    }

    public enum DietaryChangeType
    {
        Add = 0, Update = 1, Remove = 2, SportsChange = 3
    }
}