using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Michaelhouse.Services
{
    /// <summary>
    /// AI-powered residence allocation service.
    ///
    /// Responsibilities:
    /// - Analyze student/residence/room features
    /// - Calculate compatibility & confidence scores using a decision model
    /// - Produce human-readable explanations for recommendations
    /// - Commit allocations to the database (transactional)
    ///
    /// Designed for reuse from controllers and background jobs. Uses composition
    /// to depend on small services (Availability + Compatibility scoring).
    /// </summary>
    public class AIResidenceAllocationService
    {
        private const string AlgorithmVersionId = "ai-v1.0";
        private readonly DBContextClass _db;
        private readonly ResidenceAvailabilityService _availability;
        private readonly CompatibilityScoringService _scoring;
        private readonly ExplanationGenerator _explanationGenerator;

        public AIResidenceAllocationService()
            : this(new DBContextClass(), new ResidenceAvailabilityService(), new CompatibilityScoringService())
        {
        }

        // Constructor for testability / DI
        public AIResidenceAllocationService(DBContextClass db, ResidenceAvailabilityService availability, CompatibilityScoringService scoring)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
            _scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
            _explanationGenerator = new ExplanationGenerator();
        }

        #region Analysis DTOs

        public class StudentAnalysis
        {
            public StudentProfile Profile { get; set; }
            public Student Student { get; set; }
        }

        public class ResidenceAnalysis
        {
            public Residence Residence { get; set; }
            public int FreeBeds { get; set; }
            public string Summary { get; set; }
        }

        public class RoomAnalysis
        {
            public Room Room { get; set; }
            public Residence Residence { get; set; }
            public List<Student> Occupants { get; set; }
            public string Summary { get; set; }
        }

        public class AIAllocationRecommendation
        {
            public Residence Residence { get; set; }
            public Room Room { get; set; }
            public double CompatibilityScore { get; set; } // 0..100
            public double ConfidenceScore { get; set; } // 0..1
            public string Explanation { get; set; }
        }

        #endregion

        /// <summary>
        /// Analyze student and return combined Student + Profile data used by the model.
        /// </summary>
        public StudentAnalysis AnalyzeStudent(int studentId)
        {
            var student = _db.Students.Find(studentId);
            if (student == null) throw new InvalidOperationException("Student not found.");

            var profile = _db.StudentProfiles.Find(studentId);
            if (profile == null)
                throw new InvalidOperationException("Student profile not found. Complete profile before running AI allocation.");

            // Basic heuristics enrichment could be added here (e.g., derive age band)
            return new StudentAnalysis { Student = student, Profile = profile };
        }

        /// <summary>
        /// Analyze a residence to extract features relevant to allocation.
        /// </summary>
        public ResidenceAnalysis AnalyzeResidence(int residenceId)
        {
            var res = _db.Residences.Find(residenceId);
            if (res == null) throw new InvalidOperationException("Residence not found.");

            var freeBeds = res.Capacity - res.OccupiedBeds;

            var summary = $"{res.Name}: {freeBeds} free beds, grade category={res.GradeCategory}, nearMedical={res.NearMedicalFacility}";

            return new ResidenceAnalysis { Residence = res, FreeBeds = freeBeds, Summary = summary };
        }

        /// <summary>
        /// Analyze a room (within its residence) and return occupants and attributes.
        /// </summary>
        public RoomAnalysis AnalyzeRoom(int roomId)
        {
            var room = _db.Rooms.Include(r => r.Residence).FirstOrDefault(r => r.RoomId == roomId);
            if (room == null) throw new InvalidOperationException("Room not found.");

            var occupants = _db.ResidenceAssignments
                .Where(a => a.RoomId == roomId && a.IsActive)
                .Select(a => a.Student)
                .ToList();

            var summary = $"Room {room.RoomNumber}: {room.Capacity - room.OccupiedBeds} free / {room.Capacity} capacity; groundFloor={room.IsGroundFloor}; quiet={room.IsQuietStudyRoom}";

            return new RoomAnalysis { Room = room, Residence = room.Residence, Occupants = occupants, Summary = summary };
        }

        /// <summary>
        /// Calculate a compatibility score (0..100) and explanation for a student vs a room.
        /// This method composes the existing compatibility scoring service and augments
        /// with AI-style confidence heuristics. The scoringService returns a RoomScoreResult
        /// with a breakdown which we convert into a numeric 0..100 compatibility value.
        /// </summary>
        public (double compatibility, double confidence, string explanation) CalculateCompatibilityScore(StudentProfile studentProfile, Room room, Residence residence, List<Student> occupants)
        {
            var scoreResult = _scoring.ScoreRoom(studentProfile, room, residence, occupants);
            if (scoreResult.IsDisqualified)
            {
                return (0, 0, $"Disqualified: {scoreResult.DisqualificationReason}");
            }

            var (compat, conf, expl) = ComputeFromScoreResult(scoreResult, studentProfile, room, residence, occupants);
            return (compat, conf, expl);
        }

        // Internal: compute compatibility and confidence from an existing RoomScoreResult
        private (double compatibility, double confidence, string explanation) ComputeFromScoreResult(RoomScoreResult scoreResult, StudentProfile profile, Room room, Residence residence, List<Student> occupants)
        {
            // Map TotalScore (int) to 0..100 using same heuristic upper bound as before
            var maxPossible = AllocationRuleService.GradeLevelWeight
                + AllocationRuleService.AgeCompatibilityMaxWeight
                + AllocationRuleService.SubjectCompatibilityWeight
                + AllocationRuleService.SportsCompatibilityWeight
                + AllocationRuleService.ClubsCompatibilityWeight
                + AllocationRuleService.PreviousRoommateWeight
                + AllocationRuleService.AccessibilityWeight
                + AllocationRuleService.MedicalAccommodationWeight;

            double compatibility = Math.Max(0, Math.Min(100, (scoreResult.TotalScore / (double)maxPossible) * 100.0));

            double baseConfidence = 0.5;
            if (occupants != null && occupants.Count > 0)
            {
                baseConfidence += Math.Min(0.4, occupants.Count / (double)Math.Max(1, room.Capacity) * 0.4);
            }
            else
            {
                baseConfidence -= 0.1;
            }
            baseConfidence += (compatibility / 100.0) * 0.2;
            double confidence = Math.Max(0, Math.Min(1.0, baseConfidence));

            var explanation = _explanationGenerator.Generate(scoreResult, profile, room, residence, compatibility, confidence);
            return (compatibility, confidence, explanation);
        }

        /// <summary>
        /// Generate a ranked list of recommendations for the given student.
        /// Returns the best recommendation (highest compatibility) as well as
        /// the full ranked list for display.
        /// </summary>
        public (AIAllocationRecommendation Best, List<AIAllocationRecommendation> Ranked) GenerateRecommendation(int studentId)
        {
            var analysis = AnalyzeStudent(studentId);

            var candidates = _availability.GetAvailableRoomsWithOccupants();
            var recs = new List<AIAllocationRecommendation>();

            foreach (var c in candidates)
            {
                var (compat, conf, explanation) = CalculateCompatibilityScore(analysis.Profile, c.Room, c.Residence, c.Occupants);

                var rec = new AIAllocationRecommendation
                {
                    Residence = c.Residence,
                    Room = c.Room,
                    CompatibilityScore = Math.Round(compat, 2),
                    ConfidenceScore = Math.Round(conf, 2),
                    Explanation = explanation
                };

                // Only include non-zero compatibility
                if (rec.CompatibilityScore > 0)
                    recs.Add(rec);
            }

            var ranked = recs.OrderByDescending(r => r.CompatibilityScore).ThenByDescending(r => r.ConfidenceScore).ToList();

            if (!ranked.Any())
            {
                // No eligible rooms - place student on AI waiting list and notify admissions
                var reason = "No eligible rooms available or all rooms at capacity.";
                try
                {
                    var wait = new Models.AIWaitingList
                    {
                        StudentId = analysis.Profile.StudentId,
                        Reason = reason,
                        RequestedAt = DateTime.UtcNow,
                        Priority = 0,
                        NotifiedAdmissions = false
                    };
                    _db.AIWaitingLists.Add(wait);
                    _db.SaveChanges();

                    // Create allocation history entry indicating waiting list placement
                    var history = new AIAllocationHistory
                    {
                        StudentId = analysis.Profile.StudentId,
                        ResidenceId = 0,
                        RoomId = 0,
                        Score = 0,
                        Confidence = 0,
                        Explanation = "Placed on waiting list: " + reason,
                        Accepted = false,
                        Overridden = false,
                        OverrideReason = null,
                        CreatedDate = DateTime.UtcNow
                    };
                    _db.AIAllocationHistories.Add(history);
                    _db.SaveChanges();

                    // Notify admissions (simple email to admin contact via EmailService)
                    try
                    {
                        var email = new EmailService();
                        var adminContact = System.Configuration.ConfigurationManager.AppSettings["Admissions:ContactEmail"] ?? "";
                        if (!string.IsNullOrEmpty(adminContact))
                        {
                            var student = _db.Students.Find(analysis.Profile.StudentId);
                            var body = $"Auto allocation could not find a room for student {student?.Name} (ID: {student?.StudentId}). Reason: {reason}. The student was placed on the AI waiting list (ID: {wait.WaitingListId}).";
                            email.SendPlain(adminContact, "AI Allocation Waiting List Notification", body);
                            wait.NotifiedAdmissions = true;
                            _db.SaveChanges();
                        }
                    }
                    catch { }
                }
                catch { }

                throw new InvalidOperationException("No eligible rooms found for this student; student placed on waiting list.");
            }

            // Persist top recommendation as AIResidenceRecommendation for audit & review
            var top = ranked.First();
            try
            {
                var aiRec = new Models.AIResidenceRecommendation
                {
                    StudentId = analysis.Profile.StudentId,
                    ResidenceId = top.Residence.ResidenceId,
                    RoomId = top.Room.RoomId,
                    CompatibilityScore = top.CompatibilityScore,
                    ConfidenceScore = top.ConfidenceScore,
                    Explanation = top.Explanation,
                    GeneratedAt = DateTime.Now,
                    AlgorithmVersion = AlgorithmVersionId
                };
                _db.AIResidenceRecommendations.Add(aiRec);
                _db.SaveChanges();
            }
            catch
            {
                // swallow to avoid blocking recommendation generation
            }

            return (ranked.First(), ranked);
        }

        /// <summary>
        /// Allocate the student to the recommended room. If overrideRoomId is provided,
        /// the service will attempt to allocate that room if it is still available.
        /// Returns the created ResidenceAssignment on success.
        /// </summary>
        public ResidenceAssignment AllocateStudent(int studentId, int? overrideRoomId = null, bool accepted = true, bool overridden = false, string overrideReason = null)
        {
            using (var tx = _db.Database.BeginTransaction())
            {
                try
                {
                    // Prevent double allocation
                    var existing = _db.ResidenceAssignments.FirstOrDefault(a => a.StudentId == studentId && a.IsActive);
                    if (existing != null)
                        throw new InvalidOperationException("Student already has an active residence assignment.");

                    var student = _db.Students.Find(studentId);
                    if (student == null)
                        throw new InvalidOperationException("Student not found.");

                    Room chosenRoom = null;
                    Residence chosenResidence = null;
                    double compatScore = 0;
                    double confidenceScore = 1;
                    string explanation = "Automatically allocated to the residence with the most available beds, then the room with the most available beds.";

                    if (overrideRoomId.HasValue)
                    {
                        chosenRoom = _db.Rooms.Include(r => r.Residence).FirstOrDefault(r => r.RoomId == overrideRoomId.Value);
                        if (chosenRoom == null) throw new InvalidOperationException("Selected room not found.");
                        if (chosenRoom.OccupiedBeds >= chosenRoom.Capacity || chosenRoom.IsFull)
                            throw new InvalidOperationException("Selected room is now full.");

                        chosenResidence = chosenRoom.Residence;
                    }
                    else
                    {
                        chosenRoom = _db.Rooms
                            .Include(r => r.Residence)
                            .Where(r => !r.IsArchived &&
                                        !r.NeedsMaintenance &&
                                        !r.IsFull &&
                                        r.OccupiedBeds < r.Capacity &&
                                        !r.Residence.IsArchived &&
                                        r.Residence.OccupiedBeds < r.Residence.Capacity &&
                                        _db.Beds.Any(b => b.RoomId == r.RoomId &&
                                                         !b.IsOccupied &&
                                                         (b.Status == null || b.Status == "Available")))
                            .OrderByDescending(r => r.Residence.Capacity - r.Residence.OccupiedBeds)
                            .ThenByDescending(r => r.Capacity - r.OccupiedBeds)
                            .ThenBy(r => r.Residence.Name)
                            .ThenBy(r => r.RoomNumber)
                            .FirstOrDefault();

                        if (chosenRoom == null)
                            throw new InvalidOperationException("No available residence bed exists. Please add active rooms and available bed spaces before completing registration.");

                        chosenResidence = chosenRoom.Residence;
                    }

                    if (chosenResidence == null)
                        throw new InvalidOperationException("Selected room is not linked to an active residence.");

                    var bed = _availability.GetFirstAvailableBed(chosenRoom.RoomId);
                    if (bed == null) throw new InvalidOperationException("No available bed in the selected room.");

                    // Reserve bed and update counts
                    bed.IsOccupied = true;
                    bed.Status = "Occupied";
                    bed.OccupiedByStudentId = studentId;
                    chosenRoom.OccupiedBeds++;
                    chosenRoom.IsFull = chosenRoom.OccupiedBeds >= chosenRoom.Capacity;
                    chosenResidence.OccupiedBeds++;

                    var assignment = new ResidenceAssignment
                    {
                        StudentId = studentId,
                        ResidenceId = chosenResidence.ResidenceId,
                        RoomId = chosenRoom.RoomId,
                        BedId = bed.BedId,
                        IsActive = true,
                        MoveInDate = DateTime.Now,
                        Status = "Active"
                    };

                    // Add assignment and audit/history in same transaction
                    _db.ResidenceAssignments.Add(assignment);

                    // Create allocation history record for ML/audit
                    var history = new AIAllocationHistory
                    {
                        StudentId = studentId,
                        ResidenceId = chosenResidence.ResidenceId,
                        RoomId = chosenRoom.RoomId,
                        Score = compatScore,
                        Confidence = confidenceScore,
                        Explanation = explanation,
                        Accepted = accepted,
                        Overridden = overridden,
                        OverrideReason = overrideReason,
                        CreatedDate = DateTime.UtcNow
                    };

                    _db.AIAllocationHistories.Add(history);

                    _db.SaveChanges();
                    tx.Commit();

                    new StudentQRCodeService().GenerateQRCode(studentId);

                    // Hook for notifications (housemaster etc.)
                    NotifyHouseMaster(chosenResidence, assignment);

                    return assignment;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Administrator or HouseMaster can override an allocation and move a student.
        /// This preserves original AI allocation records, records the override in AIAllocationHistory,
        /// updates occupancy and bed availability, records a ResidenceMovement, and notifies the student/parent.
        /// </summary>
        public void OverrideAllocation(int studentId, int newRoomId, int performedByUserId, string performedByName, string reason)
        {
            using (var tx = _db.Database.BeginTransaction())
            {
                try
                {
                    // Find existing active assignment
                    var existing = _db.ResidenceAssignments.FirstOrDefault(a => a.StudentId == studentId && a.IsActive);
                    if (existing == null)
                        throw new InvalidOperationException("Student has no active residence assignment to override.");

                    var oldRoom = _db.Rooms.Include(r => r.Residence).FirstOrDefault(r => r.RoomId == existing.RoomId);
                    var oldResidence = oldRoom?.Residence;

                    var newRoom = _db.Rooms.Include(r => r.Residence).FirstOrDefault(r => r.RoomId == newRoomId);
                    if (newRoom == null) throw new InvalidOperationException("Target room not found.");
                    if (newRoom.OccupiedBeds >= newRoom.Capacity || newRoom.IsFull) throw new InvalidOperationException("Target room is full.");

                    // Free old bed
                    var oldBed = _db.Beds.Find(existing.BedId);
                    if (oldBed != null)
                    {
                        oldBed.IsOccupied = false;
                        oldBed.Status = "Available";
                        oldBed.OccupiedByStudentId = null;
                    }

                    if (oldRoom != null)
                    {
                        oldRoom.OccupiedBeds = Math.Max(0, oldRoom.OccupiedBeds - 1);
                        oldRoom.IsFull = oldRoom.OccupiedBeds >= oldRoom.Capacity;
                    }
                    if (oldResidence != null)
                    {
                        oldResidence.OccupiedBeds = Math.Max(0, oldResidence.OccupiedBeds - 1);
                    }

                    // Mark existing assignment inactive
                    existing.IsActive = false;
                    existing.Status = "Transferred";
                    existing.VacatedDate = DateTime.Now;

                    // Allocate new bed
                    var newBed = _availability.GetFirstAvailableBed(newRoom.RoomId);
                    if (newBed == null) throw new InvalidOperationException("No available bed in target room.");
                    newBed.IsOccupied = true;
                    newBed.Status = "Occupied";
                    newBed.OccupiedByStudentId = studentId;
                    newRoom.OccupiedBeds++;
                    newRoom.IsFull = newRoom.OccupiedBeds >= newRoom.Capacity;
                    var newResidence = newRoom.Residence;
                    if (newResidence != null) newResidence.OccupiedBeds++;

                    var newAssignment = new ResidenceAssignment
                    {
                        StudentId = studentId,
                        ResidenceId = newResidence.ResidenceId,
                        RoomId = newRoom.RoomId,
                        BedId = newBed.BedId,
                        IsActive = true,
                        MoveInDate = DateTime.Now,
                        Status = "Active"
                    };

                    _db.ResidenceAssignments.Add(newAssignment);

                    // Record movement
                    var movement = new ResidenceMovement
                    {
                        StudentId = studentId,
                        FromResidenceId = oldResidence?.ResidenceId,
                        FromRoomId = oldRoom?.RoomId,
                        ToResidenceId = newResidence?.ResidenceId,
                        ToRoomId = newRoom.RoomId,
                        PerformedByUserId = performedByUserId,
                        PerformedByName = performedByName,
                        PerformedAt = DateTime.UtcNow,
                        Reason = reason
                    };
                    _db.ResidenceMovements.Add(movement);

                    // Record override in allocation history
                    var history = new AIAllocationHistory
                    {
                        StudentId = studentId,
                        ResidenceId = newResidence?.ResidenceId ?? 0,
                        RoomId = newRoom.RoomId,
                        Score = 0, // override entries may set score to 0 or preserve original - set 0 to indicate manual change
                        Confidence = 0,
                        Explanation = $"Manual override by {performedByName}: {reason}",
                        Accepted = false,
                        Overridden = true,
                        OverrideReason = reason,
                        CreatedDate = DateTime.UtcNow
                    };
                    _db.AIAllocationHistories.Add(history);

                    // Preserve original AI recommendation by creating an override record
                    try
                    {
                        var origRec = _db.AIResidenceRecommendations
                            .Where(r => r.StudentId == studentId)
                            .OrderByDescending(r => r.GeneratedAt)
                            .FirstOrDefault();

                        if (origRec != null)
                        {
                            var recOverride = new AIRecommendationOverride
                            {
                                RecommendationId = origRec.RecommendationId,
                                OriginalScore = origRec.CompatibilityScore,
                                AdminUserId = performedByUserId,
                                AdminName = performedByName,
                                OverrideReason = reason,
                                NewResidenceId = newResidence?.ResidenceId ?? 0,
                                NewRoomId = newRoom.RoomId,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.AIRecommendationOverrides.Add(recOverride);
                        }
                    }
                    catch { /* swallow to avoid rollback on audit write failure */ }

                    _db.SaveChanges();
                    tx.Commit();

                    // Notify student/parent of change
                    var student = _db.Students.Find(studentId);
                    if (student != null)
                    {
                        var parent = _db.Parents.Find(student.ParentId);
                        var email = new EmailService();
                        if (parent != null)
                        {
                            string body = $"<p>Dear {parent.Name},</p><p>Your child {student.Name} has been moved from {oldResidence?.Name ?? "N/A"} Room {oldRoom?.RoomNumber ?? "N/A"} to {newResidence?.Name} Room {newRoom.RoomNumber}. Reason: {reason}</p>";
                            email.SendPlain(parent.Contact, "Residence Allocation Changed", body);
                        }
                    }
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Notification hook — left for the integrator to wire up existing notification/email services.
        /// </summary>
        protected virtual void NotifyHouseMaster(Residence residence, ResidenceAssignment assignment)
        {
            // Intentionally left blank as an extension point.
        }
    }
}
