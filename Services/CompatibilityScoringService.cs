using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;

namespace Michaelhouse.Services
{
    public class RoomScoreResult
    {
        public Room Room { get; set; }
        public Residence Residence { get; set; }
        public int TotalScore { get; set; }
        public List<string> ScoreBreakdown { get; set; } = new List<string>();
        public bool IsDisqualified { get; set; }
        public string DisqualificationReason { get; set; }
    }

    public class CompatibilityScoringService
    {
        private DBContextClass db = DbContextFactory.Create();

        // New intelligent scoring implementing weighted factors with audit storage
        public RoomScoreResult ScoreRoom(StudentProfile profile, Room room, Residence residence,
            List<Student> currentOccupants)
        {
            var result = new RoomScoreResult { Room = room, Residence = residence };

            // ---- Rule 10 — capacity (mandatory, disqualifying) ----
            if (room.OccupiedBeds >= room.Capacity || room.IsFull)
            {
                result.IsDisqualified = true;
                result.DisqualificationReason = "Room is at full capacity.";
                return result;
            }

            // Implement weighted scoring factors to compute a final score 0..100
            double totalWeight = 0;
            double weightedSum = 0;

            // Helper to add factor
            void AddFactor(double weight, double normalizedValue, string explanation)
            {
                weightedSum += weight * normalizedValue;
                totalWeight += weight;
                result.ScoreBreakdown.Add($"{explanation} (w={weight} v={normalizedValue:F2})");
            }

            // 1) Same Grade (30%) — full match if room/residence grade category matches student grade
            double sameGradeWeight = 30;
            double sameGradeVal = 0;
            if (!string.IsNullOrEmpty(residence.GradeCategory) && !string.IsNullOrEmpty(profile.Grade))
            {
                sameGradeVal = residence.GradeCategory == profile.Grade ? 1.0 : 0.0;
            }
            AddFactor(sameGradeWeight, sameGradeVal, $"SameGrade: residence={residence.GradeCategory} student={profile.Grade}");

            // 2) Occupancy Balance (20%) — prefer rooms that keep occupancy balanced (less deviation)
            double occWeight = 20;
            double occVal = 0;
            if (room.Capacity > 0)
            {
                // normalized as (1 - abs(current occupancy ratio - target ratio)), target ratio = 0.8 (80% ideal)
                double currentRatio = room.OccupiedBeds / (double)room.Capacity;
                double target = 0.8;
                occVal = Math.Max(0, 1 - Math.Abs(currentRatio - target));
            }
            AddFactor(occWeight, occVal, $"OccupancyBalance: occupied={room.OccupiedBeds}/{room.Capacity}");

            // 3) Age Compatibility (15%) — measure closeness to average occupant age
            double ageWeight = 15;
            double ageVal = 0;
            var studentAge = profile.CalculateAge();
            if (studentAge > 0)
            {
                var occAges = currentOccupants.Select(o => db.StudentProfiles.Find(o.StudentId)?.CalculateAge() ?? 0).Where(a => a > 0).ToList();
                if (occAges.Any())
                {
                    var avg = occAges.Average();
                    var diff = Math.Abs(studentAge - avg);
                    // assume max meaningful diff = 10 years
                    ageVal = Math.Max(0, 1 - (diff / 10.0));
                }
                else
                {
                    ageVal = 0.5; // neutral for empty room
                }
            }
            AddFactor(ageWeight, ageVal, $"AgeCompatibility: studentAge={studentAge}");

            // 4) Behaviour Compatibility (10%) — penalise known disciplinary conflicts
            double behaviourWeight = 10;
            double behaviourVal = 1.0;
            var conflictIds = db.DisciplinaryConflicts
                .Where(c => c.StudentAId == profile.StudentId || c.StudentBId == profile.StudentId)
                .Select(c => c.StudentAId == profile.StudentId ? c.StudentBId : c.StudentAId)
                .ToList();
            if (conflictIds.Any() && currentOccupants.Any(o => conflictIds.Contains(o.StudentId)))
            {
                behaviourVal = 0.0; // disqualify effectively
            }
            AddFactor(behaviourWeight, behaviourVal, $"BehaviourCompatibility: conflictsWithRoom={conflictIds.Count}");

            // 5) Medical Requirements (10%) — must satisfy medical constraints
            double medicalWeight = 10;
            double medicalVal = 1.0;
            if (profile.MedicalAccommodationRequired)
            {
                medicalVal = (residence.NearMedicalFacility || residence.NearHouseMasterOffice || room.IsQuietStudyRoom) ? 1.0 : 0.0;
            }
            AddFactor(medicalWeight, medicalVal, $"MedicalAccommodation: required={profile.MedicalAccommodationRequired}");

            // 6) Leadership Distribution (5%) — prefer distributing student leaders across rooms
            double leadershipWeight = 5;
            double leadershipVal = 1.0;
            // if student is marked as leader in profile (using ClubsAndSocieties containing 'leader' or similar), reduce value if room already has leaders
            bool isLeader = !string.IsNullOrWhiteSpace(profile.ClubsAndSocieties) && profile.ClubsAndSocieties.IndexOf("leader", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isLeader)
            {
                // count occupants with leader flag
                int leaders = currentOccupants.Count(o => (db.StudentProfiles.Find(o.StudentId)?.ClubsAndSocieties ?? string.Empty).IndexOf("leader", StringComparison.OrdinalIgnoreCase) >= 0);
                leadershipVal = leaders == 0 ? 1.0 : Math.Max(0, 1.0 - (leaders / 3.0));
            }
            AddFactor(leadershipWeight, leadershipVal, $"LeadershipDistribution: studentLeader={isLeader}");

            // 7) Maintenance Status (5%) — deprioritise rooms needing maintenance
            double maintenanceWeight = 5;
            double maintenanceVal = room.NeedsMaintenance ? 0.0 : 1.0;
            AddFactor(maintenanceWeight, maintenanceVal, $"MaintenanceStatus: needsMaintenance={room.NeedsMaintenance}");

            // 8) Distance to House Master's Office (5%) — prefer closer rooms
            double distanceWeight = 5;
            double distanceVal = 0.5;
            // If Residence has NearHouseMasterOffice flag, award full points; otherwise neutral.
            distanceVal = residence.NearHouseMasterOffice ? 1.0 : 0.5;
            AddFactor(distanceWeight, distanceVal, $"DistanceToHouseMaster: near={residence.NearHouseMasterOffice}");

            // Combine into 0..100 score
            double normalized = (totalWeight > 0) ? (weightedSum / totalWeight) : 0.0;
            double finalScore = Math.Round(normalized * 100.0, 2);

            result.TotalScore = (int)Math.Round(finalScore);
            result.ScoreBreakdown.Add($"FinalScore={finalScore}");

            // Persist audit record for this scoring
            try
            {
                var audit = new RoomScoreAudit
                {
                    RoomId = room.RoomId,
                    ResidenceId = residence.ResidenceId,
                    StudentId = profile.StudentId,
                    Score = finalScore,
                    CalculatedAt = DateTime.Now,
                    Details = string.Join(" | ", result.ScoreBreakdown)
                };
                db.RoomScoreAudits.Add(audit);
                db.SaveChanges();
            }
            catch { /* swallow audit errors to avoid blocking allocation */ }

            return result;
        }

        private string GetGradeBand(string grade)
        {
            switch (grade)
            {
                case "8":
                case "9":
                    return "Junior";
                case "10":
                    return "Middle";
                case "11":
                case "12":
                    return "Senior";
                default:
                    return null;
            }
        }

        private HashSet<string> SplitToSet(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return new HashSet<string>();

            var items = csv.Split(',')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            return new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<int> SplitToIntSet(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return new HashSet<int>();

            var nums = csv.Split(',')
                .Select(x => int.TryParse(x.Trim(), out int n) ? n : -1)
                .Where(n => n > 0)
                .ToList();

            return new HashSet<int>(nums);
        }
    }
}