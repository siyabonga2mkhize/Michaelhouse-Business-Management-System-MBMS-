using System;
using System.Collections.Generic;
using System.Linq;
using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC12 rework — Computes per-day, per-meal portion demand.
    //
    // Portions = students + staff meals + sports uplift.
    //
    // - Students         : every active student living in a house
    //                      (residence allocation). A student the
    //                      Coach marked unavailable (injury, leave)
    //                      still eats — they are counted, and shown
    //                      as "unavailable" for information only.
    // - Staff meals      : constant number, same for every meal.
    // - Sports uplift    : +1 portion for each student playing in a
    //                      scheduled match that day — their sport,
    //                      available for it, and in the match's house
    //                      if it names one (StudentSportService).
    //
    // This service is pure reads. It does not modify the database.
    // ============================================================

    public class DemandService
    {
        private readonly DBContextClass _db;

        public DemandService(DBContextClass db)
        {
            _db = db;
        }

        // Meal slots the service computes for.
        private static readonly string[] MealSlots = { "Breakfast", "Lunch", "Dinner" };

        public List<MealDemand> ComputeRange(DateTime start, DateTime end, int staffMealsPerMeal)
        {
            if (end < start) end = start;

            var results = new List<MealDemand>();

            // ── Preload everything once ──────────────────────────────
            var students = _db.Students.Where(s => s.IsActive).ToList();

            // Group students by the house they live in
            // (residence allocation — see StudentHouseService)
            var houseByStudent = new StudentHouseService(_db).HouseByStudent();
            var studentsByResidence = students
                .Where(s => houseByStudent.ContainsKey(s.StudentId))
                .GroupBy(s => houseByStudent[s.StudentId])
                .ToDictionary(g => g.Key, g => g.ToList());

            // Residence name lookup
            var residenceNames = _db.Residences
                .ToDictionary(r => r.ResidenceId, r => r.Name);

            var calendar = new StudentSportService(_db).LoadCalendar(start, end);

            // ── Loop through days and meals ──────────────────────────
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                var dayEvents = calendar.EventsOn(date);
                var matches = dayEvents.Where(StudentSportService.IsMatch).ToList();

                // Students playing in any match today
                var matchPlayers = new HashSet<int>(matches.SelectMany(calendar.PlayersFor));

                foreach (var meal in MealSlots)
                {
                    var demand = new MealDemand
                    {
                        Date = date,
                        MealSlot = meal,
                        StaffPortions = staffMealsPerMeal
                    };

                    // ── Per-house breakdown ──────────────────────────
                    foreach (var kvp in studentsByResidence)
                    {
                        var residenceId = kvp.Key;
                        var houseStudents = kvp.Value;

                        int unavailable = houseStudents.Count(s => IsUnavailableOn(calendar, s.StudentId, date));
                        int players = houseStudents.Count(s => matchPlayers.Contains(s.StudentId));

                        demand.Houses.Add(new HouseBreakdown
                        {
                            ResidenceId = residenceId,
                            ResidenceName = residenceNames.ContainsKey(residenceId)
                                ? residenceNames[residenceId]
                                : "Unknown",
                            ActiveStudents = houseStudents.Count,
                            Unavailable = unavailable,
                            MatchPlayers = players
                        });

                        demand.HousePortions += houseStudents.Count;
                        demand.SportsUplift += players;
                    }

                    // ── Event labels for display ─────────────────────
                    foreach (var evt in dayEvents)
                    {
                        demand.EventsThisMeal.Add(StudentSportService.Label(evt));
                    }

                    results.Add(demand);
                }
            }

            return results;
        }

        // ── Helpers ──────────────────────────────────────────────────

        // Every sport the student plays is marked unavailable today
        // (information only — they still eat)
        private static bool IsUnavailableOn(SportsCalendar calendar, int studentId, DateTime date)
        {
            var statuses = calendar.StatusesOf(studentId);
            return statuses.Count > 0 && !statuses.Any(s => StudentSportService.IsAvailableOn(s, date));
        }
    }
}
