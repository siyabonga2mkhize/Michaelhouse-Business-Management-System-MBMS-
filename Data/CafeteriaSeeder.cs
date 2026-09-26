using System;
using System.Linq;
using Michaelhouse.Models;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Data
{
    public static class CafeteriaSeeder
    {
        public static void Seed(DBContextClass db)
        {
            SeedSports(db);
            SeedMealSlots(db);
            SeedDiningHalls(db);
            SeedAllergens(db);
            SeedDietaryCategories(db);
            SeedRecipes(db);
            SeedMenuItems(db);
            SeedSportsMealRules(db);
            SeedSettings(db);
        }

        private static void SeedSports(DBContextClass db)
        {
            if (db.Sports.Any()) return;
            db.Sports.AddRange(new[]
            {
                new Sport { Name = "Rugby", Season = "Winter", Description = "Rugby Union" },
                new Sport { Name = "Hockey", Season = "Winter", Description = "Field Hockey" },
                new Sport { Name = "Cricket", Season = "Summer", Description = "Cricket" },
                new Sport { Name = "Swimming", Season = "Year-round", Description = "Swimming squad" },
                new Sport { Name = "Athletics", Season = "Summer", Description = "Track & field" },
                new Sport { Name = "Basketball", Season = "Winter", Description = "Basketball" },
                new Sport { Name = "Water Polo", Season = "Summer", Description = "Water Polo" },
                new Sport { Name = "Squash", Season = "Year-round", Description = "Squash" },
                new Sport { Name = "Tennis", Season = "Year-round", Description = "Tennis" },
                new Sport { Name = "Cross Country", Season = "Winter", Description = "Cross Country running" },
                new Sport { Name = "Canoeing", Season = "Summer", Description = "Canoeing / Kayaking" },
                new Sport { Name = "Golf", Season = "Year-round", Description = "Golf" }
            });
            db.SaveChanges();
        }

        private static void SeedMealSlots(DBContextClass db)
        {
            if (db.MealSlots.Any()) return;
            db.MealSlots.AddRange(new[]
            {
                new MealSlot { Name = "Breakfast",     DisplayOrder = 1, DefaultStartTime = new TimeSpan(6,30,0),  DefaultEndTime = new TimeSpan(8,0,0),   IsSportRelevant = true },
                new MealSlot { Name = "Morning Snack", DisplayOrder = 2, DefaultStartTime = new TimeSpan(10,0,0),  DefaultEndTime = new TimeSpan(10,30,0) },
                new MealSlot { Name = "Lunch",         DisplayOrder = 3, DefaultStartTime = new TimeSpan(12,30,0), DefaultEndTime = new TimeSpan(14,0,0),  IsSportRelevant = true },
                new MealSlot { Name = "Afternoon Tea", DisplayOrder = 4, DefaultStartTime = new TimeSpan(15,30,0), DefaultEndTime = new TimeSpan(16,0,0),  IsSportRelevant = true },
                new MealSlot { Name = "Dinner",        DisplayOrder = 5, DefaultStartTime = new TimeSpan(18,0,0),  DefaultEndTime = new TimeSpan(19,30,0), IsSportRelevant = true },
                new MealSlot { Name = "Sunday Brunch", DisplayOrder = 6, DefaultStartTime = new TimeSpan(10,30,0), DefaultEndTime = new TimeSpan(12,0,0) },
                new MealSlot { Name = "Sports Meal",   DisplayOrder = 7, IsSportRelevant = true },
                new MealSlot { Name = "Event Meal",    DisplayOrder = 8 }
            });
            db.SaveChanges();
        }

        private static void SeedDiningHalls(DBContextClass db)
        {
            if (db.DiningHalls.Any()) return;
            db.DiningHalls.AddRange(new[]
            {
                new DiningHall { Name = "Dining Hall A", Location = "Main Campus", Capacity = 400 },
                new DiningHall { Name = "Dining Hall B", Location = "Main Campus", Capacity = 250 }
            });
            db.SaveChanges();
        }

        private static void SeedAllergens(DBContextClass db)
        {
            if (db.Allergens.Any()) return;
            db.Allergens.AddRange(new[]
            {
                new Allergen { Name = "Peanuts" },
                new Allergen { Name = "Tree Nuts" },
                new Allergen { Name = "Milk / Lactose" },
                new Allergen { Name = "Eggs" },
                new Allergen { Name = "Wheat / Gluten" },
                new Allergen { Name = "Soy" },
                new Allergen { Name = "Fish" },
                new Allergen { Name = "Shellfish" },
                new Allergen { Name = "Sesame" }
            });
            db.SaveChanges();
        }

        private static void SeedDietaryCategories(DBContextClass db)
        {
            if (db.DietaryCategories.Any()) return;
            db.DietaryCategories.AddRange(new[]
            {
                new DietaryCategory { Name = "Vegetarian" },
                new DietaryCategory { Name = "Vegan" },
                new DietaryCategory { Name = "Gluten-Free" },
                new DietaryCategory { Name = "Lactose-Free" },
                new DietaryCategory { Name = "Halal" },
                new DietaryCategory { Name = "Kosher" },
                new DietaryCategory { Name = "Diabetic-Friendly" }
            });
            db.SaveChanges();
        }

        private static void SeedRecipes(DBContextClass db)
        {
            if (db.Recipes.Any()) return;
            var names = new[]
            {
                "Chicken & Rice", "Beef Stew", "Fish & Chips", "Vegetarian Curry",
                "Pasta & Meatballs", "Roast Chicken", "Chicken Schnitzel", "Lamb Curry",
                "Beef Burger & Chips", "Vegetable Stir-fry",
                "Eggs & Toast", "Cereal & Milk", "Oats & Fruit", "Pancakes", "Omelette",
                "Fruit Salad", "Ice Cream", "Malva Pudding", "Trifle",
                "Cheese & Crackers", "Yogurt & Granola", "Protein Shake", "Fruit & Nuts"
            };
            foreach (var n in names)
                db.Recipes.Add(new Recipe { Name = n, YieldUnit = "portions", ServingYield = 1, IsActive = true });
            db.SaveChanges();
        }

        private static void SeedMenuItems(DBContextClass db)
        {
            if (db.MenuItems.Any()) return;
            db.MenuItems.AddRange(new[]
            {
                new MenuItem { Name = "Grilled Chicken",       Category = "Protein",   PortionDescription = "150g" },
                new MenuItem { Name = "Beef Stew",             Category = "Protein" },
                new MenuItem { Name = "Fish Fillets",          Category = "Protein" },
                new MenuItem { Name = "Scrambled Eggs",        Category = "Protein",   IsVegetarian = true },
                new MenuItem { Name = "Boiled Eggs",           Category = "Protein",   IsVegetarian = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Vegetarian Bean Curry", Category = "Protein",   IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Tofu Stir Fry",         Category = "Protein",   IsVegetarian = true, IsVegan = true },
                new MenuItem { Name = "White Rice",            Category = "Starch",    IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Brown Rice",            Category = "Starch",    IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Mielie Pap",            Category = "Starch",    IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Roast Potatoes",        Category = "Starch",    IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Pasta",                 Category = "Starch",    IsVegetarian = true, IsVegan = true },
                new MenuItem { Name = "Brown Bread",           Category = "Bread",     IsVegetarian = true },
                new MenuItem { Name = "White Bread",           Category = "Bread",     IsVegetarian = true },
                new MenuItem { Name = "Mixed Vegetables",      Category = "Vegetable", IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Green Salad",           Category = "Vegetable", IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Steamed Broccoli",      Category = "Vegetable", IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Fresh Fruit",           Category = "Fruit",     IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Bananas",               Category = "Fruit",     IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Yoghurt",               Category = "Dairy",     IsVegetarian = true },
                new MenuItem { Name = "Milk",                  Category = "Dairy",     IsVegetarian = true, IsGlutenFree = true },
                new MenuItem { Name = "Porridge",              Category = "Breakfast", IsVegetarian = true },
                new MenuItem { Name = "Cereal",                Category = "Breakfast", IsVegetarian = true },
                new MenuItem { Name = "Orange Juice",          Category = "Drink",     IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Water",                 Category = "Drink",     IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Tea",                   Category = "Drink",     IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true },
                new MenuItem { Name = "Coffee",                Category = "Drink",     IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true }
            });
            db.SaveChanges();
        }

        private static void SeedSportsMealRules(DBContextClass db)
        {
            if (db.SportsMealRecommendationRules.Any()) return;

            var breakfast = db.MealSlots.FirstOrDefault(m => m.Name == "Breakfast");
            var lunch = db.MealSlots.FirstOrDefault(m => m.Name == "Lunch");
            var afternoonTea = db.MealSlots.FirstOrDefault(m => m.Name == "Afternoon Tea");
            var dinner = db.MealSlots.FirstOrDefault(m => m.Name == "Dinner");

            db.SportsMealRecommendationRules.AddRange(new[]
            {
                new SportsMealRecommendationRule { ActivityType = SportsActivityType.Match,    MealSlotId = breakfast?.Id,    RecommendedMealType = "High-energy pre-match breakfast", Description = "Pre-match breakfast before morning matches" },
                new SportsMealRecommendationRule { ActivityType = SportsActivityType.Match,    MealSlotId = lunch?.Id,        RecommendedMealType = "Post-match recovery meal",        Description = "High-protein lunch after a match" },
                new SportsMealRecommendationRule { ActivityType = SportsActivityType.Training, MealSlotId = afternoonTea?.Id, RecommendedMealType = "Energy snack",                    Description = "Snack before/after training" },
                new SportsMealRecommendationRule { ActivityType = SportsActivityType.Training, MealSlotId = dinner?.Id,       RecommendedMealType = "High-protein dinner",             Description = "Recovery meal after evening training" }
            });
            db.SaveChanges();
        }

        private static void SeedSettings(DBContextClass db)
        {
            if (db.CafeteriaSettings.Any()) return;
            db.CafeteriaSettings.AddRange(new[]
            {
                new CafeteriaSetting { Key = "BreakfastCutoff",     Value = "05:00",           Description = "Breakfast selection cutoff (HH:mm)" },
                new CafeteriaSetting { Key = "LunchCutoff",         Value = "10:30",           Description = "Lunch selection cutoff (HH:mm)" },
                new CafeteriaSetting { Key = "DinnerCutoff",        Value = "16:00",           Description = "Dinner selection cutoff (HH:mm)" },
                new CafeteriaSetting { Key = "CoachDeadline",       Value = "Thursday 12:00",  Description = "Weekly coach requirement deadline" },
                new CafeteriaSetting { Key = "DemandBufferPercent", Value = "5",               Description = "Buffer % added to forecast demand" },
                new CafeteriaSetting { Key = "DefaultFallbackMealId", Value = "",              Description = "MenuItem Id used when no selection made" }
            });
            db.SaveChanges();
        }
    }
}