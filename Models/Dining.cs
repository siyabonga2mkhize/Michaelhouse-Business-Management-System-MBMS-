using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    public class DiningHall
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(80)] public string Name { get; set; }
        [StringLength(200)] public string Location { get; set; }
        public int? Capacity { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class MealSlot
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(80)] public string Name { get; set; }
        public int DisplayOrder { get; set; }
        public TimeSpan? DefaultStartTime { get; set; }
        public TimeSpan? DefaultEndTime { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSportRelevant { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class Allergen
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(80)] public string Name { get; set; }
        [StringLength(250)] public string Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class DietaryCategory
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(80)] public string Name { get; set; }
        [StringLength(250)] public string Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class MenuItem
    {
        [Key] public int Id { get; set; }
        [Required, StringLength(120)] public string Name { get; set; }
        [StringLength(500)] public string Description { get; set; }
        [StringLength(80)] public string Category { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsVegetarian { get; set; }
        public bool IsVegan { get; set; }
        public bool IsGlutenFree { get; set; }
        public bool IsLactoseFree { get; set; }
        public bool HasIncompleteAllergenInfo { get; set; }

        [StringLength(120)] public string PortionDescription { get; set; }
        [StringLength(500)] public string PreparationNotes { get; set; }
        [StringLength(500)] public string CrossContactWarning { get; set; }

        public int? RecipeId { get; set; }
        [ForeignKey("RecipeId")] public virtual Recipe Recipe { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<MenuItemAllergen> Allergens { get; set; } = new List<MenuItemAllergen>();
        public virtual ICollection<MenuItemDietaryTag> DietaryTags { get; set; } = new List<MenuItemDietaryTag>();
    }

    public class MenuItemAllergen
    {
        [Key] public int Id { get; set; }
        public int MenuItemId { get; set; }
        [ForeignKey("MenuItemId")] public virtual MenuItem MenuItem { get; set; }
        public int AllergenId { get; set; }
        [ForeignKey("AllergenId")] public virtual Allergen Allergen { get; set; }
        public AllergenPresenceType PresenceType { get; set; } = AllergenPresenceType.Contains;
    }

    public class MenuItemDietaryTag
    {
        [Key] public int Id { get; set; }
        public int MenuItemId { get; set; }
        [ForeignKey("MenuItemId")] public virtual MenuItem MenuItem { get; set; }
        public int DietaryCategoryId { get; set; }
        [ForeignKey("DietaryCategoryId")] public virtual DietaryCategory DietaryCategory { get; set; }
    }

    public class MenuDay
    {
        [Key] public int Id { get; set; }
        public int WeeklyMenuId { get; set; }
        [ForeignKey("WeeklyMenuId")] public virtual WeeklyMenu WeeklyMenu { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public DateTime Date { get; set; }
        public virtual ICollection<MenuMeal> Meals { get; set; } = new List<MenuMeal>();
    }

    public class MenuMeal
    {
        [Key] public int Id { get; set; }
        public int MenuDayId { get; set; }
        [ForeignKey("MenuDayId")] public virtual MenuDay MenuDay { get; set; }
        public int MealSlotId { get; set; }
        [ForeignKey("MealSlotId")] public virtual MealSlot MealSlot { get; set; }

        [StringLength(150)] public string Title { get; set; }
        [StringLength(1000)] public string Notes { get; set; }
        public MealServiceType ServiceType { get; set; } = MealServiceType.Normal;
        public bool IsSpecialMeal { get; set; }

        public bool AvailableAtHallA { get; set; } = true;
        public bool AvailableAtHallB { get; set; } = true;

        public int? ExpectedCount { get; set; }
        public int? ProductionTarget { get; set; }

        public virtual ICollection<MenuMealComponent> Components { get; set; } = new List<MenuMealComponent>();
        public virtual ICollection<MenuMealAlternative> Alternatives { get; set; } = new List<MenuMealAlternative>();
    }

    public class MenuMealComponent
    {
        [Key] public int Id { get; set; }
        public int MenuMealId { get; set; }
        [ForeignKey("MenuMealId")] public virtual MenuMeal MenuMeal { get; set; }
        public int MenuItemId { get; set; }
        [ForeignKey("MenuItemId")] public virtual MenuItem MenuItem { get; set; }
        public MenuMealComponentType ComponentType { get; set; } = MenuMealComponentType.Main;
        public int DisplayOrder { get; set; }
        public bool IsDefault { get; set; } = true;
    }

    public class MenuMealAlternative
    {
        [Key] public int Id { get; set; }
        public int MenuMealId { get; set; }
        [ForeignKey("MenuMealId")] public virtual MenuMeal MenuMeal { get; set; }
        public int MenuItemId { get; set; }
        [ForeignKey("MenuItemId")] public virtual MenuItem MenuItem { get; set; }
        [StringLength(200)] public string Reason { get; set; }
        public bool IsDefaultAlternative { get; set; }
    }

    public class MenuValidationIssue
    {
        [Key] public int Id { get; set; }
        public int WeeklyMenuId { get; set; }
        [ForeignKey("WeeklyMenuId")] public virtual WeeklyMenu WeeklyMenu { get; set; }
        public ValidationSeverity Severity { get; set; }
        public ValidationCategory Category { get; set; }
        [Required, StringLength(500)] public string Message { get; set; }
        [StringLength(200)] public string RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public bool IsResolved { get; set; }
        public int? ResolvedByUserId { get; set; }
        public DateTime? ResolvedAt { get; set; }
        [StringLength(500)] public string ResolutionNotes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}