namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCaptureMarkAndReport : DbMigration
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
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
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
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.TermResults", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TermResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Assessments", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StudentMarks", "AssessmentmentId", "dbo.Assessments");
            DropIndex("dbo.YearResults", new[] { "StudentId" });
            DropIndex("dbo.TermResults", new[] { "SubjectId" });
            DropIndex("dbo.TermResults", new[] { "StudentId" });
            DropIndex("dbo.StudentMarks", new[] { "StudentId" });
            DropIndex("dbo.StudentMarks", new[] { "AssessmentmentId" });
            DropIndex("dbo.Assessments", new[] { "SubjectId" });
            DropIndex("dbo.Assessments", new[] { "TeacherId" });
            DropTable("dbo.YearResults");
            DropTable("dbo.TermResults");
            DropTable("dbo.StudentMarks");
            DropTable("dbo.Assessments");
        }
    }
}
