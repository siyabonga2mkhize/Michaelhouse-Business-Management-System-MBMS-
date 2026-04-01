namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialFix : DbMigration
    {
        public override void Up()
        {
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
                "dbo.Students",
                c => new
                    {
                        StudentId = c.Int(nullable: false, identity: true),
                        FirstName = c.String(nullable: false, maxLength: 50),
                        LastName = c.String(nullable: false, maxLength: 50),
                        DateOfBirth = c.DateTime(nullable: false),
                        Gender = c.String(nullable: false, maxLength: 10),
                        GradeLevel = c.Int(nullable: false),
                        StudentNumber = c.String(),
                        Email = c.String(),
                        Phone = c.String(),
                        Address = c.String(),
                        EnrollmentDate = c.DateTime(nullable: false),
                        IsBoarder = c.Boolean(nullable: false),
                        Status = c.String(),
                        BoardingHouseId = c.Int(),
                        ParentId = c.Int(),
                    })
                .PrimaryKey(t => t.StudentId)
                .ForeignKey("dbo.BoardingHouses", t => t.BoardingHouseId)
                .ForeignKey("dbo.Parents", t => t.ParentId)
                .Index(t => t.BoardingHouseId)
                .Index(t => t.ParentId);
            
            CreateTable(
                "dbo.BoardingHouses",
                c => new
                    {
                        BoardingHouseId = c.Int(nullable: false, identity: true),
                        HouseName = c.String(nullable: false, maxLength: 100),
                        Capacity = c.Int(nullable: false),
                        CurrentOccupancy = c.Int(nullable: false),
                        HouseMaster = c.String(),
                    })
                .PrimaryKey(t => t.BoardingHouseId);
            
            CreateTable(
                "dbo.Rooms",
                c => new
                    {
                        RoomId = c.Int(nullable: false, identity: true),
                        RoomNumber = c.String(),
                        Capacity = c.Int(nullable: false),
                        Floor = c.Int(nullable: false),
                        BoardingHouseId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.RoomId)
                .ForeignKey("dbo.BoardingHouses", t => t.BoardingHouseId, cascadeDelete: true)
                .Index(t => t.BoardingHouseId);
            
            CreateTable(
                "dbo.Invoices",
                c => new
                    {
                        InvoiceId = c.Int(nullable: false, identity: true),
                        InvoiceNumber = c.String(),
                        Description = c.String(),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        AmountPaid = c.Decimal(nullable: false, precision: 18, scale: 2),
                        DueDate = c.DateTime(nullable: false),
                        IssueDate = c.DateTime(nullable: false),
                        Status = c.String(),
                        Category = c.String(),
                        StudentId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.InvoiceId)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.Parents",
                c => new
                    {
                        ParentId = c.Int(nullable: false, identity: true),
                        FirstName = c.String(nullable: false, maxLength: 50),
                        LastName = c.String(nullable: false, maxLength: 50),
                        Email = c.String(nullable: false),
                        Phone = c.String(nullable: false),
                        Address = c.String(),
                        Occupation = c.String(),
                        Relationship = c.String(),
                    })
                .PrimaryKey(t => t.ParentId);
            
            CreateTable(
                "dbo.Applications",
                c => new
                    {
                        ApplicationId = c.Int(nullable: false, identity: true),
                        StudentFirstName = c.String(nullable: false),
                        StudentLastName = c.String(nullable: false),
                        DateOfBirth = c.DateTime(nullable: false),
                        Gender = c.String(nullable: false),
                        ApplyingForGrade = c.Int(nullable: false),
                        PreviousSchool = c.String(),
                        PreviousMarks = c.Double(nullable: false),
                        ApplicationDate = c.DateTime(nullable: false),
                        Status = c.String(),
                        AdminComments = c.String(),
                        DocumentPath = c.String(),
                        RequiresBoarding = c.Boolean(nullable: false),
                        AIReviewScore = c.Int(),
                        ParentId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ApplicationId)
                .ForeignKey("dbo.Parents", t => t.ParentId, cascadeDelete: true)
                .Index(t => t.ParentId);
            
            CreateTable(
                "dbo.StudentMarks",
                c => new
                    {
                        StudentMarkId = c.Int(nullable: false, identity: true),
                        AssessmentName = c.String(),
                        AssessmentType = c.String(),
                        MarkObtained = c.Double(nullable: false),
                        TotalMarks = c.Double(nullable: false),
                        Weight = c.Double(nullable: false),
                        Term = c.Int(nullable: false),
                        Year = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.StudentMarkId)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.Subjects",
                c => new
                    {
                        SubjectId = c.Int(nullable: false, identity: true),
                        SubjectName = c.String(nullable: false, maxLength: 100),
                        SubjectCode = c.String(maxLength: 10),
                        GradeLevel = c.Int(nullable: false),
                        Credits = c.Int(nullable: false),
                        TeacherId = c.Int(),
                    })
                .PrimaryKey(t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId);
            
            CreateTable(
                "dbo.Teachers",
                c => new
                    {
                        TeacherId = c.Int(nullable: false, identity: true),
                        FirstName = c.String(nullable: false, maxLength: 50),
                        LastName = c.String(nullable: false, maxLength: 50),
                        Email = c.String(nullable: false),
                        Phone = c.String(),
                        EmployeeNumber = c.String(),
                        Department = c.String(),
                        Specialization = c.String(),
                        HireDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TeacherId);
            
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
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Attendances", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TeacherAttendances", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StudentMarks", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Students", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Applications", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Invoices", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Students", "BoardingHouseId", "dbo.BoardingHouses");
            DropForeignKey("dbo.Rooms", "BoardingHouseId", "dbo.BoardingHouses");
            DropForeignKey("dbo.Attendances", "StudentId", "dbo.Students");
            DropIndex("dbo.TeacherAttendances", new[] { "TeacherId" });
            DropIndex("dbo.Subjects", new[] { "TeacherId" });
            DropIndex("dbo.StudentMarks", new[] { "SubjectId" });
            DropIndex("dbo.StudentMarks", new[] { "StudentId" });
            DropIndex("dbo.Applications", new[] { "ParentId" });
            DropIndex("dbo.Invoices", new[] { "StudentId" });
            DropIndex("dbo.Rooms", new[] { "BoardingHouseId" });
            DropIndex("dbo.Students", new[] { "ParentId" });
            DropIndex("dbo.Students", new[] { "BoardingHouseId" });
            DropIndex("dbo.Attendances", new[] { "SubjectId" });
            DropIndex("dbo.Attendances", new[] { "StudentId" });
            DropTable("dbo.TeacherAttendances");
            DropTable("dbo.Teachers");
            DropTable("dbo.Subjects");
            DropTable("dbo.StudentMarks");
            DropTable("dbo.Applications");
            DropTable("dbo.Parents");
            DropTable("dbo.Invoices");
            DropTable("dbo.Rooms");
            DropTable("dbo.BoardingHouses");
            DropTable("dbo.Students");
            DropTable("dbo.Attendances");
        }
    }
}
