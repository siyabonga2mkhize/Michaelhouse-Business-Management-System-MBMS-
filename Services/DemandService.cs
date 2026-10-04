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
    // Portions = active students + staff meals + sports uplift.
    //
    // - Active students  : students with at least one active sport
    //                      status on that date. Students with no
    //                      sport records are counted as active.
    // - Staff meals      : constant number, same for every meal.
    // - Sports uplift    : +1 portion for each active student who
    //                      has a scheduled match on that day.
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
            var students = _db.Students.ToList();

            var sportStatuses = _db.StudentSportStatuses.ToList();

            // FIX: AddDays can't be translated to SQL by EF6.
            // Compute the upper bound in C# first, then use the variable.
            var upperBound = end.Date.AddDays(1);
            var events = _db.SportEvents
                .Where(e => e.ScheduledDate >= start
                         && e.ScheduledDate < upperBound)
                .ToList();

            // Group sport statuses by student
            var statusByStudent = sportStatuses
                .GroupBy(s => s.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Group active students by the house they live in
            // (residence allocation — see StudentHouseService)
            var houseByStudent = new StudentHouseService(_db).HouseByStudent();
            var studentsByResidence = students
                .Where(s => s.IsActive && houseByStudent.ContainsKey(s.StudentId))
                .GroupBy(s => houseByStudent[s.StudentId])
                .ToDictionary(g => g.Key, g => g.ToList());

            // Residence name lookup
            var residenceNames = _db.Residences
                .ToDictionary(r => r.ResidenceId, r => r.Name);

            // ── Loop through days and meals ──────────────────────────
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                var dayEvents = events
                    .Where(e => e.ScheduledDate.Date == date.Date)
                    .Where(e => !e.IsCancelled)
                    .Where(e => string.IsNullOrEmpty(e.Status)
                             || e.Status == "Scheduled")
                    .ToList();

                // FIX: A fixture like "Rugby vs Hilton College" is
                // school-wide — every rugby player plays, not just the
                // house nominally listed on the event. So we collect
                // the set of sports playing today and match on that,
                // ignoring ResidenceId entirely.
                var todaysSports = new HashSet<string>(
                    dayEvents.Select(e => e.Sport)
                             .Where(s => !string.IsNullOrEmpty(s)));

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

                        int active = 0;
                        int unavailable = 0;

                        foreach (var student in houseStudents)
                        {
                            if (IsStudentActiveOn(student.StudentId, date, statusByStudent))
                                active++;
                            else
                                unavailable++;
                        }

                        // Match players from this house today.
                        // +1 uplift per active student whose sport is
                        // playing today, regardless of house.
                        int matchPlayers = 0;
                        foreach (var student in houseStudents)
                        {
                            if (!IsStudentActiveOn(student.StudentId, date, statusByStudent))
                                continue;

                            var sports = GetSportsForStudent(student.StudentId, statusByStudent);
                            if (sports.Any(s => todaysSports.Contains(s)))
                                matchPlayers++;
                        }

                        demand.Houses.Add(new HouseBreakdown
                        {
                            ResidenceId = residenceId,
                            ResidenceName = residenceNames.ContainsKey(residenceId)
                                ? residenceNames[residenceId]
                                : "Unknown",
                            ActiveStudents = active,
                            Unavailable = unavailable,
                            MatchPlayers = matchPlayers
                        });

                        demand.HousePortions += active;
                        demand.SportsUplift += matchPlayers;
                    }

                    // ── Event labels for display ─────────────────────
                    foreach (var evt in dayEvents)
                    {
                        var label = evt.Sport;
                        if (!string.IsNullOrEmpty(evt.Opponent))
                            label += " vs " + evt.Opponent;

                        demand.EventsThisMeal.Add(label);
                    }

                    results.Add(demand);
                }
            }

            return results;
        }

        // ── Helpers ──────────────────────────────────────────────────

        private bool IsStudentActiveOn(
            int studentId,
            DateTime date,
            Dictionary<int, List<StudentSportStatus>> statusByStudent)
        {
            if (!statusByStudent.ContainsKey(studentId)) return true;
            var statuses = statusByStudent[studentId];
            if (statuses == null || statuses.Count == 0) return true;

            // Active if ANY of their sports is available today
            foreach (var s in statuses)
            {
                if (IsActiveOn(s, date)) return true;
            }
            return false;
        }

        private bool IsActiveOn(StudentSportStatus status, DateTime date)
        {
            // Auto-recover: if the return date has passed, treat as active
            if (status.UnavailableUntil.HasValue
                && status.UnavailableUntil.Value.Date <= date.Date)
            {
                return true;
            }

            return status.IsActive;
        }

        private List<string> GetSportsForStudent(
            int studentId,
            Dictionary<int, List<StudentSportStatus>> statusByStudent)
        {
            if (!statusByStudent.ContainsKey(studentId))
                return new List<string>();

            return statusByStudent[studentId]
                .Select(s => s.Sport)
                .Distinct()
                .ToList();
        }
    }
}