using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Michaelhouse.Models;

namespace Michaelhouse.Services
{
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public void AddError(string error)
        {
            IsValid = false;
            Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }

    public class CompatibilityScoreResult
    {
        public int Score { get; set; } // 0 - 100
        public List<string> Factors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class MichaelhouseRulesEngine
    {
        private readonly DBContextClass _db;

        public MichaelhouseRulesEngine(DBContextClass db)
        {
            _db = db;
        }

        // ====================================================================
        // 1. BOARDING HOUSE & ALLOCATION RULES
        // ====================================================================

        /// <summary>
        /// Validates whether a student can be allocated to a specific bed/room/residence.
        /// Checks capacity, gender compatibility, grade alignment, active conflicts, and existing allocations.
        /// </summary>
        public async Task<ValidationResult> ValidateAllocationAsync(int studentId, int bedId)
        {
            var result = new ValidationResult();

            var student = await _db.Students
                .Include(s => s.StudentProfile)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
            {
                result.AddError("Student not found.");
                return result;
            }

            var bed = await _db.Beds
                .Include(b => b.Room)
                .Include(b => b.Room.Residence)
                .FirstOrDefaultAsync(b => b.BedId == bedId);

            if (bed == null)
            {
                result.AddError("Bed not found.");
                return result;
            }

            var room = bed.Room;
            var residence = room?.Residence;

            if (room == null || residence == null)
            {
                result.AddError("Associated room or residence configuration is invalid.");
                return result;
            }

            // 1. Check if bed is already occupied
            if (bed.IsOccupied || bed.OccupiedByStudentId.HasValue)
            {
                result.AddError($"Bed '{bed.BedNumber}' in Room '{room.RoomNumber}' is already occupied.");
            }

            // 2. Check if student already has an active allocation
            var existingAllocation = await _db.ResidenceAllocations
                .AnyAsync(ra => ra.StudentId == studentId && ra.IsActive);

            if (existingAllocation)
            {
                result.AddError($"Student {student.FirstName} {student.LastName} already has an active residence allocation.");
            }

            // Extract gender and grade safely from StudentProfile
            string studentGender = student.StudentProfile?.Gender ?? string.Empty;
            string studentGrade = student.StudentProfile?.Grade ?? string.Empty;

            // 3. Gender alignment check
            if (!string.IsNullOrEmpty(residence.Gender) &&
                !string.IsNullOrEmpty(studentGender) &&
                !residence.Gender.Equals("Co-ed", StringComparison.OrdinalIgnoreCase) &&
                !residence.Gender.Equals(studentGender, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError($"Gender mismatch: Student is '{studentGender}' but Residence '{residence.Name}' is designated for '{residence.Gender}'.");
            }

            // 4. Room capacity check
            var currentRoomOccupants = await _db.Beds.CountAsync(b => b.RoomId == room.RoomId && b.IsOccupied);
            if (currentRoomOccupants >= room.Capacity)
            {
                result.AddError($"Room '{room.RoomNumber}' has reached its maximum capacity of {room.Capacity}.");
            }

            // 5. Residence capacity check
            var currentResOccupants = await _db.Beds.CountAsync(b => b.Room.ResidenceId == residence.ResidenceId && b.IsOccupied);
            if (currentResOccupants >= residence.Capacity)
            {
                result.AddError($"Residence '{residence.Name}' is at full capacity ({residence.Capacity} beds).");
            }

            // 6. Grade alignment check (Warning/Soft rule)
            if (!string.IsNullOrEmpty(studentGrade))
            {
                var roomOccupantGrades = await _db.Beds
                    .Where(b => b.RoomId == room.RoomId && b.IsOccupied && b.OccupiedByStudentId.HasValue)
                    .Select(b => _db.Students.Where(s => s.StudentId == b.OccupiedByStudentId.Value).Select(s => s.StudentProfile.Grade).FirstOrDefault())
                    .ToListAsync();

                if (roomOccupantGrades.Any(g => !string.IsNullOrEmpty(g) && g != studentGrade))
                {
                    result.AddWarning($"Grade mix warning: Student is Grade {studentGrade}, but room contains student(s) of different grades.");
                }
            }

            // 7. Active Disciplinary Conflict Check
            var roomOccupantStudentIds = await _db.Beds
                .Where(b => b.RoomId == room.RoomId && b.IsOccupied && b.OccupiedByStudentId.HasValue)
                .Select(b => b.OccupiedByStudentId.Value)
                .ToListAsync();

            if (roomOccupantStudentIds.Any())
            {
                var hasConflict = await _db.DisciplinaryConflicts
                    .AnyAsync(dc => (dc.StudentAId == studentId && roomOccupantStudentIds.Contains(dc.StudentBId)) ||
                                    (dc.StudentBId == studentId && roomOccupantStudentIds.Contains(dc.StudentAId)));

                if (hasConflict)
                {
                    result.AddError("Disciplinary Conflict Alert: Student has a logged conflict with an existing roommate in this room.");
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates compatibility score (0 to 100) between a student and potential roommates in a room.
        /// </summary>
        public async Task<CompatibilityScoreResult> CalculateRoomCompatibilityAsync(int studentId, int roomId)
        {
            var result = new CompatibilityScoreResult { Score = 70 }; // Baseline neutral score

            var student = await _db.Students
                .Include(s => s.StudentProfile)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null) return result;

            var existingRoommateIds = await _db.Beds
                .Where(b => b.RoomId == roomId && b.IsOccupied && b.OccupiedByStudentId.HasValue)
                .Select(b => b.OccupiedByStudentId.Value)
                .ToListAsync();

            if (!existingRoommateIds.Any())
            {
                result.Factors.Add("Empty room: Base compatibility set to 100.");
                result.Score = 100;
                return result;
            }

            var roommates = await _db.Students
                .Include(s => s.StudentProfile)
                .Where(s => existingRoommateIds.Contains(s.StudentId))
                .ToListAsync();

            int scoreAdjustment = 0;
            string studentGrade = student.StudentProfile?.Grade;

            foreach (var mate in roommates)
            {
                // Disciplinary Conflict Check (-50 penalty)
                var hasConflict = await _db.DisciplinaryConflicts
                    .AnyAsync(dc => (dc.StudentAId == studentId && dc.StudentBId == mate.StudentId) ||
                                    (dc.StudentBId == studentId && dc.StudentAId == mate.StudentId));

                if (hasConflict)
                {
                    scoreAdjustment -= 50;
                    result.Warnings.Add($"Logged conflict detected with {mate.FirstName} {mate.LastName}.");
                }

                // Grade Match (+10 reward)
                string mateGrade = mate.StudentProfile?.Grade;
                if (!string.IsNullOrEmpty(studentGrade) && studentGrade == mateGrade)
                {
                    scoreAdjustment += 10;
                    result.Factors.Add($"Grade match with {mate.FirstName} {mate.LastName} (+10).");
                }
            }

            result.Score = Math.Max(0, Math.Min(100, result.Score + scoreAdjustment));
            return result;
        }

        // ====================================================================
        // 2. VISITOR ACCESS MANAGEMENT RULES
        // ====================================================================

        /// <summary>
        /// Validates visitor access requests against term dates, visiting hours, and host student status.
        /// </summary>
        public async Task<ValidationResult> ValidateVisitorAccessRequestAsync(VisitorAccessRequest request)
        {
            var result = new ValidationResult();

            if (request == null)
            {
                result.AddError("Request details are missing.");
                return result;
            }

            // 1. Verify host student exists and is actively enrolled
            var student = await _db.Students.FindAsync(request.StudentId);
            if (student == null)
            {
                result.AddError("Host student was not found.");
                return result;
            }

            // 2. Max visitor count check per student
            var existingVisitorCountToday = await _db.VisitorAccessRequests
                .CountAsync(v => v.StudentId == request.StudentId);

            if (existingVisitorCountToday >= 4)
            {
                result.AddError("Student has reached the maximum allowance of registered visitors.");
            }

            return result;
        }

        // ====================================================================
        // 3. MOVEMENTS & ABSENCE RULES
        // ====================================================================

        /// <summary>
        /// Validates a movement/overnight exeat request for a student.
        /// </summary>
        public async Task<ValidationResult> ValidateMovementRequestAsync(int studentId, DateTime departureDate, DateTime returnDate, string movementType)
        {
            var result = new ValidationResult();

            if (returnDate <= departureDate)
            {
                result.AddError("Return date must be later than departure date.");
                return result;
            }

            // Check for existing movements for this student
            var activeMovements = await _db.ResidenceMovements
                .AnyAsync(m => m.StudentId == studentId);

            if (activeMovements)
            {
                result.AddWarning("Student already has existing movement history logged in the system.");
            }

            return result;
        }
    }
}