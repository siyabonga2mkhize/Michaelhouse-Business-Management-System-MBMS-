namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate1 : DbMigration
    {
        public override void Up()
        {
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
                    })
                .PrimaryKey(t => t.StudentMarkId)
                .ForeignKey("dbo.Assessments", t => t.AssessmentmentId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.AssessmentmentId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.Teachers",
                c => new
                    {
                        TeacherId = c.Int(nullable: false, identity: true),
                        FirstName = c.String(nullable: false, maxLength: 100),
                        LastName = c.String(nullable: false, maxLength: 100),
                        Email = c.String(nullable: false, maxLength: 200),
                        Phone = c.String(maxLength: 20),
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
                        TeacherId = c.Int(nullable: false),
                        RegistrationId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        Stream = c.Int(nullable: false),
                        TakesMathematics = c.Boolean(nullable: false),
                        EnrolledAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.StreamEnrolmentId)
                .ForeignKey("dbo.Registrations", t => t.RegistrationId)
                .ForeignKey("dbo.Students", t => t.TeacherId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.TeacherId)
                .Index(t => t.RegistrationId);
            
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
                    })
                .PrimaryKey(t => t.SlotId)
                .ForeignKey("dbo.Periods", t => t.PeriodId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.PeriodId)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId);
            
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
            
            AddColumn("dbo.StudentSubjects", "Stream", c => c.Int(nullable: false));
            AddColumn("dbo.StudentSubjects", "IsCompulsory", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "Name", c => c.String(nullable: false, maxLength: 200));
            AddColumn("dbo.Subjects", "Code", c => c.String(maxLength: 50));
            AddColumn("dbo.Subjects", "Stream", c => c.Int(nullable: false));
            AddColumn("dbo.Subjects", "IsLanguage", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "IsMathsOption", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "RequiresMaths", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "SortOrder", c => c.Int(nullable: false));
            DropColumn("dbo.StudentSubjects", "IsElective");
            DropColumn("dbo.Subjects", "SubjectName");
            DropColumn("dbo.Subjects", "SubjectCode");
            DropColumn("dbo.Subjects", "Category");
            DropColumn("dbo.Subjects", "Credits");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Subjects", "Credits", c => c.Int(nullable: false));
            AddColumn("dbo.Subjects", "Category", c => c.String(maxLength: 100));
            AddColumn("dbo.Subjects", "SubjectCode", c => c.String(maxLength: 10));
            AddColumn("dbo.Subjects", "SubjectName", c => c.String(nullable: false, maxLength: 100));
            AddColumn("dbo.StudentSubjects", "IsElective", c => c.Boolean(nullable: false));
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.TermResults", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TermResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.OrderItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.OrderItems", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.Products", "CategoryId", "dbo.Categories");
            DropForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Teachers", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TimetableSlots", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TimetableSlots", "PeriodId", "dbo.Periods");
            DropForeignKey("dbo.TeacherSubjectGrades", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherSubjectGrades", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Students");
            DropForeignKey("dbo.StreamEnrolments", "RegistrationId", "dbo.Registrations");
            DropForeignKey("dbo.Assessments", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StudentMarks", "AssessmentmentId", "dbo.Assessments");
            DropIndex("dbo.YearResults", new[] { "StudentId" });
            DropIndex("dbo.TermResults", new[] { "SubjectId" });
            DropIndex("dbo.TermResults", new[] { "StudentId" });
            DropIndex("dbo.OrderItems", new[] { "ProductId" });
            DropIndex("dbo.OrderItems", new[] { "OrderId" });
            DropIndex("dbo.Products", new[] { "CategoryId" });
            DropIndex("dbo.TimetableSlots", new[] { "SubjectId" });
            DropIndex("dbo.TimetableSlots", new[] { "TeacherId" });
            DropIndex("dbo.TimetableSlots", new[] { "PeriodId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "SubjectId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "TeacherId" });
            DropIndex("dbo.StreamEnrolments", new[] { "RegistrationId" });
            DropIndex("dbo.StreamEnrolments", new[] { "TeacherId" });
            DropIndex("dbo.Teachers", new[] { "UserId" });
            DropIndex("dbo.StudentMarks", new[] { "StudentId" });
            DropIndex("dbo.StudentMarks", new[] { "AssessmentmentId" });
            DropIndex("dbo.Assessments", new[] { "SubjectId" });
            DropIndex("dbo.Assessments", new[] { "TeacherId" });
            DropColumn("dbo.Subjects", "SortOrder");
            DropColumn("dbo.Subjects", "RequiresMaths");
            DropColumn("dbo.Subjects", "IsMathsOption");
            DropColumn("dbo.Subjects", "IsLanguage");
            DropColumn("dbo.Subjects", "Stream");
            DropColumn("dbo.Subjects", "Code");
            DropColumn("dbo.Subjects", "Name");
            DropColumn("dbo.StudentSubjects", "IsCompulsory");
            DropColumn("dbo.StudentSubjects", "Stream");
            DropTable("dbo.YearResults");
            DropTable("dbo.TermResults");
            DropTable("dbo.Orders");
            DropTable("dbo.OrderItems");
            DropTable("dbo.Products");
            DropTable("dbo.Categories");
            DropTable("dbo.Periods");
            DropTable("dbo.TimetableSlots");
            DropTable("dbo.TeacherSubjectGrades");
            DropTable("dbo.StreamEnrolments");
            DropTable("dbo.Teachers");
            DropTable("dbo.StudentMarks");
            DropTable("dbo.Assessments");
        }
    }
}
