using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // Which house (residence) each student lives in.
    //
    // The residence module records this as an active
    // ResidenceAllocation. Student.ResidenceId is older and usually
    // empty, so it is only used for a student who has no active
    // allocation. If a student somehow has several active
    // allocations, the most recent one counts.
    //
    // Used for the daily menu (DemandService, MenuCoverageService,
    // kitchen plan) and event invitations (EventRsvpService), so they
    // all agree on who lives where.
    // ============================================================

    public class StudentHouseService
    {
        private readonly DBContextClass _db;

        public StudentHouseService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // StudentId → ResidenceId for every student who has a house
        public Dictionary<int, int> HouseByStudent()
        {
            var result = _db.ResidenceAllocations
                .Where(a => a.IsActive)
                .Select(a => new { a.StudentId, a.ResidenceId, a.AllocatedAt })
                .ToList()
                .GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AllocatedAt).First().ResidenceId);

            // Fallback for students without an active allocation
            foreach (var s in _db.Students
                                 .Where(s => s.ResidenceId.HasValue)
                                 .Select(s => new { s.StudentId, s.ResidenceId })
                                 .ToList())
            {
                if (!result.ContainsKey(s.StudentId)) result[s.StudentId] = s.ResidenceId.Value;
            }

            return result;
        }

        public int? HouseOf(Student student)
        {
            if (student == null) return null;

            int studentId = student.StudentId;
            var allocation = _db.ResidenceAllocations
                .Where(a => a.StudentId == studentId && a.IsActive)
                .OrderByDescending(a => a.AllocatedAt)
                .FirstOrDefault();

            return allocation != null ? allocation.ResidenceId : student.ResidenceId;
        }

        // Students living in any of these houses
        public List<int> StudentIdsInHouses(ICollection<int> houses)
        {
            return HouseByStudent()
                .Where(kv => houses.Contains(kv.Value))
                .Select(kv => kv.Key)
                .ToList();
        }

        // Active students who live in a house — the boarders the
        // cafeteria caters for every day
        public List<int> BoardingStudentIds()
        {
            var housed = HouseByStudent();
            var housedIds = housed.Keys.ToList();

            return _db.Students
                .Where(s => s.IsActive && housedIds.Contains(s.StudentId))
                .Select(s => s.StudentId)
                .ToList();
        }
    }
}
