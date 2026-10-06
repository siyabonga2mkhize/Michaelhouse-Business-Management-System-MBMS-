using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // The sports the school offers, with their nutrition archetype.
    //
    // One list for the parent's profile wizard, the Coach's fixture
    // form and StudentSportStatus, so a sport chosen at registration
    // always matches the Coach's fixtures by name.
    // ============================================================
    public static class SportCatalogue
    {
        private static readonly Dictionary<string, SportArchetype> Sports =
            new Dictionary<string, SportArchetype>(StringComparer.OrdinalIgnoreCase)
            {
                // Power
                { "Rugby",           SportArchetype.Power },
                { "Water Polo",      SportArchetype.Power },
                { "Hockey",          SportArchetype.Power },
                { "Basketball",      SportArchetype.Power },

                // Endurance
                { "Athletics",       SportArchetype.Endurance },
                { "Swimming",        SportArchetype.Endurance },
                { "Cross Country",   SportArchetype.Endurance },
                { "Cycling",         SportArchetype.Endurance },
                { "Rowing",          SportArchetype.Endurance },
                { "Football",        SportArchetype.Endurance },

                // Skill
                { "Cricket",         SportArchetype.Skill },
                { "Tennis",          SportArchetype.Skill },
                { "Squash",          SportArchetype.Skill },
                { "Golf",            SportArchetype.Skill },

                // Speed
                { "Sprinting",       SportArchetype.Speed },
                { "Sprint Swimming", SportArchetype.Speed }
            };

        // Alphabetical, for drop-downs and checkboxes
        public static IList<string> Names
        {
            get { return Sports.Keys.OrderBy(n => n).ToList(); }
        }

        // The catalogue spelling of a sport, or null if it isn't offered
        public static string Normalise(string sport)
        {
            if (string.IsNullOrWhiteSpace(sport)) return null;

            var trimmed = sport.Trim();
            return Sports.Keys.FirstOrDefault(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        public static SportArchetype ArchetypeOf(string sport)
        {
            SportArchetype archetype;
            return sport != null && Sports.TryGetValue(sport.Trim(), out archetype) ? archetype : SportArchetype.None;
        }

        // StudentProfile.Sports ("Rugby, Athletics") → distinct names.
        // Offered sports use the catalogue spelling; anything else
        // already on a profile is kept as written, so nothing is lost.
        // "None" means no sport.
        public static List<string> Parse(string sports)
        {
            if (string.IsNullOrWhiteSpace(sports)) return new List<string>();

            return sports
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0 && !string.Equals(s, "None", StringComparison.OrdinalIgnoreCase))
                .Select(s => Normalise(s) ?? s)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string Join(IEnumerable<string> sports)
        {
            return string.Join(", ", (sports ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => Normalise(s) ?? s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}
