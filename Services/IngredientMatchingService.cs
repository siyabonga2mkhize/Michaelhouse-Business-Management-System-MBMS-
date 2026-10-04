using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Michaelhouse.Services
{
    // ============================================================
    // Matches an invoice line ("CHKN BREAST FILLET 2KG") to one of
    // the cafeteria's existing ingredients.
    //
    // In order of trust:
    //   1. the supplier's own item name / code saved on the
    //      ingredient (IngredientSupplier.SupplierItemName)
    //   2. the ingredient's exact name
    //   3. the ingredient's name appearing as whole words in the line
    //   4. word overlap — only a suggestion, never "confident"
    //
    // Anything not confident is shown to the manager as "Could not
    // match invoice item to an existing ingredient." to correct.
    // ============================================================

    public class IngredientMatch
    {
        public Ingredient Ingredient { get; set; }
        public bool Confident { get; set; }
        public string How { get; set; }
    }

    public class IngredientMatchingService
    {
        private static readonly HashSet<string> IgnoredWords = new HashSet<string>
        {
            "kg", "g", "gr", "l", "ml", "ltr", "x", "per", "pack", "pk", "bag", "box", "case", "tin", "fresh", "frozen",
            "the", "and", "of", "with", "a", "each", "ea", "unit", "units", "bulk", "grade", "a1"
        };

        private readonly List<Ingredient> _ingredients;
        private readonly List<IngredientSupplier> _links;

        public IngredientMatchingService(DBContextClass db)
        {
            _ingredients = db.Ingredients.Where(i => i.IsActive).ToList();
            _links = db.IngredientSuppliers.Where(x => x.IsActive && x.SupplierItemName != null).ToList();
        }

        public IngredientMatch Match(string description, string productCode, int? supplierId)
        {
            var text = Normalise(description);
            var code = Normalise(productCode);
            if (text.Length == 0 && code.Length == 0) return null;

            var byId = _ingredients.ToDictionary(i => i.Id);

            // 1. The supplier's own name / code for the item
            var links = _links.Where(l => byId.ContainsKey(l.IngredientId)).ToList();
            var ownSupplierFirst = links.OrderByDescending(l => supplierId.HasValue && l.SupplierId == supplierId.Value).ToList();

            foreach (var link in ownSupplierFirst)
            {
                var name = Normalise(link.SupplierItemName);
                if (name.Length == 0) continue;

                if (name == text || name == code || (name.Length >= 4 && ContainsWords(text, name)))
                {
                    return new IngredientMatch { Ingredient = byId[link.IngredientId], Confident = true, How = "supplier's item name" };
                }
            }

            // 2. Exact ingredient name
            var exact = _ingredients.Where(i => Normalise(i.Name) == text).ToList();
            if (exact.Count == 1)
            {
                return new IngredientMatch { Ingredient = exact[0], Confident = true, How = "ingredient name" };
            }

            // 3. Ingredient name as whole words in the line — the longest
            //    one wins ("brown rice" over "rice"); a tie isn't confident
            var contained = _ingredients
                .Select(i => new { Ingredient = i, Name = Normalise(i.Name) })
                .Where(x => x.Name.Length >= 3 && ContainsWords(text, x.Name))
                .OrderByDescending(x => x.Name.Length)
                .ToList();

            if (contained.Count > 0)
            {
                bool tie = contained.Count > 1 && contained[1].Name.Length == contained[0].Name.Length;
                return new IngredientMatch
                {
                    Ingredient = contained[0].Ingredient,
                    Confident = !tie,
                    How = tie ? "possible match — several ingredients fit" : "ingredient name in the invoice line"
                };
            }

            // 4. Word overlap: a suggestion only
            var words = Words(text);
            if (words.Count == 0) return null;

            var scored = _ingredients
                .Select(i =>
                {
                    var iw = Words(Normalise(i.Name));
                    if (iw.Count == 0) return new { Ingredient = i, Score = 0.0 };
                    double overlap = iw.Count(w => words.Contains(w) || words.Any(x => x.Length >= 4 && (x.StartsWith(w) || w.StartsWith(x))));
                    return new { Ingredient = i, Score = overlap / iw.Count };
                })
                .Where(x => x.Score >= 0.5)
                .OrderByDescending(x => x.Score)
                .ToList();

            if (scored.Count == 0) return null;

            return new IngredientMatch { Ingredient = scored[0].Ingredient, Confident = false, How = "possible match — please check" };
        }

        public static string Normalise(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.ToLowerInvariant().Replace("&", " and ");
            s = Regex.Replace(s, @"[^a-z0-9]+", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static bool ContainsWords(string text, string phrase)
        {
            return (" " + text + " ").Contains(" " + phrase + " ");
        }

        private static HashSet<string> Words(string normalised)
        {
            return new HashSet<string>(normalised
                .Split(' ')
                .Where(w => w.Length >= 2 && !IgnoredWords.Contains(w) && !Regex.IsMatch(w, @"^\d")));
        }
    }
}
