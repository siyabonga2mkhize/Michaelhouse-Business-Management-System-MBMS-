namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddTeachersAndTimetable : DbMigration
    {
        public override void Up()
        {
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
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Teachers", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TimetableSlots", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TimetableSlots", "PeriodId", "dbo.Periods");
            DropForeignKey("dbo.TeacherSubjectGrades", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherSubjectGrades", "SubjectId", "dbo.Subjects");
            DropIndex("dbo.TimetableSlots", new[] { "SubjectId" });
            DropIndex("dbo.TimetableSlots", new[] { "TeacherId" });
            DropIndex("dbo.TimetableSlots", new[] { "PeriodId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "SubjectId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "TeacherId" });
            DropIndex("dbo.Teachers", new[] { "UserId" });
            DropTable("dbo.TimetableSlots");
            DropTable("dbo.TeacherSubjectGrades");
            DropTable("dbo.Teachers");
            DropTable("dbo.Periods");
        }
    }
}
