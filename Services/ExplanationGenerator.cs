using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Michaelhouse.Services
{
    public class ExplanationGenerator
    {
        /// <summary>
        /// Generate a human-readable explanation from a RoomScoreResult and supporting info.
        /// </summary>
        public string Generate(RoomScoreResult scoreResult, StudentProfile profile, Room room, Residence residence, double compatibility, double confidence)
        {
            if (scoreResult == null) throw new ArgumentNullException(nameof(scoreResult));

            var positives = new List<string>();
            var neutrals = new List<string>();
            var negatives = new List<string>();

            foreach (var entry in scoreResult.ScoreBreakdown)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                if (entry.StartsWith("FinalScore", StringComparison.OrdinalIgnoreCase)) continue;

                // Try to extract a normalized value like "v=0.80" from the entry
                double value = ExtractValue(entry);

                // Determine factor key (text before ':' or first word)
                var key = entry.Split(':').FirstOrDefault()?.Trim() ?? entry;

                // Map to readable phrase depending on key and value
                switch (true)
                {
                    case var _ when key.StartsWith("SameGrade", StringComparison.OrdinalIgnoreCase):
                        if (value >= 0.7) positives.Add("the room contains students from the same grade");
                        else if (value >= 0.4) neutrals.Add("the room has some students from the same grade");
                        else negatives.Add("the room does not contain students from the same grade");
                        break;

                    case var _ when key.StartsWith("OccupancyBalance", StringComparison.OrdinalIgnoreCase):
                        if (value >= 0.7) positives.Add("it has balanced occupancy");
                        else if (value >= 0.4) neutrals.Add("occupancy is moderately balanced");
                        else negatives.Add("occupancy is unbalanced");
                        break;

                    case var _ when key.IndexOf("AgeCompatibility", StringComparison.OrdinalIgnoreCase) >= 0:
                        if (value >= 0.7) positives.Add("occupants are age-compatible with the student");
                        else if (value >= 0.4) neutrals.Add("age difference with occupants is moderate");
                        else negatives.Add("age difference with occupants is large");
                        break;

                    case var _ when key.IndexOf("BehaviourCompatibility", StringComparison.OrdinalIgnoreCase) >= 0:
                        if (value >= 0.7) positives.Add("no behavioural conflicts with current occupants");
                        else negatives.Add("known behavioural conflicts with current occupants");
                        break;

                    case var _ when key.IndexOf("MedicalAccommodation", StringComparison.OrdinalIgnoreCase) >= 0:
                        if (value >= 0.7) positives.Add("it meets the student's medical accommodation needs");
                        else negatives.Add("it does not meet the student's medical needs");
                        break;

                    case var _ when key.IndexOf("LeadershipDistribution", StringComparison.OrdinalIgnoreCase) >= 0:
                        if (value >= 0.7) positives.Add("it helps distribute leadership roles effectively");
                        else if (value >= 0.4) neutrals.Add("leadership distribution is moderate");
                        else negatives.Add("team already has several leaders");
                        break;

                    case var _ when key.IndexOf("MaintenanceStatus", StringComparison.OrdinalIgnoreCase) >= 0:
                        if (value >= 0.7) positives.Add("no maintenance issues");
                        else negatives.Add("the room has maintenance issues");
                        break;

                    case var _ when key.IndexOf("DistanceToHouseMaster", StringComparison.OrdinalIgnoreCase) >= 0:
                        if (value >= 0.7) positives.Add("it is close to the house master's office");
                        else neutrals.Add("it is at a neutral distance from the house master's office");
                        break;

                    default:
                        // fallback: use sentiment by value
                        if (value >= 0.7) positives.Add(key);
                        else if (value >= 0.4) neutrals.Add(key);
                        else negatives.Add(key);
                        break;
                }
            }

            // Build explanation sentence
            var parts = new List<string>();
            if (positives.Any()) parts.Add(string.Join(", ", positives));
            if (neutrals.Any()) parts.Add(string.Join(", ", neutrals));

            var location = residence != null && room != null ? $"{residence.Name} Room {room.RoomNumber}" : (room != null ? $"Room {room.RoomNumber}" : "the selected room");

            var reason = parts.Any() ? string.Join(", and ", parts) : "it met several allocation criteria";

            var explanation = $"The student was allocated to {location} because {reason}, achieving a compatibility score of {Math.Round(compatibility, 2)}%";

            if (negatives.Any())
            {
                explanation += $". Note: {string.Join(", ", negatives)}.";
            }

            // Add confidence as textual note
            explanation += $" Confidence: {Math.Round(confidence * 100)}%.";

            return explanation;
        }

        private double ExtractValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            try
            {
                var m = Regex.Match(text, "v=([0-9]*\\.?[0-9]+)");
                if (m.Success && double.TryParse(m.Groups[1].Value, out double v))
                    return v;
            }
            catch { }

            return 0; // unknown => conservative
        }
    }
}
