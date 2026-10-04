using Michaelhouse.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // Seed data: more meals, their ingredients, the suppliers we buy
    // them from, and opening stock — called from
    // Migrations/Configuration.Seed after the original 30 meals.
    //
    // Safe to run on every Update-Database:
    //   • ingredients / meals / suppliers / links are only ADDED when
    //     missing (matched by name) — meals the Dietitian has since
    //     edited and suppliers the manager has changed are left alone
    //   • reorder / target levels are only set while still unset
    //   • opening stock is only written for an ingredient that has no
    //     stock at all and no stock history, and goes through
    //     IngredientInventoryService, so it appears in the history as
    //     "Opening balance"
    //
    // Suppliers are made up for the demo (".example" addresses can't
    // receive email). Prices are rough Rand-per-kg / litre figures.
    // A few items deliberately start low or out of stock so the
    // dashboard's warnings can be demonstrated — see Stock below.
    // ============================================================

    public static class CafeteriaInventorySeed
    {
        // ── New ingredients: name, unit, kcal/protein/carb/fat per unit,
        //    allergens, dietary tags ───────────────────────────────────
        private static readonly object[][] NewIngredients =
        {
            //            name                  unit  kcal   prot   carb   fat    allergens  tags
            new object[] { "Halal Chicken Thighs", "g", 1.77m, 0.24m, 0m,    0.09m,  null,      "Poultry,Halal" },
            new object[] { "Halal Beef Mince",     "g", 2.50m, 0.26m, 0m,    0.15m,  null,      "Meat,Halal" },
            new object[] { "Lamb Shoulder",        "g", 2.82m, 0.25m, 0m,    0.20m,  null,      "Meat" },
            new object[] { "Pork Sausages",        "g", 3.00m, 0.14m, 0.03m, 0.26m,  null,      "Pork" },
            new object[] { "Tuna",                 "g", 1.16m, 0.26m, 0m,    0.01m,  "Fish",    null },
            new object[] { "Tofu",                 "g", 0.76m, 0.08m, 0.02m, 0.05m,  "Soy",     null },
            new object[] { "Butternut",            "g", 0.45m, 0.01m, 0.12m, 0.001m, null,      null },
            new object[] { "Cabbage",              "g", 0.25m, 0.013m,0.058m,0.001m, null,      null },
            new object[] { "Green Beans",          "g", 0.31m, 0.018m,0.07m, 0.002m, null,      null },
            new object[] { "Mushrooms",            "g", 0.22m, 0.031m,0.033m,0.003m, null,      null },
            new object[] { "Garlic",               "g", 1.49m, 0.064m,0.33m, 0.005m, null,      null },
            new object[] { "Lettuce",              "g", 0.15m, 0.014m,0.029m,0.002m, null,      null },
            new object[] { "Cucumber",             "g", 0.15m, 0.007m,0.036m,0.001m, null,      null },
            new object[] { "Apple",                "g", 0.52m, 0.003m,0.14m, 0.002m, null,      null },
            new object[] { "Maize Meal",           "g", 3.60m, 0.08m, 0.78m, 0.015m, null,      null },
            new object[] { "Sugar Beans",          "g", 3.33m, 0.21m, 0.60m, 0.012m, null,      null },
            new object[] { "Coconut Milk",         "ml",2.30m, 0.023m,0.055m,0.24m,  null,      null },
            new object[] { "Tortilla Wraps",       "g", 3.10m, 0.08m, 0.50m, 0.08m,  "Gluten",  null },
            new object[] { "Bread Rolls",          "g", 2.70m, 0.09m, 0.50m, 0.035m, "Gluten",  null },
            new object[] { "Butter",               "g", 7.17m, 0.009m,0.001m,0.81m,  "Dairy",   null }
        };

        // ── New meals ───────────────────────────────────────────────
        private class MealSeed
        {
            public string Name;
            public string Description;
            public string Slot;          // Breakfast / Lunch / Dinner
            public string Notes;
            public int Prep, Cook;
            public string Station;
            public bool Farm;
            public object[][] Items;     // { ingredient, grams/ml per portion, note }
        }

        private static readonly MealSeed[] NewMeals =
        {
            // ── Breakfast ──
            new MealSeed { Name = "Maize Porridge & Banana", Slot = "Breakfast", Prep = 5, Cook = 20, Station = "Stove",
                Description = "Soft maize meal porridge with warm milk and sliced banana.",
                Notes = "Cook maize meal slowly, stirring; serve with milk and banana.",
                Items = new[] { I("Maize Meal", 70, "Cooked soft."), I("Milk", 200, "Warm."), I("Banana", 100, "Sliced.") } },
            new MealSeed { Name = "Mushroom & Spinach Omelette", Slot = "Breakfast", Prep = 10, Cook = 10, Station = "Stove",
                Description = "Three-egg omelette with mushrooms and spinach, with toast.",
                Notes = "Sauté mushrooms and spinach, add beaten eggs, fold; serve with toast.",
                Items = new[] { I("Egg", 180, "Beaten."), I("Mushrooms", 50, "Sliced."), I("Spinach", 30, "Fresh."), I("Whole Wheat Bread", 60, "Toasted."), I("Cooking Oil", 5, "For the pan.") } },
            new MealSeed { Name = "Sausage, Egg & Toast", Slot = "Breakfast", Prep = 5, Cook = 15, Station = "Grill",
                Description = "Grilled pork sausages, fried egg, grilled tomato and toast.",
                Notes = "Grill sausages and tomato; fry eggs; serve with toast. Contains pork.",
                Items = new[] { I("Pork Sausages", 100, "Grilled."), I("Egg", 100, "Fried."), I("Tomato", 50, "Halved, grilled."), I("Whole Wheat Bread", 60, "Toasted."), I("Cooking Oil", 5, "For frying.") } },
            new MealSeed { Name = "Tofu Scramble on Toast", Slot = "Breakfast", Prep = 10, Cook = 10, Station = "Stove",
                Description = "Vegan tofu scramble with spinach and tomato on toast.",
                Notes = "Crumble tofu, cook with onion, tomato and spinach; season; serve on toast.",
                Items = new[] { I("Tofu", 150, "Crumbled."), I("Spinach", 30, "Wilted."), I("Tomato", 40, "Diced."), I("Onion", 20, "Diced."), I("Whole Wheat Bread", 80, "Toasted."), I("Cooking Oil", 5, "For cooking.") } },
            new MealSeed { Name = "Halal Chicken Breakfast Wrap", Slot = "Breakfast", Prep = 10, Cook = 15, Station = "Grill",
                Description = "Halal chicken, egg, tomato and lettuce in a soft tortilla.",
                Notes = "Grill halal chicken strips separately from other meat; assemble wraps to order.",
                Items = new[] { I("Tortilla Wraps", 90, "One wrap."), I("Halal Chicken Thighs", 90, "Grilled strips."), I("Egg", 60, "Scrambled."), I("Tomato", 30, "Sliced."), I("Lettuce", 20, "Shredded.") } },
            new MealSeed { Name = "Apple & Cinnamon Oats", Slot = "Breakfast", Prep = 5, Cook = 15, Station = "Stove", Farm = true,
                Description = "Oats cooked in milk with stewed apple.",
                Notes = "Cook oats in milk; stir through diced stewed apple.",
                Items = new[] { I("Oats", 70, "Cooked in milk."), I("Milk", 200, "For cooking."), I("Apple", 100, "Diced and stewed.") } },

            // ── Lunch ──
            new MealSeed { Name = "Halal Chicken Biryani", Slot = "Lunch", Prep = 20, Cook = 45, Station = "Stove",
                Description = "Spiced halal chicken layered with rice and peas.",
                Notes = "Marinate halal chicken in spice; cook with onion; layer with par-boiled rice and peas; steam.",
                Items = new[] { I("Halal Chicken Thighs", 150, "Diced, marinated."), I("White Rice", 120, "Par-boiled."), I("Onion", 30, "Fried."), I("Peas", 40, "Added at the end."), I("Curry Spice Mix", 10, "Biryani spice."), I("Cooking Oil", 10, "For frying.") } },
            new MealSeed { Name = "Halal Beef Burger & Salad", Slot = "Lunch", Prep = 15, Cook = 15, Station = "Grill",
                Description = "Halal beef patty on a roll with cheese and a side salad.",
                Notes = "Shape and grill halal patties on a separate grill area; serve with salad.",
                Items = new[] { I("Halal Beef Mince", 150, "Shaped into a patty."), I("Bread Rolls", 80, "Toasted."), I("Cheese", 20, "Slice."), I("Lettuce", 30, "Shredded."), I("Tomato", 40, "Sliced."), I("Cucumber", 30, "Sliced.") } },
            new MealSeed { Name = "Tuna Pasta Salad", Slot = "Lunch", Prep = 15, Cook = 12, Station = "Cold Prep",
                Description = "Pasta tossed with tuna, cucumber, tomato and lettuce.",
                Notes = "Cook and cool pasta; mix with flaked tuna, vegetables and a little oil. Serve chilled.",
                Items = new[] { I("Pasta", 100, "Cooked and cooled."), I("Tuna", 100, "Flaked."), I("Cucumber", 40, "Diced."), I("Tomato", 40, "Diced."), I("Lettuce", 30, "Shredded."), I("Cooking Oil", 10, "Dressing.") } },
            new MealSeed { Name = "Bean & Butternut Wrap", Slot = "Lunch", Prep = 15, Cook = 25, Station = "Oven", Farm = true,
                Description = "Roast butternut and kidney beans in a tortilla — vegan.",
                Notes = "Roast butternut; warm beans with onion; fill wraps with lettuce.",
                Items = new[] { I("Tortilla Wraps", 90, "One wrap."), I("Kidney Beans", 100, "Warmed."), I("Butternut", 100, "Roasted cubes."), I("Onion", 20, "Sautéed."), I("Lettuce", 20, "Shredded.") } },
            new MealSeed { Name = "Pap, Beef Stew & Cabbage", Slot = "Lunch", Prep = 20, Cook = 90, Station = "Stove", Farm = true,
                Description = "Stiff maize pap with slow-cooked beef stew and braised cabbage.",
                Notes = "Slow-cook beef with tomato and onion; braise cabbage; serve with stiff pap.",
                Items = new[] { I("Maize Meal", 100, "Stiff pap."), I("Lean Beef", 140, "Cubed, stewed."), I("Tomato", 50, "For the stew."), I("Onion", 30, "Diced."), I("Cabbage", 100, "Braised."), I("Cooking Oil", 10, "For browning.") } },
            new MealSeed { Name = "Tofu & Vegetable Stir-Fry", Slot = "Lunch", Prep = 15, Cook = 15, Station = "Stove",
                Description = "Tofu and mixed vegetables stir-fried with garlic, on brown rice — vegan.",
                Notes = "Fry tofu until golden; stir-fry vegetables with garlic; serve on brown rice.",
                Items = new[] { I("Tofu", 150, "Cubed."), I("Mixed Vegetables", 120, "Stir-fried."), I("Brown Rice", 100, "Cooked."), I("Garlic", 5, "Crushed."), I("Cooking Oil", 10, "For frying.") } },

            // ── Dinner ──
            new MealSeed { Name = "Lamb Curry & Rice", Slot = "Dinner", Prep = 20, Cook = 120, Station = "Stove",
                Description = "Slow-cooked lamb curry with tomato, served on white rice.",
                Notes = "Brown lamb, cook slowly with onion, tomato and spice until tender.",
                Items = new[] { I("Lamb Shoulder", 160, "Cubed."), I("White Rice", 120, "Cooked."), I("Onion", 40, "Diced."), I("Tomato", 50, "Chopped."), I("Curry Spice Mix", 10, "Curry spice."), I("Cooking Oil", 10, "For browning.") } },
            new MealSeed { Name = "Halal Cottage Pie", Slot = "Dinner", Prep = 25, Cook = 45, Station = "Oven", Farm = true,
                Description = "Halal beef mince with carrots and peas under mashed potato.",
                Notes = "Cook halal mince with vegetables; top with buttery mash; bake until golden.",
                Items = new[] { I("Halal Beef Mince", 150, "Browned."), I("Potato", 200, "Mashed."), I("Peas", 40, "In the filling."), I("Carrots", 40, "Diced."), I("Onion", 30, "Diced."), I("Butter", 10, "For the mash."), I("Milk", 30, "For the mash.") } },
            new MealSeed { Name = "Coconut Chickpea & Butternut Curry", Slot = "Dinner", Prep = 15, Cook = 35, Station = "Stove", Farm = true,
                Description = "Chickpeas and butternut in a mild coconut curry, on brown rice — vegan.",
                Notes = "Simmer butternut and chickpeas in coconut milk with onion and spice.",
                Items = new[] { I("Chickpeas", 120, "Cooked."), I("Butternut", 120, "Cubed."), I("Coconut Milk", 100, "Sauce."), I("Onion", 30, "Diced."), I("Curry Spice Mix", 8, "Mild."), I("Brown Rice", 100, "Cooked.") } },
            new MealSeed { Name = "Mushroom & Spinach Pasta Bake", Slot = "Dinner", Prep = 20, Cook = 30, Station = "Oven",
                Description = "Pasta baked in a creamy mushroom and spinach sauce with cheese.",
                Notes = "Make a white sauce with butter and milk; mix with pasta, mushrooms and spinach; top with cheese and bake.",
                Items = new[] { I("Pasta", 110, "Par-cooked."), I("Mushrooms", 80, "Sliced."), I("Spinach", 40, "Wilted."), I("Cheese", 40, "Grated."), I("Milk", 100, "White sauce."), I("Butter", 10, "White sauce.") } },
            new MealSeed { Name = "Pap, Sugar Beans & Chakalaka", Slot = "Dinner", Prep = 20, Cook = 60, Station = "Stove", Farm = true,
                Description = "Pap with sugar beans and spicy chakalaka relish — vegan.",
                Notes = "Soak and cook beans; cook chakalaka with carrot, tomato, onion and spice; serve with pap.",
                Items = new[] { I("Maize Meal", 100, "Pap."), I("Sugar Beans", 80, "Soaked and cooked."), I("Carrots", 50, "Grated."), I("Tomato", 50, "Chopped."), I("Onion", 30, "Diced."), I("Curry Spice Mix", 5, "Chakalaka spice."), I("Cooking Oil", 10, "For cooking.") } },
            new MealSeed { Name = "Halal Grilled Chicken, Wedges & Green Beans", Slot = "Dinner", Prep = 15, Cook = 40, Station = "Grill", Farm = true,
                Description = "Garlic-grilled halal chicken with potato wedges and green beans.",
                Notes = "Grill halal chicken on its own grill; roast wedges; steam beans.",
                Items = new[] { I("Halal Chicken Thighs", 180, "Grilled."), I("Potato", 200, "Wedges, roasted."), I("Green Beans", 100, "Steamed."), I("Garlic", 5, "Marinade."), I("Seasoning", 3, "To taste."), I("Cooking Oil", 10, "For roasting.") } }
        };

        // ── Suppliers (fictional) ───────────────────────────────────
        private static readonly string[][] Suppliers =
        {
            //           name                            contact             phone           email                                   address
            new[] { "Midlands Fresh Produce",        "Thandi Ngcobo",    "033 000 1101", "orders@midlandsfresh.example",        "Howick, KwaZulu-Natal" },
            new[] { "Umgeni Meat Wholesalers",       "Pieter van Wyk",   "033 000 1102", "sales@umgenimeat.example",            "Pietermaritzburg, KwaZulu-Natal" },
            new[] { "Al-Huda Halal Meats",           "Yusuf Moosa",      "031 000 1103", "orders@alhudahalal.example",          "Durban, KwaZulu-Natal (halal-certified)" },
            new[] { "Midlands Dairy Co-operative",   "Sarah Mkhize",     "033 000 1104", "dairy@midlandsdairy.example",         "Mooi River, KwaZulu-Natal" },
            new[] { "Natal Coast Seafood",           "Ravi Naidoo",      "031 000 1105", "orders@natalseafood.example",         "Durban, KwaZulu-Natal" },
            new[] { "Howick Bakery",                 "Lindiwe Zulu",     "033 000 1106", "bakery@howickbakery.example",         "Howick, KwaZulu-Natal" },
            new[] { "KZN Dry Goods Distributors",    "Johan Botha",      "033 000 1107", "orders@kzndrygoods.example",          "Pietermaritzburg, KwaZulu-Natal" },
            new[] { "Pietermaritzburg Catering Supplies", "Nomvula Dlamini", "033 000 1108", "sales@pmbcatering.example",   "Pietermaritzburg, KwaZulu-Natal" }
        };

        private const string Produce = "Midlands Fresh Produce";
        private const string Meat = "Umgeni Meat Wholesalers";
        private const string Halal = "Al-Huda Halal Meats";
        private const string Dairy = "Midlands Dairy Co-operative";
        private const string Seafood = "Natal Coast Seafood";
        private const string Bakery = "Howick Bakery";
        private const string DryGoods = "KZN Dry Goods Distributors";
        private const string Backup = "Pietermaritzburg Catering Supplies";

        // ── Stock: ingredient → preferred supplier, backup (or null),
        //    reorder / target / opening bought-in / opening farm (all in
        //    kg or litres), cost per kg / L, lead days, invoice name ──
        private class StockSeed
        {
            public string Ingredient, Preferred, Second, InvoiceName;
            public decimal Reorder, Target, Opening, Farm, Cost;
            public int Lead;
        }

        private static StockSeed S(string ingredient, string preferred, string second, decimal reorder, decimal target,
            decimal opening, decimal farm, decimal cost, int lead, string invoiceName = null)
        {
            return new StockSeed { Ingredient = ingredient, Preferred = preferred, Second = second, Reorder = reorder, Target = target,
                Opening = opening, Farm = farm, Cost = cost, Lead = lead, InvoiceName = invoiceName };
        }

        private static readonly StockSeed[] Stock =
        {
            // Meat, poultry, fish — deliberately some LOW / OUT for the demo
            S("Chicken Breast",       Meat,     Backup, 40, 120,  25,  0, 89.90m, 2, "CHICKEN BREAST FILLET IQF"),   // LOW
            S("Turkey Breast",        Meat,     Backup, 10,  30,  18,  0, 119.00m, 3, "TURKEY BREAST FILLET"),
            S("Lean Beef",            Meat,     Backup, 40, 100,  70,  0, 129.00m, 2, "BEEF CHUCK LEAN CUBED"),
            S("Lamb Shoulder",        Meat,     null,   15,  40,  25,  0, 169.00m, 3, "LAMB SHOULDER DICED"),
            S("Pork Sausages",        Meat,     Backup, 10,  30,  16,  0, 79.00m, 2, "PORK BANGERS THICK"),
            S("Halal Chicken Thighs", Halal,    null,   40, 120,  80,  0, 84.00m, 2, "HALAL CHICKEN THIGH FILLET"),
            S("Halal Beef Mince",     Halal,    null,   20,  60,  35,  0, 119.00m, 2, "HALAL BEEF MINCE LEAN"),
            S("Hake Fillet",          Seafood,  Backup, 20,  60,   0,  0, 115.00m, 3, "HAKE FILLET SKINLESS"),       // OUT
            S("Tuna",                 Seafood,  DryGoods, 8,  25,   6,  0, 140.00m, 5, "TUNA CHUNKS IN BRINE"),       // LOW
            S("Tofu",                 DryGoods, null,    8,  25,  15,  0, 65.00m, 5, "TOFU FIRM"),

            // Dairy & eggs
            S("Milk",                 Dairy,    Backup, 120, 300, 90, 0, 17.50m, 1, "FULL CREAM MILK"),            // LOW (litres)
            S("Greek Yoghurt",        Dairy,    Backup, 30,  80,  45,  0, 54.00m, 2, "GREEK YOGHURT PLAIN"),
            S("Cheese",               Dairy,    Backup, 15,  40,  25,  0, 109.00m, 3, "CHEDDAR CHEESE BLOCK"),
            S("Butter",               Dairy,    Backup,  5,  15,   9,  0, 129.00m, 3, "SALTED BUTTER"),
            S("Egg",                  Dairy,    Backup, 40, 100,  60,  0, 42.00m, 2, "EGGS LARGE TRAY"),

            // Bakery
            S("Whole Wheat Bread",    Bakery,   Backup, 30,  80,  40,  0, 32.00m, 1, "BROWN BREAD SLICED 700G"),
            S("Breakfast Wrap",       Bakery,   Backup, 10,  30,  15,  0, 58.00m, 2),
            S("Tortilla Wraps",       Bakery,   Backup, 10,  30,  16,  0, 56.00m, 2, "FLOUR TORTILLAS 25CM"),
            S("Bread Rolls",          Bakery,   Backup, 10,  30,  12,  0, 34.00m, 1, "HAMBURGER ROLLS"),

            // Fresh produce — some from the school farm
            S("Banana",               Produce,  Backup, 30,  80,  45,  0, 22.00m, 1),
            S("Berries",              Produce,  null,   10,  25,  12,  0, 120.00m, 2, "MIXED BERRIES FROZEN"),
            S("Fresh Fruit",          Produce,  Backup, 40, 100,  60,  0, 24.00m, 1),
            S("Apple",                Produce,  Backup, 20,  60,  20, 15, 21.00m, 1),
            S("Broccoli",             Produce,  Backup, 15,  40,  20,  0, 45.00m, 1),
            S("Carrots",              Produce,  Backup, 20,  60,  15, 25, 14.00m, 1),
            S("Mixed Vegetables",     Produce,  Backup, 30,  80,  45,  0, 38.00m, 2, "MIXED VEG FROZEN"),
            S("Onion",                Produce,  Backup, 30,  80,  50,  0, 16.00m, 1),
            S("Potato",               Produce,  Backup, 80, 200,  60, 90, 12.00m, 1),
            S("Spinach",              Produce,  Backup, 10,  30,   5, 12, 35.00m, 1),
            S("Sweet Potato",         Produce,  Backup, 20,  60,  20, 15, 18.00m, 1),
            S("Tomato",               Produce,  Backup, 30,  80,  30, 20, 22.00m, 1),
            S("Butternut",            Produce,  Backup, 25,  70,  15, 30, 13.00m, 1),
            S("Cabbage",              Produce,  Backup, 20,  60,  10, 30, 9.00m, 1),
            S("Green Beans",          Produce,  Backup, 15,  40,  20,  0, 40.00m, 1),
            S("Mushrooms",            Produce,  Backup, 10,  25,  12,  0, 75.00m, 1, "BUTTON MUSHROOMS"),
            S("Garlic",               Produce,  DryGoods, 2,  6,    3,  0, 85.00m, 2),
            S("Lettuce",              Produce,  Backup, 10,  25,   6,  8, 30.00m, 1),
            S("Cucumber",             Produce,  Backup, 10,  25,   8,  6, 25.00m, 1),

            // Dry goods & pantry
            S("White Rice",           DryGoods, Backup, 80, 200, 150, 0, 22.00m, 3, "RICE PARBOILED 10KG"),
            S("Brown Rice",           DryGoods, Backup, 40, 100,  70, 0, 29.00m, 3, "BROWN RICE 5KG"),
            S("Pasta",                DryGoods, Backup, 50, 120,  90, 0, 25.00m, 3, "PASTA PENNE 5KG"),
            S("Couscous",             DryGoods, Backup, 15,  40,  25, 0, 48.00m, 5),
            S("Oats",                 DryGoods, Backup, 30,  80,  50, 0, 24.00m, 3, "ROLLED OATS 10KG"),
            S("Granola",              DryGoods, Backup, 10,  30,  18, 0, 65.00m, 5),
            S("Weet-Bix",             DryGoods, Backup, 15,  40,  25, 0, 52.00m, 3),
            S("Maize Meal",           DryGoods, Backup, 60, 150, 100, 0, 11.00m, 3, "SUPER MAIZE MEAL 10KG"),
            S("Lentils",              DryGoods, Backup, 15,  40,  25, 0, 35.00m, 5),
            S("Chickpeas",            DryGoods, Backup, 15,  40,  20, 0, 38.00m, 5),
            S("Kidney Beans",         DryGoods, Backup, 15,  40,  22, 0, 36.00m, 5),
            S("Sugar Beans",          DryGoods, Backup, 15,  40,  20, 0, 34.00m, 5),
            S("Peas",                 DryGoods, Backup, 15,  40,  25, 0, 32.00m, 3, "GARDEN PEAS FROZEN"),
            S("Peanut Butter",        DryGoods, Backup,  5,  15,   8, 0, 72.00m, 5),
            S("Cooking Oil",          DryGoods, Backup, 40, 100,  60, 0, 32.00m, 3, "SUNFLOWER OIL 20L"),
            S("Coconut Milk",         DryGoods, null,   10,  30,  14, 0, 45.00m, 5),
            S("Curry Spice Mix",      DryGoods, Backup,  3,   8,   5, 0, 140.00m, 5),
            S("Seasoning",            DryGoods, Backup,  2,   6,   4, 0, 95.00m, 5),
            S("Tomato Sauce",         DryGoods, Backup, 15,  40,  20, 0, 30.00m, 3)
        };

        // ============================================================

        public static void Run(DBContextClass context)
        {
            SeedIngredients(context);
            SeedMeals(context);
            SeedSuppliersAndLinks(context);
            SeedStock(context);
        }

        private static object[] I(string ingredient, decimal quantity, string note)
        {
            return new object[] { ingredient, quantity, note };
        }

        private static void SeedIngredients(DBContextClass context)
        {
            foreach (var row in NewIngredients)
            {
                string name = (string)row[0];
                var ingredient = context.Ingredients.FirstOrDefault(x => x.Name == name);

                if (ingredient == null)
                {
                    context.Ingredients.Add(new Ingredient
                    {
                        Name = name,
                        Unit = (string)row[1],
                        CaloriesPerUnit = (decimal)row[2],
                        ProteinGramsPerUnit = (decimal)row[3],
                        CarbohydrateGramsPerUnit = (decimal)row[4],
                        FatGramsPerUnit = (decimal)row[5],
                        Allergens = (string)row[6],
                        DietaryTags = (string)row[7],
                        IsActive = true
                    });
                }
                else
                {
                    // Only fill gaps — never overwrite what's been set since
                    if (string.IsNullOrEmpty(ingredient.Allergens)) ingredient.Allergens = (string)row[6];
                    if (string.IsNullOrEmpty(ingredient.DietaryTags)) ingredient.DietaryTags = (string)row[7];
                }
            }

            context.SaveChanges();
        }

        private static void SeedMeals(DBContextClass context)
        {
            var ingredients = context.Ingredients.ToList().ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var meal in NewMeals)
            {
                // Already there (perhaps edited by the Dietitian): leave it
                if (context.MenuItems.Any(m => m.Name == meal.Name)) continue;

                var recipe = new Recipe
                {
                    Name = meal.Name,
                    PreparationNotes = meal.Notes,
                    StandardPortionCount = 1,
                    PrepTimeMinutes = meal.Prep,
                    CookTimeMinutes = meal.Cook,
                    Station = meal.Station,
                    IsActive = true
                };

                // Base ingredients come from the original meal seed; if it
                // hasn't run yet, skip this meal until a later run
                if (meal.Items.Any(item => !ingredients.ContainsKey((string)item[0])))
                {
                    System.Diagnostics.Debug.WriteLine("Cafeteria seed: skipped '" + meal.Name + "' — ingredients not seeded yet.");
                    continue;
                }

                foreach (var item in meal.Items)
                {
                    var ingredient = ingredients[(string)item[0]];

                    recipe.RecipeIngredients.Add(new RecipeIngredient
                    {
                        Ingredient = ingredient,
                        IngredientId = ingredient.Id,
                        QuantityPerStandardPortion = (decimal)item[1],
                        PreparationNotes = (string)item[2]
                    });
                }

                var used = recipe.RecipeIngredients.Select(ri => ri.Ingredient).ToList();
                var nutrition = MealLibraryService.CalculateNutrition(recipe.RecipeIngredients);

                var menuItem = new MenuItem
                {
                    Name = meal.Name,
                    Description = meal.Description,
                    DietaryClassification = MealLibraryService.StrictestClassification(used),
                    IsBreakfastItem = meal.Slot == "Breakfast",
                    IsLunchItem = meal.Slot == "Lunch",
                    IsDinnerItem = meal.Slot == "Dinner",
                    IsFarmGrownProduce = meal.Farm,
                    NutritionCategory = MealLibraryService.SuggestCategory(nutrition),
                    IsActive = true,
                    Recipe = recipe
                };
                MealLibraryService.ApplyNutrition(menuItem, nutrition);

                context.MenuItems.Add(menuItem);
            }

            context.SaveChanges();
        }

        private static void SeedSuppliersAndLinks(DBContextClass context)
        {
            foreach (var s in Suppliers)
            {
                string name = s[0];
                if (context.Suppliers.Any(x => x.Name == name)) continue;

                context.Suppliers.Add(new Supplier
                {
                    Name = name,
                    ContactPerson = s[1],
                    Phone = s[2],
                    Email = s[3],
                    Address = s[4],
                    IsActive = true,
                    SuppliesCafeteria = true
                });
            }
            context.SaveChanges();

            var suppliers = context.Suppliers.ToList().ToDictionary(x => x.Name);
            var ingredients = context.Ingredients.ToList().ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var row in Stock)
            {
                Ingredient ingredient;
                if (!ingredients.TryGetValue(row.Ingredient, out ingredient)) continue;

                AddLink(context, ingredient, suppliers[row.Preferred], true, row.InvoiceName, row.Cost, row.Lead);
                if (row.Second != null)
                {
                    // Backup supplier: a little dearer, a day slower
                    AddLink(context, ingredient, suppliers[row.Second], false, null, Math.Round(row.Cost * 1.08m, 2), row.Lead + 1);
                }
            }
            context.SaveChanges();
        }

        private static void AddLink(DBContextClass context, Ingredient ingredient, Supplier supplier, bool preferred, string invoiceName, decimal cost, int lead)
        {
            int ingredientId = ingredient.Id, supplierId = supplier.SupplierId;
            if (context.IngredientSuppliers.Any(x => x.IngredientId == ingredientId && x.SupplierId == supplierId)) return;

            // Only one preferred supplier per ingredient
            if (preferred && context.IngredientSuppliers.Any(x => x.IngredientId == ingredientId && x.IsPreferred)) preferred = false;

            context.IngredientSuppliers.Add(new IngredientSupplier
            {
                IngredientId = ingredientId,
                SupplierId = supplierId,
                IsPreferred = preferred,
                SupplierItemName = invoiceName,
                UnitCost = cost,
                LeadTimeDays = lead,
                IsActive = true
            });
        }

        private static void SeedStock(DBContextClass context)
        {
            var inventory = new IngredientInventoryService(context);
            var ingredients = context.Ingredients.ToList().ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var row in Stock)
            {
                Ingredient ingredient;
                if (!ingredients.TryGetValue(row.Ingredient, out ingredient)) continue;

                // Levels: only while nobody has set them
                if (ingredient.ReorderLevel == 0m && !ingredient.TargetStockLevel.HasValue)
                {
                    ingredient.ReorderLevel = IngredientUnits.ToBase(row.Reorder, ingredient.Unit);
                    ingredient.TargetStockLevel = IngredientUnits.ToBase(row.Target, ingredient.Unit);
                }
                context.SaveChanges();

                // Opening stock: only once, for an ingredient with no stock and no history
                int id = ingredient.Id;
                bool untouched = ingredient.FarmAvailableQuantity == 0m && ingredient.ExternalAvailableQuantity == 0m
                                 && !context.IngredientStockTransactions.Any(t => t.IngredientId == id);
                if (!untouched || !ingredient.IsActive) continue;

                if (row.Opening > 0m)
                    inventory.Apply(id, StockTransactionType.Adjustment, StockSource.External,
                        IngredientUnits.ToBase(row.Opening, ingredient.Unit), "OPENING", "Opening balance (seed data)", null);

                if (row.Farm > 0m)
                    inventory.Apply(id, StockTransactionType.Adjustment, StockSource.Farm,
                        IngredientUnits.ToBase(row.Farm, ingredient.Unit), "OPENING", "Opening balance — school farm (seed data)", null);
            }
        }
    }
}
