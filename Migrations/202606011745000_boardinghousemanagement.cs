namespace Michaelhouse.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class boardinghousemanagement : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.HouseMasters",
                c => new
                {
                    HouseMasterId = c.Int(nullable: false, identity: true),
                    FullName = c.String(nullable: false, maxLength: 200),
                    ContactEmail = c.String(maxLength: 100),
                    ContactPhone = c.String(maxLength: 20),
                    ResidenceId = c.Int()
                })
                .PrimaryKey(t => t.HouseMasterId);

            CreateTable(
                "dbo.Residences",
                c => new
                {
                    ResidenceId = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 100),
                    Gender = c.String(maxLength: 20),
                    Capacity = c.Int(nullable: false),
                    OccupiedBeds = c.Int(nullable: false),
                    HouseMasterId = c.Int(),
                    GradeCategory = c.String(),
                    NearMedicalFacility = c.Boolean(nullable: false),
                    NearHouseMasterOffice = c.Boolean(nullable: false),
                    IsArchived = c.Boolean(nullable: false),
                    ArchivedDate = c.DateTime(nullable: false),
                    ArchivedBy = c.String()
                })
                .PrimaryKey(t => t.ResidenceId)
                .ForeignKey("dbo.HouseMasters", t => t.HouseMasterId)
                .Index(t => t.HouseMasterId);

            CreateTable(
                "dbo.Rooms",
                c => new
                {
                    RoomId = c.Int(nullable: false, identity: true),
                    RoomNumber = c.String(maxLength: 50),
                    Capacity = c.Int(nullable: false),
                    OccupiedBeds = c.Int(nullable: false),
                    IsFull = c.Boolean(nullable: false),
                    Floor = c.Int(nullable: false),
                    IsGroundFloor = c.Boolean(nullable: false),
                    IsWheelchairAccessible = c.Boolean(nullable: false),
                    NearBathroom = c.Boolean(nullable: false),
                    IsQuietStudyRoom = c.Boolean(nullable: false),
                    NeedsMaintenance = c.Boolean(nullable: false),
                    ResidenceId = c.Int(nullable: false),
                    IsArchived = c.Boolean(nullable: false)
                })
                .PrimaryKey(t => t.RoomId)
                .ForeignKey("dbo.Residences", t => t.ResidenceId)
                .Index(t => t.ResidenceId);

            CreateTable(
                "dbo.Beds",
                c => new
                {
                    BedId = c.Int(nullable: false, identity: true),
                    RoomId = c.Int(nullable: false),
                    BedNumber = c.String(maxLength: 50),
                    IsOccupied = c.Boolean(nullable: false),
                    Status = c.String(maxLength: 20),
                    OccupiedByStudentId = c.Int()
                })
                .PrimaryKey(t => t.BedId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .ForeignKey("dbo.Students", t => t.OccupiedByStudentId)
                .Index(t => t.RoomId)
                .Index(t => t.OccupiedByStudentId);

            CreateTable(
                "dbo.ResidenceAssignments",
                c => new
                {
                    ResidenceAssignmentId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    ResidenceId = c.Int(nullable: false),
                    RoomId = c.Int(nullable: false),
                    BedId = c.Int(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    MoveInDate = c.DateTime(nullable: false),
                    VacatedDate = c.DateTime(),
                    Status = c.String(maxLength: 20)
                })
                .PrimaryKey(t => t.ResidenceAssignmentId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Residences", t => t.ResidenceId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .ForeignKey("dbo.Beds", t => t.BedId)
                .Index(t => t.StudentId)
                .Index(t => t.ResidenceId)
                .Index(t => t.RoomId)
                .Index(t => t.BedId);

            CreateTable(
                "dbo.StudentQRCodes",
                c => new
                {
                    QRCodeId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    QRCodeValue = c.String(nullable: false, maxLength: 512),
                    QRImage = c.Binary(),
                    DateGenerated = c.DateTime(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    RegeneratedFromId = c.Int()
                })
                .PrimaryKey(t => t.QRCodeId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId)
                .Index(t => t.QRCodeValue, unique: true);

            CreateTable(
                "dbo.QRScanRecords",
                c => new
                {
                    QRScanRecordId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    ResidenceId = c.Int(),
                    ScannedAt = c.DateTime(nullable: false),
                    Action = c.String(maxLength: 40),
                    HouseMasterId = c.Int(),
                    Scanner = c.String(maxLength: 200),
                    Approved = c.Boolean(nullable: false),
                    Reason = c.String(maxLength: 1000),
                    IsHoliday = c.Boolean(nullable: false),
                    OnTrip = c.Boolean(nullable: false),
                    MedicalRestrictionViolated = c.Boolean(nullable: false)
                })
                .PrimaryKey(t => t.QRScanRecordId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Residences", t => t.ResidenceId)
                .ForeignKey("dbo.HouseMasters", t => t.HouseMasterId)
                .Index(t => t.StudentId)
                .Index(t => t.ResidenceId)
                .Index(t => t.HouseMasterId);

            CreateTable(
                "dbo.StudentProfiles",
                c => new
                {
                    StudentId = c.Int(nullable: false),
                    AcademicStream = c.String(),
                    Grade = c.String(),
                    ElectiveSubjects = c.String(),
                    Sports = c.String(),
                    ClubsAndSocieties = c.String(),
                    AccessibilityRequired = c.Boolean(nullable: false),
                    AccessibilityNotes = c.String(),
                    MedicalAccommodationRequired = c.Boolean(nullable: false),
                    MedicalAccommodationNotes = c.String(),
                    PreviousResidenceId = c.Int(),
                    PreviousRoomId = c.Int(),
                    PreviousRoommateIds = c.String()
                })
                .PrimaryKey(t => t.StudentId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);

            CreateTable(
                "dbo.RoomScoreAudits",
                c => new
                {
                    RoomScoreAuditId = c.Int(nullable: false, identity: true),
                    RoomId = c.Int(nullable: false),
                    ResidenceId = c.Int(nullable: false),
                    StudentId = c.Int(nullable: false),
                    Score = c.Double(nullable: false),
                    CalculatedAt = c.DateTime(nullable: false),
                    Details = c.String()
                })
                .PrimaryKey(t => t.RoomScoreAuditId);

            CreateTable(
                "dbo.DisciplinaryConflicts",
                c => new
                {
                    DisciplinaryConflictId = c.Int(nullable: false, identity: true),
                    StudentAId = c.Int(nullable: false),
                    StudentBId = c.Int(nullable: false),
                    Reason = c.String(),
                    ReportedAt = c.DateTime(nullable: false)
                })
                .PrimaryKey(t => t.DisciplinaryConflictId);

            CreateTable(
                "dbo.AIResidenceRecommendations",
                c => new
                {
                    RecommendationId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    ResidenceId = c.Int(nullable: false),
                    RoomId = c.Int(nullable: false),
                    CompatibilityScore = c.Double(nullable: false),
                    ConfidenceScore = c.Double(nullable: false),
                    Explanation = c.String(maxLength: 4000),
                    GeneratedAt = c.DateTime(nullable: false),
                    AlgorithmVersion = c.String(maxLength: 50)
                })
                .PrimaryKey(t => t.RecommendationId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Residences", t => t.ResidenceId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .Index(t => t.StudentId)
                .Index(t => t.ResidenceId)
                .Index(t => t.RoomId);

            CreateTable(
                "dbo.AIAllocationHistories",
                c => new
                {
                    AllocationHistoryId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    ResidenceId = c.Int(nullable: false),
                    RoomId = c.Int(nullable: false),
                    Score = c.Double(nullable: false),
                    Confidence = c.Double(nullable: false),
                    Explanation = c.String(maxLength: 4000),
                    Accepted = c.Boolean(nullable: false),
                    Overridden = c.Boolean(nullable: false),
                    OverrideReason = c.String(maxLength: 1000),
                    CreatedDate = c.DateTime(nullable: false)
                })
                .PrimaryKey(t => t.AllocationHistoryId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);

            CreateTable(
                "dbo.AIWaitingLists",
                c => new
                {
                    WaitingListId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    RequestedAt = c.DateTime(nullable: false),
                    Reason = c.String(maxLength: 1000),
                    Priority = c.Int(nullable: false),
                    NotifiedAdmissions = c.Boolean(nullable: false)
                })
                .PrimaryKey(t => t.WaitingListId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);

            CreateTable(
                "dbo.AIRecommendationOverrides",
                c => new
                {
                    AIRecommendationOverrideId = c.Int(nullable: false, identity: true),
                    RecommendationId = c.Int(nullable: false),
                    OriginalScore = c.Double(nullable: false),
                    AdminUserId = c.Int(nullable: false),
                    AdminName = c.String(),
                    OverrideReason = c.String(maxLength: 1000),
                    NewResidenceId = c.Int(nullable: false),
                    NewRoomId = c.Int(nullable: false),
                    CreatedAt = c.DateTime(nullable: false)
                })
                .PrimaryKey(t => t.AIRecommendationOverrideId)
                .ForeignKey("dbo.AIResidenceRecommendations", t => t.RecommendationId)
                .Index(t => t.RecommendationId);

            CreateTable(
                "dbo.AIAlerts",
                c => new
                {
                    AIAlertId = c.Int(nullable: false, identity: true),
                    ResidenceId = c.Int(nullable: false),
                    Title = c.String(nullable: false, maxLength: 200),
                    Message = c.String(maxLength: 4000),
                    CreatedAt = c.DateTime(nullable: false),
                    IsResolved = c.Boolean(nullable: false)
                })
                .PrimaryKey(t => t.AIAlertId)
                .ForeignKey("dbo.Residences", t => t.ResidenceId)
                .Index(t => t.ResidenceId);

            CreateTable(
                "dbo.ResidenceMovements",
                c => new
                {
                    ResidenceMovementId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    FromResidenceId = c.Int(),
                    FromRoomId = c.Int(),
                    ToResidenceId = c.Int(),
                    ToRoomId = c.Int(),
                    PerformedByUserId = c.Int(),
                    PerformedByName = c.String(),
                    PerformedAt = c.DateTime(nullable: false),
                    Reason = c.String(maxLength: 1000)
                })
                .PrimaryKey(t => t.ResidenceMovementId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);

            CreateTable(
                "dbo.ResidenceAllocations",
                c => new
                {
                    ResidenceAllocationId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    ResidenceId = c.Int(nullable: false),
                    RoomId = c.Int(nullable: false),
                    BedId = c.Int(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    AllocatedAt = c.DateTime(nullable: false),
                    ReleasedAt = c.DateTime(),
                    CreatedBy = c.String(),
                    CreatedAt = c.DateTime(nullable: false),
                    UpdatedBy = c.String(),
                    UpdatedAt = c.DateTime()
                })
                .PrimaryKey(t => t.ResidenceAllocationId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Residences", t => t.ResidenceId)
                .ForeignKey("dbo.Rooms", t => t.RoomId)
                .ForeignKey("dbo.Beds", t => t.BedId)
                .Index(t => t.StudentId)
                .Index(t => t.ResidenceId)
                .Index(t => t.RoomId)
                .Index(t => t.BedId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.ResidenceAllocations", "BedId", "dbo.Beds");
            DropForeignKey("dbo.ResidenceAllocations", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.ResidenceAllocations", "ResidenceId", "dbo.Residences");
            DropForeignKey("dbo.ResidenceAllocations", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ResidenceMovements", "StudentId", "dbo.Students");
            DropForeignKey("dbo.AIAlerts", "ResidenceId", "dbo.Residences");
            DropForeignKey("dbo.AIRecommendationOverrides", "RecommendationId", "dbo.AIResidenceRecommendations");
            DropForeignKey("dbo.AIWaitingLists", "StudentId", "dbo.Students");
            DropForeignKey("dbo.AIAllocationHistories", "StudentId", "dbo.Students");
            DropForeignKey("dbo.AIResidenceRecommendations", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.AIResidenceRecommendations", "ResidenceId", "dbo.Residences");
            DropForeignKey("dbo.AIResidenceRecommendations", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StudentProfiles", "StudentId", "dbo.Students");
            DropForeignKey("dbo.QRScanRecords", "HouseMasterId", "dbo.HouseMasters");
            DropForeignKey("dbo.QRScanRecords", "ResidenceId", "dbo.Residences");
            DropForeignKey("dbo.QRScanRecords", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StudentQRCodes", "StudentId", "dbo.Students");
            DropForeignKey("dbo.ResidenceAssignments", "BedId", "dbo.Beds");
            DropForeignKey("dbo.ResidenceAssignments", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.ResidenceAssignments", "ResidenceId", "dbo.Residences");
            DropForeignKey("dbo.ResidenceAssignments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Beds", "OccupiedByStudentId", "dbo.Students");
            DropForeignKey("dbo.Beds", "RoomId", "dbo.Rooms");
            DropForeignKey("dbo.Rooms", "ResidenceId", "dbo.Residences");
            DropForeignKey("dbo.Residences", "HouseMasterId", "dbo.HouseMasters");
            DropTable("dbo.ResidenceAllocations");
            DropTable("dbo.ResidenceMovements");
            DropTable("dbo.AIAlerts");
            DropTable("dbo.AIRecommendationOverrides");
            DropTable("dbo.AIWaitingLists");
            DropTable("dbo.AIAllocationHistories");
            DropTable("dbo.AIResidenceRecommendations");
            DropTable("dbo.DisciplinaryConflicts");
            DropTable("dbo.RoomScoreAudits");
            DropTable("dbo.StudentProfiles");
            DropTable("dbo.QRScanRecords");
            DropTable("dbo.StudentQRCodes");
            DropTable("dbo.ResidenceAssignments");
            DropTable("dbo.Beds");
            DropTable("dbo.Rooms");
            DropTable("dbo.Residences");
            DropTable("dbo.HouseMasters");
        }
    }
}
