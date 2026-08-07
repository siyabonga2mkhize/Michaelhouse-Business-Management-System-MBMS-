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
    }

    public class ResidenceAllocationEngine
    {
        private DBContextClass db = new DBContextClass();
        private ResidenceAvailabilityService availabilityService = new ResidenceAvailabilityService();
        private CompatibilityScoringService scoringService = new CompatibilityScoringService();

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

            var candidates = availabilityService.GetAvailableRoomsWithOccupants();

            var scored = candidates
                .Select(c => scoringService.ScoreRoom(profile, c.Room, c.Residence, c.Occupants))
                .Where(r => !r.IsDisqualified)
                .OrderByDescending(r => r.TotalScore)
                .ToList();

            if (!scored.Any())
                throw new InvalidOperationException(
                    "No eligible room found. All rooms are either full or fail a mandatory requirement (e.g. accessibility) for this student.");

            return new AllocationRecommendation
            {
                StudentId = studentId,
                RankedRooms = scored,
                BestMatch = scored.First()
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
                        chosenRoom = db.Rooms.Find(recommendation.BestMatch.Room.RoomId);
                        chosenResidence = db.Residences.Find(recommendation.BestMatch.Residence.ResidenceId);
                    }

                    var bed = availabilityService.GetFirstAvailableBed(chosenRoom.RoomId);

                    if (bed == null)
                        throw new InvalidOperationException("No available bed in the selected room.");

                    // ---- Reserve the bed ----
                    bed.IsOccupied = true;

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