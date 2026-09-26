USE master;
GO

-- Kick everyone off the DB
ALTER DATABASE MichaelHouse SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
GO

-- Delete it
DROP DATABASE MichaelHouse;
GO

SELECT 'MealSlots'                     AS TblName, COUNT(*) AS Cnt FROM MealSlots
UNION ALL SELECT 'DiningHalls',                     COUNT(*) FROM DiningHalls
UNION ALL SELECT 'Allergens',                       COUNT(*) FROM Allergens
UNION ALL SELECT 'DietaryCategories',               COUNT(*) FROM DietaryCategories
UNION ALL SELECT 'Sports',                          COUNT(*) FROM Sports
UNION ALL SELECT 'Recipes',                         COUNT(*) FROM Recipes
UNION ALL SELECT 'MenuItems',                       COUNT(*) FROM MenuItems
UNION ALL SELECT 'Coaches',                         COUNT(*) FROM Coaches
UNION ALL SELECT 'Teams',                           COUNT(*) FROM Teams
UNION ALL SELECT 'CafeteriaSettings',               COUNT(*) FROM CafeteriaSettings
UNION ALL SELECT 'SportsMealRecommendationRules',   COUNT(*) FROM SportsMealRecommendationRules
UNION ALL SELECT 'InventoryItems (cafeteria)',      COUNT(*) FROM InventoryItems
   WHERE Category IN ('Food','Beverage','Kitchen Supply');

   SELECT 'MealSlots'                     AS TblName, COUNT(*) AS Cnt FROM MealSlots
UNION ALL SELECT 'DiningHalls',                     COUNT(*) FROM DiningHalls
UNION ALL SELECT 'Allergens',                       COUNT(*) FROM Allergens
UNION ALL SELECT 'DietaryCategories',               COUNT(*) FROM DietaryCategories
UNION ALL SELECT 'Sports',                          COUNT(*) FROM Sports
UNION ALL SELECT 'Recipes',                         COUNT(*) FROM Recipes
UNION ALL SELECT 'MenuItems',                       COUNT(*) FROM MenuItems
UNION ALL SELECT 'Coaches',                         COUNT(*) FROM Coaches
UNION ALL SELECT 'Teams',                           COUNT(*) FROM Teams
UNION ALL SELECT 'CafeteriaSettings',               COUNT(*) FROM CafeteriaSettings
UNION ALL SELECT 'SportsMealRecommendationRules',   COUNT(*) FROM SportsMealRecommendationRules
UNION ALL SELECT 'InventoryItems (cafeteria)',      COUNT(*) FROM InventoryItems
   WHERE Category IN ('Food','Beverage','Kitchen Supply');