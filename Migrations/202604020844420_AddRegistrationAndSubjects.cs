namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRegistrationAndSubjects : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StudentSubjects",
                c => new
                    {
                        StudentSubjectId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        IsElective = c.Boolean(nullable: false),
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
                        SubjectName = c.String(nullable: false, maxLength: 100),
                        SubjectCode = c.String(maxLength: 10),
                        Category = c.String(maxLength: 100),
                        IsCompulsory = c.Boolean(nullable: false),
                        ApplicableGrades = c.String(),
                        Credits = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.SubjectId);
            
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
            
            CreateIndex("dbo.Students", "UserId");
            AddForeignKey("dbo.Students", "UserId", "dbo.AppUsers", "UserId");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Registrations", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Registrations", "AppId", "dbo.Applications");
            DropForeignKey("dbo.Students", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.StudentSubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentSubjects", "StudentId", "dbo.Students");
            DropIndex("dbo.Registrations", new[] { "StudentId" });
            DropIndex("dbo.Registrations", new[] { "AppId" });
            DropIndex("dbo.StudentSubjects", new[] { "SubjectId" });
            DropIndex("dbo.StudentSubjects", new[] { "StudentId" });
            DropIndex("dbo.Students", new[] { "UserId" });
            DropTable("dbo.Registrations");
            DropTable("dbo.Subjects");
            DropTable("dbo.StudentSubjects");
        }
    }
}
