using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    public class StudentRoomCompatibilityService
    {
        private readonly DBContextClass db;
        private readonly CompatibilityScoringService bedScoringService;

        public StudentRoomCompatibilityService()
            : this(new DBContextClass(), new CompatibilityScoringService())
        {
        }

        public StudentRoomCompatibilityService(DBContextClass context, CompatibilityScoringService scoringService)
        {
            db = context ?? new DBContextClass();
            bedScoringService = scoringService ?? new CompatibilityScoringService();
        }

        public RoomCompatibilityResult Evaluate(StudentProfile incomingProfile, Bed bed, Room room, Residence residence, List<Student> occupants)
        {
            var bedScore = bedScoringService.ScoreBed(incomingProfile, bed, room, residence, occupants);
            var result = new RoomCompatibilityResult
            {
                BedId = bed.BedId,
                RoomId = room.RoomId,
                ResidenceId = residence.ResidenceId,
                ResidenceName = residence.Name,
                RoomNumber = room.RoomNumber,
                BedNumber = bed.BedNumber,
                ScoreDetails = bedScore.ScoreBreakdown.ToList()
            };

            if (bedScore.IsDisqualified)
            {
                result.CompatibilityScore = 0;
                result.Confidence = 1;
                result.Warnings.Add(bedScore.DisqualificationReason);
                return result;
            }

            foreach (var occupant in occupants)
            {
                var occupantProfile = db.StudentProfiles
                    .Include(p => p.Student)
                    .FirstOrDefault(p => p.StudentId == occupant.StudentId);

                result.OccupantBreakdown.Add(EvaluateOccupant(incomingProfile, occupant, occupantProfile));
            }

            var occupantScore = result.OccupantBreakdown.Any()
                ? result.OccupantBreakdown.Average(o => o.CompatibilityScore)
                : 85;

            result.CompatibilityScore = Math.Round((bedScore.TotalScore * 0.55) + (occupantScore * 0.45), 2);
            result.Confidence = ResolveConfidence(incomingProfile, occupants, result);

            AddRoomReasonsAndWarnings(incomingProfile, room, residence, result);

            if (result.CompatibilityScore < 45)
                result.Warnings.Add("Low compatibility room. Do not recommend unless no stronger room is available.");
            else if (result.CompatibilityScore < 60)
                result.Warnings.Add("Moderate compatibility risk. Review before allocating.");

            return result;
        }

        private OccupantCompatibilityBreakdown EvaluateOccupant(StudentProfile incoming, Student occupant, StudentProfile occupantProfile)
        {
            var result = new OccupantCompatibilityBreakdown
            {
                StudentId = occupant.StudentId,
                StudentName = occupant.Name
            };

            if (occupantProfile == null)
            {
                result.CompatibilityScore = 55;
                result.Warnings.Add("Existing occupant profile is incomplete.");
                return result;
            }

            double totalWeight = 0;
            double weightedSum = 0;

            void Add(double weight, double score, string positiveReason, string warning)
            {
                var value = Math.Max(0, Math.Min(1, score));
                weightedSum += weight * value;
                totalWeight += weight;

                if (value >= 0.75 && !string.IsNullOrWhiteSpace(positiveReason))
                    result.Reasons.Add(positiveReason);
                else if (value <= 0.45 && !string.IsNullOrWhiteSpace(warning))
                    result.Warnings.Add(warning);
            }

            Add(12, CsvOverlapScore(incoming.AcademicInterests, occupantProfile.AcademicInterests), "Academic interests aligned", "Academic interests differ");
            Add(10, TextMatchScore(incoming.LearningStyle, occupantProfile.LearningStyle), "Similar learning style", "Different learning styles");
            Add(12, PersonalityScore(incoming, occupantProfile), "Compatible personalities", "Personality mismatch");
            Add(8, NumericCloseness(incoming.LeadershipScore, occupantProfile.LeadershipScore), "Leadership compatible", "Leadership balance may need review");
            Add(10, CsvOverlapScore($"{incoming.Sports},{incoming.ClubsAndSocieties},{incoming.CulturalActivities}", $"{occupantProfile.Sports},{occupantProfile.ClubsAndSocieties},{occupantProfile.CulturalActivities}"), "Similar extracurricular interests", "Few shared extracurricular interests");
            Add(10, StudyHabitScore(incoming, occupantProfile), "Similar study habits", "Study habits differ");
            Add(8, TextMatchScore(incoming.RoutinePreference, occupantProfile.RoutinePreference), "Similar daily routine", "Morning/evening routine mismatch");
            Add(10, BehaviourScore(incoming.StudentId, occupant.StudentId, result), "No known behavioural conflict", "Previous conflict exists");
            Add(8, MedicalCompatibilityScore(incoming, occupantProfile), "Medical needs are compatible", "Medical or accessibility needs may conflict");
            Add(12, AgeGradeScore(incoming, occupantProfile), "Same grade or close age", "Large age or grade gap");

            result.CompatibilityScore = totalWeight > 0
                ? Math.Round((weightedSum / totalWeight) * 100, 2)
                : 50;

            return result;
        }

        private void AddRoomReasonsAndWarnings(StudentProfile incoming, Room room, Residence residence, RoomCompatibilityResult result)
        {
            if (room.Capacity > 0)
            {
                var availableRatio = (room.Capacity - room.OccupiedBeds) / (double)room.Capacity;
                if (availableRatio >= 0.25) result.Reasons.Add("Good occupancy balance");
                if (availableRatio < 0.15) result.Warnings.Add("Room is nearly full");
            }

            if (!string.IsNullOrWhiteSpace(residence.GradeCategory) && residence.GradeCategory == incoming.Grade)
                result.Reasons.Add("Same grade residence match");

            if (incoming.StudyStyle == "Quiet" && room.IsQuietStudyRoom)
                result.Reasons.Add("Quiet study room supports student study style");

            if (incoming.MedicalAccommodationRequired && !residence.NearMedicalFacility && !residence.NearHouseMasterOffice)
                result.Warnings.Add("Medical support is not close to this residence");

            if (incoming.AccessibilityRequired && !room.IsWheelchairAccessible)
                result.Warnings.Add("Accessibility requirement not fully supported by room");

            if (result.OccupantBreakdown.Any(o => o.Warnings.Any(w => w.IndexOf("conflict", StringComparison.OrdinalIgnoreCase) >= 0)))
                result.Warnings.Add("Behaviour conflict with an existing occupant");

            foreach (var reason in result.OccupantBreakdown.SelectMany(o => o.Reasons).GroupBy(r => r).OrderByDescending(g => g.Count()).Take(4).Select(g => g.Key))
                result.Reasons.Add(reason);

            result.Reasons = result.Reasons.Distinct().ToList();
            result.Warnings = result.Warnings.Distinct().ToList();
        }

        private double ResolveConfidence(StudentProfile incoming, List<Student> occupants, RoomCompatibilityResult result)
        {
            var completedProfileCount = occupants.Count(o => db.StudentProfiles.Any(p => p.StudentId == o.StudentId && p.IsProfileComplete));
            var dataCoverage = occupants.Any() ? completedProfileCount / (double)occupants.Count : 0.85;
            var incomingCoverage = incoming.IsProfileComplete ? 1.0 : 0.6;
            var warningPenalty = Math.Min(0.25, result.Warnings.Count * 0.04);

            return Math.Round(Math.Max(0.45, Math.Min(0.98, (dataCoverage * 0.55) + (incomingCoverage * 0.45) - warningPenalty)), 2);
        }

        private double CsvOverlapScore(string left, string right)
        {
            var a = SplitToSet(left);
            var b = SplitToSet(right);
            if (!a.Any() && !b.Any()) return 0.65;
            if (!a.Any() || !b.Any()) return 0.45;
            return a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count() / (double)Math.Max(a.Count, b.Count);
        }

        private double TextMatchScore(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return 0.6;
            return left.Equals(right, StringComparison.OrdinalIgnoreCase) ? 1 : 0.45;
        }

        private double PersonalityScore(StudentProfile left, StudentProfile right)
        {
            var score = 0.5;
            if (left.EnvironmentPreference == right.EnvironmentPreference) score += 0.2;
            if (left.StudyPreference == right.StudyPreference) score += 0.15;
            if (left.SocialPreference == right.SocialPreference || left.SocialPreference == "Balanced" || right.SocialPreference == "Balanced") score += 0.15;
            return score;
        }

        private double NumericCloseness(int left, int right)
        {
            return Math.Max(0, 1 - (Math.Abs(left - right) / 100.0));
        }

        private double StudyHabitScore(StudentProfile left, StudentProfile right)
        {
            var score = 0.4;
            if (left.StudyPreference == right.StudyPreference) score += 0.35;
            if (left.StudyStyle == right.StudyStyle) score += 0.25;
            return score;
        }

        private double BehaviourScore(int incomingStudentId, int occupantStudentId, OccupantCompatibilityBreakdown result)
        {
            var conflict = db.DisciplinaryConflicts.FirstOrDefault(c =>
                (c.StudentAId == incomingStudentId && c.StudentBId == occupantStudentId) ||
                (c.StudentBId == incomingStudentId && c.StudentAId == occupantStudentId));

            if (conflict == null)
                return 1;

            result.Warnings.Add("Previous conflict: " + conflict.Reason);
            return 0;
        }

        private double MedicalCompatibilityScore(StudentProfile left, StudentProfile right)
        {
            if ((left.MedicalAccommodationRequired || left.AccessibilityRequired) &&
                (right.MedicalAccommodationRequired || right.AccessibilityRequired))
                return 0.8;

            return 1;
        }

        private double AgeGradeScore(StudentProfile left, StudentProfile right)
        {
            if (!string.IsNullOrWhiteSpace(left.Grade) && left.Grade == right.Grade)
                return 1;

            var leftAge = left.CalculateAge();
            var rightAge = right.CalculateAge();
            if (leftAge == 0 || rightAge == 0)
                return 0.6;

            return Math.Max(0, 1 - (Math.Abs(leftAge - rightAge) / 6.0));
        }

        private HashSet<string> SplitToSet(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return new HashSet<string>();

            return new HashSet<string>(
                csv.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
