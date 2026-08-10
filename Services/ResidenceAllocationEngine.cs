using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Data.Entity;

namespace Michaelhouse.Services
{
    public class AllocationRecommendation
    {
        public int StudentId { get; set; }
        public List<RoomScoreResult> RankedRooms { get; set; }
        public RoomScoreResult BestMatch { get; set; }
        public List<RoomCompatibilityResult> TopRooms { get; set; }
    }

    public class ResidenceAllocationEngine
    {
        private DBContextClass db = new DBContextClass();
        private ResidenceAvailabilityService availabilityService = new ResidenceAvailabilityService();
        private CompatibilityScoringService scoringService = new CompatibilityScoringService();
        private StudentRoomCompatibilityService compatibilityService = new StudentRoomCompatibilityService();

        // -----------------------------------------------
        // Produces ranked recommendations WITHOUT allocating.
        // Powers the "AI Recommendation Screen".
        // -----------------------------------------------
        public AllocationRecommendation GetRecommendations(int studentId)
        {
            var profile = db.StudentProfiles.Find(studentId);

            if (profile == null)
                throw new InvalidOperationException(
                    "Student has no profile — complete their profile before running allocation.");

            var candidates = availabilityService.GetAvailableBedsWithOccupants();

            var topRooms = candidates
                .Select(c => compatibilityService.Evaluate(profile, c.Bed, c.Room, c.Residence, c.Occupants))
                .Where(r => r.CompatibilityScore > 0)
                .OrderBy(r => r.Warnings.Any(w => w.IndexOf("Low compatibility", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0)
                .ThenByDescending(r => r.CompatibilityScore)
                .ThenByDescending(r => r.Confidence)
                .Take(5)
                .ToList();

            if (!topRooms.Any())
                throw new InvalidOperationException(
                    "No eligible room found. All rooms are either full or fail a mandatory requirement (e.g. accessibility) for this student.");

            var scored = topRooms
                .Select(r => new BedScoreResult
                {
                    Bed = db.Beds.Find(r.BedId),
                    Room = db.Rooms.Find(r.RoomId),
                    Residence = db.Residences.Find(r.ResidenceId),
                    TotalScore = (int)Math.Round(r.CompatibilityScore),
                    ScoreBreakdown = r.ScoreDetails
                })
                .Cast<RoomScoreResult>()
                .ToList();

            return new AllocationRecommendation
            {
                StudentId = studentId,
                RankedRooms = scored,
                BestMatch = scored.First(),
                TopRooms = topRooms
            };
        }

        // -----------------------------------------------
        // Runs the full automatic allocation and commits it.
        // Called right after student admission, OR via the
        // Recommendation Screen when staff confirm a suggested room.
        // -----------------------------------------------
        public ResidenceAssignment AllocateStudent(int studentId, int? overrideRoomId = null)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Already has an active assignment? Don't double-allocate.
                    var existing = db.ResidenceAssignments
                        .FirstOrDefault(a => a.StudentId == studentId && a.IsActive);

                    if (existing != null)
                        throw new InvalidOperationException("Student already has an active residence assignment.");

                    Room chosenRoom;
                    Residence chosenResidence;
                    RoomCompatibilityResult recommended = null;

                    if (overrideRoomId.HasValue)
                    {
                        // Staff manually picked a room off the recommendation screen
                        chosenRoom = db.Rooms.Include(r => r.Residence)
                            .FirstOrDefault(r => r.RoomId == overrideRoomId.Value);

                        if (chosenRoom == null)
                            throw new InvalidOperationException("Selected room not found.");

                        if (chosenRoom.OccupiedBeds >= chosenRoom.Capacity || chosenRoom.IsFull)
                            throw new InvalidOperationException("Selected room is now full — please choose another.");

                        chosenResidence = chosenRoom.Residence;
                    }
                    else
                    {
                        var recommendation = GetRecommendations(studentId);
                        recommended = recommendation.TopRooms.FirstOrDefault();
                        chosenRoom = db.Rooms.Find(recommendation.BestMatch.Room.RoomId);
                        chosenResidence = db.Residences.Find(recommendation.BestMatch.Residence.ResidenceId);
                    }

                    var bed = recommended != null
                        ? db.Beds.FirstOrDefault(b => b.BedId == recommended.BedId && !b.IsOccupied && (b.Status == null || b.Status == "Available"))
                        : availabilityService.GetFirstAvailableBed(chosenRoom.RoomId);

                    if (bed == null)
                        throw new InvalidOperationException("No available bed in the selected room.");

                    // ---- Reserve the bed ----
                    bed.IsOccupied = true;
                    bed.Status = "Occupied";
                    bed.OccupiedByStudentId = studentId;

                    // ---- Update room ----
                    chosenRoom.OccupiedBeds++;
                    chosenRoom.IsFull = chosenRoom.OccupiedBeds >= chosenRoom.Capacity;

                    // ---- Update residence ----
                    chosenResidence.OccupiedBeds++;

                    // ---- Create assignment ----
                    var assignment = new ResidenceAssignment
                    {
                        StudentId = studentId,
                        ResidenceId = chosenResidence.ResidenceId,
                        RoomId = chosenRoom.RoomId,
                        BedId = bed.BedId,
                        IsActive = true,
                        MoveInDate = DateTime.Now
                    };

                    db.ResidenceAssignments.Add(assignment);

                    db.SaveChanges();
                    transaction.Commit();

                    // Notification hook — wire to your NotificationService/email service
                    NotifyHouseMaster(chosenResidence, assignment);

                    return assignment;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void NotifyHouseMaster(Residence residence, ResidenceAssignment assignment)
        {
            // Placeholder hook — connect to your existing NotificationService/email
            // sender once that service is built. Left as a clean integration point
            // rather than a fake implementation.
        }
    }
}
