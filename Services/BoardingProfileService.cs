using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Services
{
    public class BoardingProfileService
    {
        public void GenerateBoardingProfile(StudentProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            profile.StudyStyle = ResolveStudyStyle(profile);
            profile.SocialScore = ResolveSocialScore(profile);
            profile.LeadershipScore = ResolveLeadershipScore(profile);
            profile.ActivityScore = ResolveActivityScore(profile);
            profile.MorningRoutine = profile.RoutinePreference == "WakeEarly" ? "Early" : "Late";
            profile.CompatibilityTraits = string.Join(", ", ResolveTraits(profile));
            profile.BoardingProfileGeneratedAt = DateTime.UtcNow;
        }

        public bool IsComplete(StudentProfile profile)
        {
            if (profile == null)
                return false;

            return profile.IsProfileComplete &&
                   !string.IsNullOrWhiteSpace(profile.Gender) &&
                   !string.IsNullOrWhiteSpace(profile.Grade) &&
                   !string.IsNullOrWhiteSpace(profile.LearningStyle) &&
                   !string.IsNullOrWhiteSpace(profile.EnvironmentPreference) &&
                   !string.IsNullOrWhiteSpace(profile.RoutinePreference) &&
                   !string.IsNullOrWhiteSpace(profile.ActivityPreference) &&
                   !string.IsNullOrWhiteSpace(profile.StudyPreference) &&
                   !string.IsNullOrWhiteSpace(profile.SocialPreference) &&
                   profile.BoardingProfileGeneratedAt.HasValue;
        }

        private string ResolveStudyStyle(StudentProfile profile)
        {
            if (profile.EnvironmentPreference == "Quiet")
                return "Quiet";

            if (profile.StudyPreference == "SmallGroup")
                return "Small group";

            if (profile.StudyPreference == "LargeGroup")
                return "Collaborative";

            return profile.LearningStyle ?? "Mixed";
        }

        private int ResolveSocialScore(StudentProfile profile)
        {
            var score = 50;

            if (profile.EnvironmentPreference == "Busy") score += 15;
            if (profile.ActivityPreference == "Team") score += 15;
            if (profile.StudyPreference == "LargeGroup") score += 10;
            if (profile.StudyPreference == "SmallGroup") score += 5;
            if (profile.SocialPreference == "Introverted") score -= 20;
            if (profile.SocialPreference == "Extroverted") score += 20;

            return Clamp(score);
        }

        private int ResolveLeadershipScore(StudentProfile profile)
        {
            var source = $"{profile.LeadershipRoles} {profile.ClubsAndSocieties} {profile.Sports}";
            var score = string.IsNullOrWhiteSpace(source) ? 25 : 45;

            if (ContainsAny(source, "captain", "prefect", "leader", "committee", "mentor")) score += 35;
            if (profile.ActivityPreference == "Team") score += 10;
            if (profile.SocialPreference == "Extroverted") score += 10;

            return Clamp(score);
        }

        private int ResolveActivityScore(StudentProfile profile)
        {
            var items = CountCsv(profile.Sports) + CountCsv(profile.ClubsAndSocieties) + CountCsv(profile.CulturalActivities);
            var score = Math.Min(95, 20 + (items * 15));

            if (profile.ActivityPreference == "Team") score += 5;
            return Clamp(score);
        }

        private IEnumerable<string> ResolveTraits(StudentProfile profile)
        {
            var traits = new List<string>();

            if (profile.EnvironmentPreference == "Quiet") traits.Add("Quiet-focused");
            if (profile.RoutinePreference == "WakeEarly") traits.Add("Disciplined");
            if (!string.IsNullOrWhiteSpace(profile.AcademicInterests)) traits.Add("Academic");
            if (!string.IsNullOrWhiteSpace(profile.Sports)) traits.Add("Sport-oriented");
            if (!string.IsNullOrWhiteSpace(profile.CulturalActivities)) traits.Add("Cultural");
            if (profile.LeadershipScore >= 70) traits.Add("Leadership");
            if (profile.MedicalAccommodationRequired || profile.AccessibilityRequired) traits.Add("Support-aware");

            return traits.Any() ? traits : new List<string> { "Balanced" };
        }

        private int CountCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            return value.Split(',')
                .Select(x => x.Trim())
                .Count(x => x.Length > 0);
        }

        private bool ContainsAny(string source, params string[] words)
        {
            if (string.IsNullOrWhiteSpace(source))
                return false;

            return words.Any(word => source.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private int Clamp(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }
}
