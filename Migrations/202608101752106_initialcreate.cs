namespace Michaelhouse.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class initialcreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AdminReviews",
                c => new
                {
                    ReviewId = c.Int(nullable: false, identity: true),
                    AppId = c.Int(),
                    DriverAppId = c.Int(),
                    AdminId = c.String(nullable: false),
                    Date = c.DateTime(nullable: false),
                    Decision = c.String(nullable: false),
                    AdminNotes = c.String(),
                    AgreedWithAi = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.ReviewId)
                .ForeignKey("dbo.Applications", t => t.AppId)
                .ForeignKey("dbo.DriverApplications", t => t.DriverAppId)
                .Index(t => t.AppId)
                .Index(t => t.DriverAppId);

            CreateTable(
                "dbo.Applications",
                c => new
                {
                    AppId = c.Int(nullable: false, identity: true),
                    ParentId = c.Int(nullable: false),
                    StudentId = c.Int(nullable: false),
                    Date = c.DateTime(nullable: false),
                    ApplicationYear = c.Int(nullable: false),
                    GradeApplying = c.Int(nullable: false),
                    Status = c.Int(nullable: false),
                    AiReviewSummary = c.String(),
                    AiRecommendation = c.String(),
                    AdditionalNotes = c.String(),
                })
                .PrimaryKey(t => t.AppId)
                .ForeignKey("dbo.Parents", t => t.ParentId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.ParentId)
                .Index(t => t.StudentId);

            CreateTable(
                "dbo.Documents",
                c => new
                {
                    DId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    AppId = c.Int(nullable: false),
                    Type = c.String(nullable: false, maxLength: 100),
                    FilePath = c.String(nullable: false),
                    FileName = c.String(),
                    ContentType = c.String(),
                    UploadedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.DId)
                .ForeignKey("dbo.Applications", t => t.AppId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId)
                .Index(t => t.AppId);

            CreateTable(
                "dbo.Students",
                c => new
                {
                    StudentId = c.Int(nullable: false, identity: true),
                    FirstName = c.String(nullable: false, maxLength: 200),
                    LastName = c.String(nullable: false, maxLength: 200),
                    DOB = c.DateTime(nullable: false),
                    HomeLanguage = c.String(maxLength: 50),
                    IdNumber = c.String(maxLength: 20),
                    PreviousSchool = c.String(maxLength: 200),
                    CurrentGrade = c.String(maxLength: 50),
                    MedicalConditions = c.String(maxLength: 500),
                    ParentId = c.Int(nullable: false),
                    UserId = c.Int(),
                    StudentNumber = c.String(),
                    GradeLevel = c.Int(nullable: false),
                    EnrollmentDate = c.DateTime(nullable: false),
                    ClassId = c.Int(),
                })
                .PrimaryKey(t => t.StudentId)
                .ForeignKey("dbo.Parents", t => t.ParentId, cascadeDelete: true)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.ParentId)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.Parents",
                c => new
                {
                    ParentId = c.Int(nullable: false, identity: true),
                    UserId = c.Int(),
                    Name = c.String(nullable: false, maxLength: 200),
                    Contact = c.String(nullable: false, maxLength: 200),
                    CellPhone = c.String(maxLength: 20),
                    WorkPhone = c.String(maxLength: 20),
                    HomePhone = c.String(maxLength: 20),
                    PhysicalAddress = c.String(maxLength: 300),
                    PostalAddress = c.String(maxLength: 300),
                    Relationship = c.String(maxLength: 100),
                    Occupation = c.String(maxLength: 200),
                    Employer = c.String(maxLength: 200),
                    EmergencyContactName = c.String(maxLength: 200),
                    EmergencyContactPhone = c.String(maxLength: 20),
                })
                .PrimaryKey(t => t.ParentId)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.AppUsers",
                c => new
                {
                    UserId = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 200),
                    Email = c.String(nullable: false, maxLength: 200),
                    PasswordHash = c.String(nullable: false),
                    Role = c.String(nullable: false),
                })
                .PrimaryKey(t => t.UserId);

            CreateTable(
                "dbo.StudentSubjects",
                c => new
                {
                    StudentSubjectId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    SubjectId = c.Int(nullable: false),
                    Stream = c.Int(nullable: false),
                    IsCompulsory = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.StudentSubjectId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);

            CreateTable(
                "dbo.Subjects",
                c => new
                {
                    SubjectId = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 200),
                    Code = c.String(maxLength: 50),
                    Stream = c.Int(nullable: false),
                    IsCompulsory = c.Boolean(nullable: false),
                    IsLanguage = c.Boolean(nullable: false),
                    IsMathsOption = c.Boolean(nullable: false),
                    RequiresMaths = c.Boolean(nullable: false),
                    ApplicableGrades = c.String(),
                    SortOrder = c.Int(nullable: false),
                    GradeLevel = c.Int(nullable: false),
                    TeacherId = c.Int(),
                })
                .PrimaryKey(t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId);

            CreateTable(
                "dbo.StudentMarks",
                c => new
                {
                    StudentMarkId = c.Int(nullable: false, identity: true),
                    AssessmentmentId = c.Int(nullable: false),
                    StudentId = c.Int(nullable: false),
                    MarksObtained = c.Decimal(precision: 18, scale: 2),
                    IsAbsent = c.Boolean(nullable: false),
                    TeacherComment = c.String(maxLength: 300),
                    CapturedAt = c.DateTime(),
                    Subject_SubjectId = c.Int(),
                })
                .PrimaryKey(t => t.StudentMarkId)
                .ForeignKey("dbo.Assessments", t => t.AssessmentmentId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Subjects", t => t.Subject_SubjectId)
                .Index(t => t.AssessmentmentId)
                .Index(t => t.StudentId)
                .Index(t => t.Subject_SubjectId);

            CreateTable(
                "dbo.Assessments",
                c => new
                {
                    AssessmentId = c.Int(nullable: false, identity: true),
                    TeacherId = c.Int(nullable: false),
                    SubjectId = c.Int(nullable: false),
                    Grade = c.Int(nullable: false),
                    Stream = c.Int(nullable: false),
                    Title = c.String(nullable: false, maxLength: 200),
                    AssessmentType = c.String(nullable: false, maxLength: 50),
                    Term = c.Int(nullable: false),
                    AcademicYear = c.Int(nullable: false),
                    ScheduledDate = c.DateTime(nullable: false),
                    TotalMarks = c.Decimal(nullable: false, precision: 18, scale: 2),
                    WeightingPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Notes = c.String(maxLength: 500),
                    MarksCaptureClosed = c.Boolean(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.AssessmentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId);

            CreateTable(
                "dbo.Teachers",
                c => new
                {
                    TeacherId = c.Int(nullable: false, identity: true),
                    FirstName = c.String(nullable: false, maxLength: 100),
                    LastName = c.String(nullable: false, maxLength: 100),
                    Email = c.String(nullable: false, maxLength: 200),
                    Phone = c.String(maxLength: 20),
                    Specialization = c.String(),
                    HireDate = c.DateTime(nullable: false),
                    UserId = c.Int(),
                })
                .PrimaryKey(t => t.TeacherId)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.StreamEnrolments",
                c => new
                {
                    StreamEnrolmentId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    TeacherId = c.Int(),
                    RegistrationId = c.Int(nullable: false),
                    Grade = c.Int(nullable: false),
                    Stream = c.Int(nullable: false),
                    TakesMathematics = c.Boolean(nullable: false),
                    EnrolledAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.StreamEnrolmentId)
                .ForeignKey("dbo.Registrations", t => t.RegistrationId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.StudentId)
                .Index(t => t.TeacherId)
                .Index(t => t.RegistrationId);

            CreateTable(
                "dbo.Registrations",
                c => new
                {
                    RegistrationId = c.Int(nullable: false, identity: true),
                    AppId = c.Int(nullable: false),
                    StudentId = c.Int(nullable: false),
                    GradeEnrolling = c.Int(nullable: false),
                    Status = c.Int(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                    CompletedAt = c.DateTime(),
                    Notes = c.String(),
                })
                .PrimaryKey(t => t.RegistrationId)
                .ForeignKey("dbo.Applications", t => t.AppId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.AppId)
                .Index(t => t.StudentId);

            CreateTable(
                "dbo.TeacherSubjectGrades",
                c => new
                {
                    TeacherSubjectGradeId = c.Int(nullable: false, identity: true),
                    TeacherId = c.Int(nullable: false),
                    SubjectId = c.Int(nullable: false),
                    Grade = c.Int(nullable: false),
                    Stream = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.TeacherSubjectGradeId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId);

            CreateTable(
                "dbo.TeacherAttendances",
                c => new
                {
                    TeacherAttendanceId = c.Int(nullable: false, identity: true),
                    Date = c.DateTime(nullable: false),
                    SignInTime = c.DateTime(),
                    SignOutTime = c.DateTime(),
                    Status = c.Int(nullable: false),
                    IsVerified = c.Boolean(nullable: false),
                    TeacherId = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.TeacherAttendanceId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.TeacherId);

            CreateTable(
                "dbo.TimetableSlots",
                c => new
                {
                    SlotId = c.Int(nullable: false, identity: true),
                    AcademicYear = c.Int(nullable: false),
                    DayOfWeek = c.Int(nullable: false),
                    PeriodId = c.Int(nullable: false),
                    TeacherId = c.Int(nullable: false),
                    SubjectId = c.Int(nullable: false),
                    Grade = c.Int(nullable: false),
                    Stream = c.Int(nullable: false),
                    Subject_SubjectId = c.Int(),
                })
                .PrimaryKey(t => t.SlotId)
                .ForeignKey("dbo.Periods", t => t.PeriodId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.Subject_SubjectId)
                .Index(t => t.PeriodId)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId)
                .Index(t => t.Subject_SubjectId);

            CreateTable(
                "dbo.Periods",
                c => new
                {
                    PeriodId = c.Int(nullable: false, identity: true),
                    PeriodNumber = c.Int(nullable: false),
                    StartTime = c.Time(nullable: false, precision: 7),
                    EndTime = c.Time(nullable: false, precision: 7),
                    Label = c.String(maxLength: 50),
                    IsBreak = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.PeriodId);

            CreateTable(
                "dbo.DriverApplications",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    FullName = c.String(nullable: false),
                    IDNumber = c.String(nullable: false),
                    PhoneNumber = c.String(nullable: false),
                    Email = c.String(nullable: false),
                    LicenceNumber = c.String(nullable: false),
                    LicenceExpiryDate = c.DateTime(nullable: false),
                    HasPDP = c.Boolean(nullable: false),
                    DocumentPath = c.String(),
                    Status = c.String(),
                    AdminNotes = c.String(),
                    DateSubmitted = c.DateTime(),
                    ReviewedDate = c.DateTime(),
                    UserId = c.Int(),
                    PublicTokenHash = c.String(),
                    PublicTokenExpiry = c.DateTime(),
                    InterviewDateTime = c.DateTime(),
                    InterviewMeetingLink = c.String(),
                    InterviewEmailSent = c.Boolean(nullable: false),
                    InterviewEmailSentAt = c.DateTime(),
                    AiReviewSummary = c.String(),
                    AiRecommendation = c.String(),
                    AiScore = c.Int(),
                    AiReviewComplete = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.DriverDocuments",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    DriverApplicationId = c.Int(nullable: false),
                    FilePath = c.String(),
                    DocumentType = c.String(),
                    OtherDocumentType = c.String(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DriverApplications", t => t.DriverApplicationId, cascadeDelete: true)
                .Index(t => t.DriverApplicationId);

            CreateTable(
                "dbo.Assets",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    AssetName = c.String(nullable: false),
                    Category = c.String(nullable: false),
                    LocationBuilding = c.String(nullable: false),
                    LocationRoom = c.String(),
                    Description = c.String(),
                    ConditionRating = c.String(),
                    QrCode = c.String(),
                    HealthScore = c.Int(nullable: false),
                    FaultCount = c.Int(nullable: false),
                    Status = c.String(),
                    DateRegistered = c.DateTime(nullable: false),
                    RegisteredById = c.Int(nullable: false),
                    WarrantyExpiry = c.DateTime(),
                    PurchaseDate = c.DateTime(),
                    PurchaseCost = c.Decimal(precision: 18, scale: 2),
                    TotalMaintenanceCost = c.Decimal(precision: 18, scale: 2),
                    ModelSerial = c.String(),
                    Supplier = c.String(),
                })
                .PrimaryKey(t => t.Id);

            CreateTable(
                "dbo.JobCards",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    JobReference = c.String(),
                    Title = c.String(nullable: false),
                    Description = c.String(),
                    JobType = c.String(),
                    Priority = c.String(nullable: false),
                    Status = c.String(),
                    PhotoBefore = c.String(),
                    PhotoAfter = c.String(),
                    CompletionNotes = c.String(),
                    FinalCondition = c.String(),
                    DateCreated = c.DateTime(nullable: false),
                    DateAssigned = c.DateTime(),
                    DateCompleted = c.DateTime(),
                    DueDate = c.DateTime(),
                    ResponseTimeMinutes = c.Int(),
                    LabourCost = c.Decimal(precision: 18, scale: 2),
                    PartsCost = c.Decimal(precision: 18, scale: 2),
                    TotalCost = c.Decimal(precision: 18, scale: 2),
                    AssetId = c.Int(nullable: false),
                    AssignedToId = c.Int(),
                    ReportedById = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Assets", t => t.AssetId)
                .ForeignKey("dbo.MaintenanceStaffs", t => t.AssignedToId)
                .Index(t => t.AssetId)
                .Index(t => t.AssignedToId);

            CreateTable(
                "dbo.MaintenanceStaffs",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    FullName = c.String(nullable: false),
                    StaffNumber = c.String(nullable: false),
                    Email = c.String(nullable: false),
                    Phone = c.String(nullable: false),
                    SkillType = c.String(nullable: false),
                    Skills = c.String(),
                    ProficiencyLevel = c.String(),
                    Certifications = c.String(),
                    CertificationExpiry = c.DateTime(),
                    JobTitle = c.String(),
                    EmployeeType = c.String(),
                    EmergencyContactName = c.String(),
                    EmergencyContactPhone = c.String(),
                    ShiftStart = c.Time(nullable: false, precision: 7),
                    ShiftEnd = c.Time(nullable: false, precision: 7),
                    CurrentStatus = c.String(),
                    DateJoined = c.DateTime(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    JobsCompletedThisMonth = c.Int(nullable: false),
                    AvgResponseTime = c.Int(nullable: false),
                    OnTimeCompletionRate = c.Int(nullable: false),
                    AttendanceScore = c.Int(nullable: false),
                    LeaveBalance = c.Int(nullable: false),
                    PhotoUrl = c.String(),
                    ReceiptFileName = c.String(),
                    UserId = c.Int(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.LeaveRequests",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    StaffId = c.Int(nullable: false),
                    Type = c.String(nullable: false),
                    StartDate = c.DateTime(nullable: false),
                    EndDate = c.DateTime(nullable: false),
                    Reason = c.String(),
                    Status = c.String(),
                    RequestedAt = c.DateTime(nullable: false),
                    ApprovedByUserId = c.Int(),
                    ApprovedAt = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.ApprovedByUserId)
                .ForeignKey("dbo.MaintenanceStaffs", t => t.StaffId)
                .Index(t => t.StaffId)
                .Index(t => t.ApprovedByUserId);

            CreateTable(
                "dbo.StaffShifts",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    StaffId = c.Int(nullable: false),
                    ShiftPatternId = c.Int(nullable: false),
                    Date = c.DateTime(nullable: false),
                    Notes = c.String(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ShiftPatterns", t => t.ShiftPatternId)
                .ForeignKey("dbo.MaintenanceStaffs", t => t.StaffId)
                .Index(t => t.StaffId)
                .Index(t => t.ShiftPatternId);

            CreateTable(
                "dbo.ShiftPatterns",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false),
                    StartTime = c.Time(nullable: false, precision: 7),
                    EndTime = c.Time(nullable: false, precision: 7),
                    Description = c.String(),
                })
                .PrimaryKey(t => t.Id);

            CreateTable(
                "dbo.Attendances",
                c => new
                {
                    AttendanceId = c.Int(nullable: false, identity: true),
                    Date = c.DateTime(nullable: false),
                    Status = c.Int(nullable: false),
                    StudentId = c.Int(nullable: false),
                    SubjectId = c.Int(nullable: false),
                    RecordedBy = c.String(),
                })
                .PrimaryKey(t => t.AttendanceId)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);

            CreateTable(
                "dbo.Categories",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 100),
                    Description = c.String(maxLength: 500),
                })
                .PrimaryKey(t => t.Id);

            CreateTable(
                "dbo.Products",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 200),
                    Description = c.String(maxLength: 1000),
                    Price = c.Double(nullable: false),
                    QuantityInStock = c.Int(nullable: false),
                    ReorderLevel = c.Int(nullable: false),
                    ImageUrl = c.String(maxLength: 500),
                    IsActive = c.Boolean(nullable: false),
                    CategoryId = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Categories", t => t.CategoryId)
                .Index(t => t.CategoryId);

            CreateTable(
                "dbo.OrderItems",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    OrderId = c.Int(nullable: false),
                    ProductId = c.Int(nullable: false),
                    Quantity = c.Int(nullable: false),
                    UnitPrice = c.Double(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Orders", t => t.OrderId, cascadeDelete: true)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .Index(t => t.OrderId)
                .Index(t => t.ProductId);

            CreateTable(
                "dbo.Orders",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    OrderNumber = c.String(nullable: false, maxLength: 50),
                    CustomerEmail = c.String(nullable: false, maxLength: 200),
                    CustomerName = c.String(nullable: false, maxLength: 100),
                    OrderDate = c.DateTime(nullable: false),
                    TotalAmount = c.Double(nullable: false),
                    Status = c.String(maxLength: 50),
                    Notes = c.String(maxLength: 500),
                })
                .PrimaryKey(t => t.Id);

            CreateTable(
                "dbo.DriverAvailabilities",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    DriverId = c.Int(nullable: false),
                    StartDate = c.DateTime(),
                    EndDate = c.DateTime(nullable: false),
                    Reason = c.String(nullable: false),
                    DateCreated = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId, cascadeDelete: true)
                .Index(t => t.DriverId);

            CreateTable(
                "dbo.Drivers",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    FullName = c.String(),
                    IDNumber = c.String(),
                    PhoneNumber = c.String(),
                    Email = c.String(),
                    LicenceNumber = c.String(),
                    LicenceExpiryDate = c.DateTime(),
                    HasPDP = c.Boolean(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    DateCreated = c.DateTime(),
                    PasswordHash = c.String(),
                    UserId = c.Int(),
                    ImageUrl = c.String(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.Invoices",
                c => new
                {
                    InvoiceId = c.Int(nullable: false, identity: true),
                    InvoiceNumber = c.String(nullable: false),
                    RegistrationId = c.Int(nullable: false),
                    StudentId = c.Int(nullable: false),
                    ParentId = c.Int(nullable: false),
                    InvoiceType = c.String(nullable: false),
                    Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Description = c.String(),
                    CreatedDate = c.DateTime(nullable: false),
                    DueDate = c.DateTime(nullable: false),
                    Status = c.String(),
                })
                .PrimaryKey(t => t.InvoiceId)
                .ForeignKey("dbo.Parents", t => t.ParentId)
                .ForeignKey("dbo.Registrations", t => t.RegistrationId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.RegistrationId)
                .Index(t => t.StudentId)
                .Index(t => t.ParentId);

            CreateTable(
                "dbo.Payments",
                c => new
                {
                    PaymentId = c.Int(nullable: false, identity: true),
                    InvoiceId = c.Int(nullable: false),
                    AmountPaid = c.Decimal(nullable: false, precision: 18, scale: 2),
                    PaymentDate = c.DateTime(nullable: false),
                    StripeChargeId = c.String(),
                    StripePaymentIntentId = c.String(),
                    Status = c.String(),
                    PaymentReference = c.String(),
                    ProofEmailSent = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.PaymentId)
                .ForeignKey("dbo.Invoices", t => t.InvoiceId)
                .Index(t => t.InvoiceId);

            CreateTable(
                "dbo.JobCardParts",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    JobCardId = c.Int(nullable: false),
                    InventoryItemId = c.Int(nullable: false),
                    QuantityUsed = c.Int(nullable: false),
                    DateUsed = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.MaintenanceInventories", t => t.InventoryItemId)
                .ForeignKey("dbo.JobCards", t => t.JobCardId, cascadeDelete: true)
                .Index(t => t.JobCardId)
                .Index(t => t.InventoryItemId);

            CreateTable(
                "dbo.MaintenanceInventories",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ItemName = c.String(nullable: false),
                    Category = c.String(nullable: false),
                    StockLevel = c.Int(nullable: false),
                    MinimumStock = c.Int(nullable: false),
                    Unit = c.String(),
                    UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Supplier = c.String(),
                    LastRestocked = c.DateTime(),
                })
                .PrimaryKey(t => t.Id);

            CreateTable(
                "dbo.Notifications",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    UserId = c.Int(nullable: false),
                    Message = c.String(),
                    IsRead = c.Boolean(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                    RelatedEntityType = c.String(),
                    RelatedEntityId = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);

            CreateTable(
                "dbo.PreventiveSchedules",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ScheduleName = c.String(nullable: false),
                    AssetId = c.Int(nullable: false),
                    TaskDescription = c.String(nullable: false),
                    Frequency = c.String(nullable: false),
                    SkillRequired = c.String(nullable: false),
                    StartDate = c.DateTime(nullable: false),
                    NextDueDate = c.DateTime(nullable: false),
                    LastCompletedDate = c.DateTime(),
                    Status = c.String(),
                    ComplianceStatus = c.String(),
                    CreatedById = c.Int(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Assets", t => t.AssetId)
                .Index(t => t.AssetId);

            CreateTable(
                "dbo.PurchaseOrderLines",
                c => new
                {
                    PurchaseOrderLineId = c.Int(nullable: false, identity: true),
                    PurchaseOrderId = c.Int(nullable: false),
                    ProductId = c.Int(nullable: false),
                    QuantityOrdered = c.Int(nullable: false),
                    UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    QuantityReceived = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.PurchaseOrderLineId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.PurchaseOrders", t => t.PurchaseOrderId, cascadeDelete: true)
                .Index(t => t.PurchaseOrderId)
                .Index(t => t.ProductId);

            CreateTable(
                "dbo.PurchaseOrders",
                c => new
                {
                    PurchaseOrderId = c.Int(nullable: false, identity: true),
                    PoNumber = c.String(nullable: false, maxLength: 50),
                    SupplierId = c.Int(nullable: false),
                    Status = c.Int(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                    SentAt = c.DateTime(),
                    ReceivedAt = c.DateTime(),
                    ExpectedDelivery = c.DateTime(),
                    Notes = c.String(maxLength: 1000),
                    EmailSent = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.PurchaseOrderId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => t.SupplierId);

            CreateTable(
                "dbo.Suppliers",
                c => new
                {
                    SupplierId = c.Int(nullable: false, identity: true),
                    Name = c.String(nullable: false, maxLength: 200),
                    ContactPerson = c.String(maxLength: 100),
                    Email = c.String(maxLength: 200),
                    Phone = c.String(maxLength: 20),
                    Address = c.String(maxLength: 500),
                    Website = c.String(maxLength: 200),
                    PaymentTermsDays = c.Int(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.SupplierId);

            CreateTable(
                "dbo.SupplierProducts",
                c => new
                {
                    SupplierProductId = c.Int(nullable: false, identity: true),
                    SupplierId = c.Int(nullable: false),
                    ProductId = c.Int(nullable: false),
                    UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    SupplierSku = c.String(maxLength: 100),
                    IsPreferred = c.Boolean(nullable: false),
                    MinOrderQty = c.Int(nullable: false),
                    LeadTimeDays = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.SupplierProductId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => t.SupplierId)
                .Index(t => t.ProductId);

            CreateTable(
                "dbo.StockMovements",
                c => new
                {
                    StockMovementId = c.Int(nullable: false, identity: true),
                    ProductId = c.Int(nullable: false),
                    MovementType = c.Int(nullable: false),
                    Quantity = c.Int(nullable: false),
                    StockAfter = c.Int(nullable: false),
                    Reference = c.String(maxLength: 500),
                    Notes = c.String(maxLength: 500),
                    CreatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.StockMovementId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .Index(t => t.ProductId);

            CreateTable(
                "dbo.StudentAttendanceTokens",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    TripScheduleId = c.Int(nullable: false),
                    Token = c.String(),
                    Type = c.String(),
                    IsUsed = c.Boolean(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                    ExpiryDate = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .ForeignKey("dbo.TripSchedules", t => t.TripScheduleId, cascadeDelete: true)
                .Index(t => t.StudentId)
                .Index(t => t.TripScheduleId);

            CreateTable(
                "dbo.TripSchedules",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TripRequestId = c.Int(nullable: false),
                    TeacherId = c.Int(nullable: false),
                    ScheduledDate = c.DateTime(nullable: false),
                    Status = c.String(maxLength: 20),
                    DriverId = c.Int(),
                    VehicleId = c.Int(),
                    Notes = c.String(maxLength: 500),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .ForeignKey("dbo.TripRequests", t => t.TripRequestId)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId)
                .Index(t => t.TripRequestId)
                .Index(t => t.TeacherId)
                .Index(t => t.DriverId)
                .Index(t => t.VehicleId);

            CreateTable(
                "dbo.TripRequests",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TeacherId = c.Int(nullable: false),
                    Title = c.String(nullable: false, maxLength: 200),
                    Description = c.String(),
                    DepartureTime = c.DateTime(nullable: false),
                    ReturnTime = c.DateTime(nullable: false),
                    Destination = c.String(),
                    MaxStudents = c.Int(nullable: false),
                    Status = c.String(maxLength: 20),
                    RejectionReason = c.String(maxLength: 500),
                    RequestedAt = c.DateTime(nullable: false),
                    ApprovedByAdminId = c.Int(),
                    ApprovedAt = c.DateTime(),
                    DestinationLat = c.Double(),
                    DestinationLng = c.Double(),
                    RebookedFromId = c.Int(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.ApprovedByAdminId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId)
                .Index(t => t.ApprovedByAdminId);

            CreateTable(
                "dbo.TripStudents",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TripScheduleId = c.Int(nullable: false),
                    StudentId = c.Int(nullable: false),
                    IsPresentBefore = c.Boolean(),
                    IsPresentAfter = c.Boolean(),
                    MarkedBeforeBy = c.String(),
                    MarkedAfterBy = c.String(),
                    MarkedBeforeAt = c.DateTime(),
                    MarkedAfterAt = c.DateTime(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.TripSchedules", t => t.TripScheduleId)
                .Index(t => t.TripScheduleId)
                .Index(t => t.StudentId);

            CreateTable(
                "dbo.Vehicles",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    VehicleNumber = c.String(nullable: false),
                    Model = c.String(nullable: false),
                    Type = c.String(nullable: false),
                    Capacity = c.Int(nullable: false),
                    IsActive = c.Boolean(nullable: false),
                    DateAdded = c.DateTime(nullable: false),
                    ImageUrl = c.String(),
                })
                .PrimaryKey(t => t.Id);

            CreateTable(
                "dbo.TripVehicleAssignments",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    TripScheduleId = c.Int(nullable: false),
                    VehicleId = c.Int(nullable: false),
                    DriverId = c.Int(nullable: false),
                    AllocatedSeats = c.Int(nullable: false),
                    CreatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId)
                .ForeignKey("dbo.TripSchedules", t => t.TripScheduleId, cascadeDelete: true)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId)
                .Index(t => t.TripScheduleId)
                .Index(t => t.VehicleId)
                .Index(t => t.DriverId);

            CreateTable(
                "dbo.TermResults",
                c => new
                {
                    TermResultId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    SubjectId = c.Int(nullable: false),
                    Grade = c.Int(nullable: false),
                    Term = c.Int(nullable: false),
                    AcademicYear = c.Int(nullable: false),
                    TermMarkPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Symbol = c.String(maxLength: 2),
                    Passed = c.Boolean(nullable: false),
                    CalculatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.TermResultId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);

            CreateTable(
                "dbo.VehicleIssues",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    DriverId = c.Int(nullable: false),
                    VehicleId = c.Int(nullable: false),
                    Description = c.String(),
                    DateReported = c.DateTime(nullable: false),
                    Status = c.String(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId, cascadeDelete: true)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId, cascadeDelete: true)
                .Index(t => t.DriverId)
                .Index(t => t.VehicleId);

            CreateTable(
                "dbo.YearResults",
                c => new
                {
                    YearResultId = c.Int(nullable: false, identity: true),
                    StudentId = c.Int(nullable: false),
                    Grade = c.Int(nullable: false),
                    AcademicYear = c.Int(nullable: false),
                    OverallPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                    PromotionStatus = c.String(nullable: false, maxLength: 20),
                    AdminNotes = c.String(maxLength: 500),
                    CalculatedAt = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.YearResultId)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .Index(t => t.StudentId);

        }

        public override void Down()
        {
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.VehicleIssues", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.VehicleIssues", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.TermResults", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TermResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StudentAttendanceTokens", "TripScheduleId", "dbo.TripSchedules");
            DropForeignKey("dbo.TripVehicleAssignments", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.TripVehicleAssignments", "TripScheduleId", "dbo.TripSchedules");
            DropForeignKey("dbo.TripVehicleAssignments", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.TripSchedules", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.TripStudents", "TripScheduleId", "dbo.TripSchedules");
            DropForeignKey("dbo.TripStudents", "StudentId", "dbo.Students");
            DropForeignKey("dbo.TripSchedules", "TripRequestId", "dbo.TripRequests");
            DropForeignKey("dbo.TripRequests", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TripRequests", "ApprovedByAdminId", "dbo.AppUsers");
            DropForeignKey("dbo.TripSchedules", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TripSchedules", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.StudentAttendanceTokens", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StockMovements", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "PurchaseOrderId", "dbo.PurchaseOrders");
            DropForeignKey("dbo.PurchaseOrders", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PreventiveSchedules", "AssetId", "dbo.Assets");
            DropForeignKey("dbo.Notifications", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.JobCardParts", "JobCardId", "dbo.JobCards");
            DropForeignKey("dbo.JobCardParts", "InventoryItemId", "dbo.MaintenanceInventories");
            DropForeignKey("dbo.Invoices", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Invoices", "RegistrationId", "dbo.Registrations");
            DropForeignKey("dbo.Payments", "InvoiceId", "dbo.Invoices");
            DropForeignKey("dbo.Invoices", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.DriverAvailabilities", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.Drivers", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.OrderItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.OrderItems", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.Products", "CategoryId", "dbo.Categories");
            DropForeignKey("dbo.Attendances", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Attendances", "StudentId", "dbo.Students");
            DropForeignKey("dbo.JobCards", "AssignedToId", "dbo.MaintenanceStaffs");
            DropForeignKey("dbo.MaintenanceStaffs", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.StaffShifts", "StaffId", "dbo.MaintenanceStaffs");
            DropForeignKey("dbo.StaffShifts", "ShiftPatternId", "dbo.ShiftPatterns");
            DropForeignKey("dbo.LeaveRequests", "StaffId", "dbo.MaintenanceStaffs");
            DropForeignKey("dbo.LeaveRequests", "ApprovedByUserId", "dbo.AppUsers");
            DropForeignKey("dbo.JobCards", "AssetId", "dbo.Assets");
            DropForeignKey("dbo.AdminReviews", "DriverAppId", "dbo.DriverApplications");
            DropForeignKey("dbo.DriverApplications", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.DriverDocuments", "DriverApplicationId", "dbo.DriverApplications");
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.Applications", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Applications", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Documents", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Students", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.StudentSubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StudentMarks", "AssessmentmentId", "dbo.Assessments");
            DropForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Teachers", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TimetableSlots", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TimetableSlots", "PeriodId", "dbo.Periods");
            DropForeignKey("dbo.TeacherAttendances", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherSubjectGrades", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherSubjectGrades", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StreamEnrolments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StreamEnrolments", "RegistrationId", "dbo.Registrations");
            DropForeignKey("dbo.Registrations", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Registrations", "AppId", "dbo.Applications");
            DropForeignKey("dbo.Assessments", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentSubjects", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Students", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Parents", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.Documents", "AppId", "dbo.Applications");
            DropIndex("dbo.YearResults", new[] { "StudentId" });
            DropIndex("dbo.VehicleIssues", new[] { "VehicleId" });
            DropIndex("dbo.VehicleIssues", new[] { "DriverId" });
            DropIndex("dbo.TermResults", new[] { "SubjectId" });
            DropIndex("dbo.TermResults", new[] { "StudentId" });
            DropIndex("dbo.TripVehicleAssignments", new[] { "DriverId" });
            DropIndex("dbo.TripVehicleAssignments", new[] { "VehicleId" });
            DropIndex("dbo.TripVehicleAssignments", new[] { "TripScheduleId" });
            DropIndex("dbo.TripStudents", new[] { "StudentId" });
            DropIndex("dbo.TripStudents", new[] { "TripScheduleId" });
            DropIndex("dbo.TripRequests", new[] { "ApprovedByAdminId" });
            DropIndex("dbo.TripRequests", new[] { "TeacherId" });
            DropIndex("dbo.TripSchedules", new[] { "VehicleId" });
            DropIndex("dbo.TripSchedules", new[] { "DriverId" });
            DropIndex("dbo.TripSchedules", new[] { "TeacherId" });
            DropIndex("dbo.TripSchedules", new[] { "TripRequestId" });
            DropIndex("dbo.StudentAttendanceTokens", new[] { "TripScheduleId" });
            DropIndex("dbo.StudentAttendanceTokens", new[] { "StudentId" });
            DropIndex("dbo.StockMovements", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrders", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "ProductId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "PurchaseOrderId" });
            DropIndex("dbo.PreventiveSchedules", new[] { "AssetId" });
            DropIndex("dbo.Notifications", new[] { "UserId" });
            DropIndex("dbo.JobCardParts", new[] { "InventoryItemId" });
            DropIndex("dbo.JobCardParts", new[] { "JobCardId" });
            DropIndex("dbo.Payments", new[] { "InvoiceId" });
            DropIndex("dbo.Invoices", new[] { "ParentId" });
            DropIndex("dbo.Invoices", new[] { "StudentId" });
            DropIndex("dbo.Invoices", new[] { "RegistrationId" });
            DropIndex("dbo.Drivers", new[] { "UserId" });
            DropIndex("dbo.DriverAvailabilities", new[] { "DriverId" });
            DropIndex("dbo.OrderItems", new[] { "ProductId" });
            DropIndex("dbo.OrderItems", new[] { "OrderId" });
            DropIndex("dbo.Products", new[] { "CategoryId" });
            DropIndex("dbo.Attendances", new[] { "SubjectId" });
            DropIndex("dbo.Attendances", new[] { "StudentId" });
            DropIndex("dbo.StaffShifts", new[] { "ShiftPatternId" });
            DropIndex("dbo.StaffShifts", new[] { "StaffId" });
            DropIndex("dbo.LeaveRequests", new[] { "ApprovedByUserId" });
            DropIndex("dbo.LeaveRequests", new[] { "StaffId" });
            DropIndex("dbo.MaintenanceStaffs", new[] { "UserId" });
            DropIndex("dbo.JobCards", new[] { "AssignedToId" });
            DropIndex("dbo.JobCards", new[] { "AssetId" });
            DropIndex("dbo.DriverDocuments", new[] { "DriverApplicationId" });
            DropIndex("dbo.DriverApplications", new[] { "UserId" });
            DropIndex("dbo.TimetableSlots", new[] { "Subject_SubjectId" });
            DropIndex("dbo.TimetableSlots", new[] { "SubjectId" });
            DropIndex("dbo.TimetableSlots", new[] { "TeacherId" });
            DropIndex("dbo.TimetableSlots", new[] { "PeriodId" });
            DropIndex("dbo.TeacherAttendances", new[] { "TeacherId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "SubjectId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "TeacherId" });
            DropIndex("dbo.Registrations", new[] { "StudentId" });
            DropIndex("dbo.Registrations", new[] { "AppId" });
            DropIndex("dbo.StreamEnrolments", new[] { "RegistrationId" });
            DropIndex("dbo.StreamEnrolments", new[] { "TeacherId" });
            DropIndex("dbo.StreamEnrolments", new[] { "StudentId" });
            DropIndex("dbo.Teachers", new[] { "UserId" });
            DropIndex("dbo.Assessments", new[] { "SubjectId" });
            DropIndex("dbo.Assessments", new[] { "TeacherId" });
            DropIndex("dbo.StudentMarks", new[] { "Subject_SubjectId" });
            DropIndex("dbo.StudentMarks", new[] { "StudentId" });
            DropIndex("dbo.StudentMarks", new[] { "AssessmentmentId" });
            DropIndex("dbo.Subjects", new[] { "TeacherId" });
            DropIndex("dbo.StudentSubjects", new[] { "SubjectId" });
            DropIndex("dbo.StudentSubjects", new[] { "StudentId" });
            DropIndex("dbo.Parents", new[] { "UserId" });
            DropIndex("dbo.Students", new[] { "UserId" });
            DropIndex("dbo.Students", new[] { "ParentId" });
            DropIndex("dbo.Documents", new[] { "AppId" });
            DropIndex("dbo.Documents", new[] { "StudentId" });
            DropIndex("dbo.Applications", new[] { "StudentId" });
            DropIndex("dbo.Applications", new[] { "ParentId" });
            DropIndex("dbo.AdminReviews", new[] { "DriverAppId" });
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            DropTable("dbo.YearResults");
            DropTable("dbo.VehicleIssues");
            DropTable("dbo.TermResults");
            DropTable("dbo.TripVehicleAssignments");
            DropTable("dbo.Vehicles");
            DropTable("dbo.TripStudents");
            DropTable("dbo.TripRequests");
            DropTable("dbo.TripSchedules");
            DropTable("dbo.StudentAttendanceTokens");
            DropTable("dbo.StockMovements");
            DropTable("dbo.SupplierProducts");
            DropTable("dbo.Suppliers");
            DropTable("dbo.PurchaseOrders");
            DropTable("dbo.PurchaseOrderLines");
            DropTable("dbo.PreventiveSchedules");
            DropTable("dbo.Notifications");
            DropTable("dbo.MaintenanceInventories");
            DropTable("dbo.JobCardParts");
            DropTable("dbo.Payments");
            DropTable("dbo.Invoices");
            DropTable("dbo.Drivers");
            DropTable("dbo.DriverAvailabilities");
            DropTable("dbo.Orders");
            DropTable("dbo.OrderItems");
            DropTable("dbo.Products");
            DropTable("dbo.Categories");
            DropTable("dbo.Attendances");
            DropTable("dbo.ShiftPatterns");
            DropTable("dbo.StaffShifts");
            DropTable("dbo.LeaveRequests");
            DropTable("dbo.MaintenanceStaffs");
            DropTable("dbo.JobCards");
            DropTable("dbo.Assets");
            DropTable("dbo.DriverDocuments");
            DropTable("dbo.DriverApplications");
            DropTable("dbo.Periods");
            DropTable("dbo.TimetableSlots");
            DropTable("dbo.TeacherAttendances");
            DropTable("dbo.TeacherSubjectGrades");
            DropTable("dbo.Registrations");
            DropTable("dbo.StreamEnrolments");
            DropTable("dbo.Teachers");
            DropTable("dbo.Assessments");
            DropTable("dbo.StudentMarks");
            DropTable("dbo.Subjects");
            DropTable("dbo.StudentSubjects");
            DropTable("dbo.AppUsers");
            DropTable("dbo.Parents");
            DropTable("dbo.Students");
            DropTable("dbo.Documents");
            DropTable("dbo.Applications");
            DropTable("dbo.AdminReviews");
        }
    }
}
