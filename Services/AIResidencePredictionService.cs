using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    /// <summary>
    /// Simple prediction engine for residence occupancy.
    /// - Predicts residences likely to become full
    /// - Predicts rooms likely to become empty
    /// - Estimates expected occupancy for next 30 days (rudimentary)
    /// - Produces textual recommendations (e.g., move students)
    ///
    /// Note: This is a heuristic-based placeholder designed to be
    /// replaced by statistical/ML models later. It is intentionally
    /// conservative and produces explainable output suitable for
    /// display on the Residence Dashboard.
    /// </summary>
    public class AIResidencePredictionService
    {
        private readonly DBContextClass _db;
        private readonly ResidenceAvailabilityService _availability;

        public AIResidencePredictionService() : this(new DBContextClass(), new ResidenceAvailabilityService()) { }
        public AIResidencePredictionService(DBContextClass db, ResidenceAvailabilityService availability)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
        }

        public class ResidencePrediction
        {
            public int ResidenceId { get; set; }
            public string ResidenceName { get; set; }
            public int CurrentOccupied { get; set; }
            public int Capacity { get; set; }
            public double LikelihoodFull { get; set; } // 0..1
            public string Explanation { get; set; }
        }

        public class RoomPrediction
        {
            public int RoomId { get; set; }
            public string RoomNumber { get; set; }
            public int CurrentOccupied { get; set; }
            public int Capacity { get; set; }
            public double LikelihoodEmpty { get; set; } // 0..1
            public string Explanation { get; set; }
        }

        public class OccupancyForecast
        {
            public int ResidenceId { get; set; }
            public string ResidenceName { get; set; }
            public List<(DateTime Date, int ExpectedOccupied)> DailyExpected { get; set; }
        }

        public class Recommendation
        {
            public string Text { get; set; }
        }

        // Predict residences that are likely to be full in next 30 days
        public List<ResidencePrediction> PredictResidencesLikelyToBecomeFull(int days = 30)
        {
            var residences = _db.Residences.Include(r => r.Rooms).ToList();
            var results = new List<ResidencePrediction>();

            foreach (var res in residences)
            {
                int occupied = res.OccupiedBeds;
                int capacity = res.Capacity;

                // simple heuristic: look at trend from last N allocation history entries
                var recent = _db.AIAllocationHistories
                    .Where(h => h.ResidenceId == res.ResidenceId)
                    .OrderByDescending(h => h.CreatedDate)
                    .Take(50)
                    .ToList();

                double trend = 0; // positive => increasing occupancy
                if (recent.Count >= 2)
                {
                    var earliest = recent.Last();
                    var latest = recent.First();
                    var daysSpan = (latest.CreatedDate - earliest.CreatedDate).TotalDays;
                    if (daysSpan > 0)
                    {
                        trend = (latest.Score - earliest.Score) / daysSpan; // not perfect but indicative
                    }
                }

                // baseline fullness ratio
                double ratio = capacity > 0 ? occupied / (double)capacity : 0;

                // likelihood: combine ratio and trend in a simple formula
                double likelihood = Math.Min(1.0, ratio + Math.Min(0.5, Math.Max(0, trend) * 30));

                var explanation = $"Current occupancy {occupied}/{capacity} ({Math.Round(ratio * 100)}%). Trend factor {Math.Round(trend, 3)}.";

                results.Add(new ResidencePrediction
                {
                    ResidenceId = res.ResidenceId,
                    ResidenceName = res.Name,
                    CurrentOccupied = occupied,
                    Capacity = capacity,
                    LikelihoodFull = Math.Round(likelihood, 3),
                    Explanation = explanation
                });
            }

            // return sorted by likelihood desc
            return results.OrderByDescending(r => r.LikelihoodFull).ToList();
        }

        // Predict rooms likely to become empty (e.g., students leaving via graduation/withdrawal)
        public List<RoomPrediction> PredictRoomsLikelyToBecomeEmpty(int days = 30)
        {
            var rooms = _db.Rooms.Include(r => r.Residence).ToList();
            var results = new List<RoomPrediction>();

            foreach (var room in rooms)
            {
                int occupied = room.OccupiedBeds;
                int capacity = room.Capacity;

                // Heuristic: rooms with many senior students and upcoming graduation are more likely to empty
                var occupants = _db.ResidenceAssignments.Where(a => a.RoomId == room.RoomId && a.IsActive).Select(a => a.Student).ToList();
                int seniorCount = 0;
                foreach (var s in occupants)
                {
                    if (s.GradeLevel >= 11) seniorCount++;
                }

                double likelihoodEmpty = 0;
                if (occupied == 0) likelihoodEmpty = 0;
                else
                {
                    double seniorRatio = occupied > 0 ? seniorCount / (double)occupied : 0;
                    likelihoodEmpty = Math.Min(1.0, seniorRatio * 0.8); // high senior ratio => more likely to empty
                }

                var seniorPercentage = occupied <= 0 ? 0 : (seniorCount / (double)occupied) * 100;
                var explanation = $"Senior ratio {Math.Round(seniorPercentage, 0)}% indicating potential departures.";

                results.Add(new RoomPrediction
                {
                    RoomId = room.RoomId,
                    RoomNumber = room.RoomNumber,
                    CurrentOccupied = occupied,
                    Capacity = capacity,
                    LikelihoodEmpty = Math.Round(likelihoodEmpty, 3),
                    Explanation = explanation
                });
            }

            return results.OrderByDescending(r => r.LikelihoodEmpty).ToList();
        }

        // Forecast expected occupancy for next N days (naive forecasting using recent average daily change)
        public List<OccupancyForecast> ForecastExpectedOccupancy(int days = 30)
        {
            var residences = _db.Residences.ToList();
            var forecasts = new List<OccupancyForecast>();

            foreach (var res in residences)
            {
                var recent = _db.AIAllocationHistories
                    .Where(h => h.ResidenceId == res.ResidenceId)
                    .OrderByDescending(h => h.CreatedDate)
                    .Take(60)
                    .ToList();

                double dailyChange = 0;
                if (recent.Count >= 2)
                {
                    // compute average daily change in score interpreted as occupancy change proxy
                    var pairs = recent.Zip(recent.Skip(1), (a, b) => new { New = a, Old = b }).ToList();
                    var changes = pairs.Select(p => (p.New.Score - p.Old.Score)).ToList();
                    dailyChange = changes.Any() ? changes.Average() : 0;
                }

                var list = new List<(DateTime Date, int ExpectedOccupied)>();
                int current = res.OccupiedBeds;
                for (int d = 0; d < days; d++)
                {
                    var date = DateTime.Today.AddDays(d);
                    // naive projection
                    current = (int)Math.Max(0, Math.Min(res.Capacity, Math.Round(current + dailyChange)));
                    list.Add((date, current));
                }

                forecasts.Add(new OccupancyForecast { ResidenceId = res.ResidenceId, ResidenceName = res.Name, DailyExpected = list });
            }

            return forecasts;
        }

        // Generate textual recommendations. These are suggestions to manual staff actions.
        public List<Recommendation> GenerateRecommendations(int days = 30)
        {
            var recs = new List<Recommendation>();

            var resPred = PredictResidencesLikelyToBecomeFull(days);
            var topAtRisk = resPred.Where(r => r.LikelihoodFull > 0.75).Take(3).ToList();

            foreach (var r in topAtRisk)
            {
                // find candidate students to move: low priority grade students (lower grade) in other residences
                var targetRes = _db.Residences.Find(r.ResidenceId);
                if (targetRes == null) continue;

                // Suggest moving a small number of students (e.g., 1-3) from most populated other residences of same grade
                var overloadedRooms = _db.Rooms.Where(room => room.ResidenceId == r.ResidenceId && room.OccupiedBeds >= room.Capacity - 1).ToList();
                int recommendCount = Math.Min(3, overloadedRooms.Sum(rr => Math.Max(0, rr.OccupiedBeds - (int)Math.Floor(rr.Capacity * 0.8))));
                if (recommendCount <= 0) continue;

                recs.Add(new Recommendation { Text = $"Move {recommendCount} students to {r.ResidenceName} to alleviate imbalance. Reason: {r.Explanation}" });
            }

            // Rooms likely to become empty: suggest consolidation if many rooms will be empty
            var roomPreds = PredictRoomsLikelyToBecomeEmpty(days);
            var likelyEmpty = roomPreds.Where(rp => rp.LikelihoodEmpty > 0.7).Take(5).ToList();
            foreach (var rp in likelyEmpty)
            {
                recs.Add(new Recommendation { Text = $"Room {rp.RoomNumber} is likely to become empty ({rp.LikelihoodEmpty * 100:F0}%). Consider consolidating occupants or repurposing." });
            }

            return recs;
        }
    }
}
