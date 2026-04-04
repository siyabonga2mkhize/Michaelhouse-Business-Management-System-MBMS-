namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddStreamsAndEnrolments : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StreamEnrolments",
                c => new
                    {
                        StreamEnrolmentId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        RegistrationId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        Stream = c.Int(nullable: false),
                        TakesMathematics = c.Boolean(nullable: false),
                        EnrolledAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.StreamEnrolmentId)
                .ForeignKey("dbo.Registrations", t => t.RegistrationId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId)
                .Index(t => t.RegistrationId);
            
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
            DropForeignKey("dbo.StreamEnrolments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StreamEnrolments", "RegistrationId", "dbo.Registrations");
            DropIndex("dbo.StreamEnrolments", new[] { "RegistrationId" });
            DropIndex("dbo.StreamEnrolments", new[] { "StudentId" });
            DropColumn("dbo.Subjects", "SortOrder");
            DropColumn("dbo.Subjects", "RequiresMaths");
            DropColumn("dbo.Subjects", "IsMathsOption");
            DropColumn("dbo.Subjects", "IsLanguage");
            DropColumn("dbo.Subjects", "Stream");
            DropColumn("dbo.Subjects", "Code");
            DropColumn("dbo.Subjects", "Name");
            DropColumn("dbo.StudentSubjects", "IsCompulsory");
            DropColumn("dbo.StudentSubjects", "Stream");
            DropTable("dbo.StreamEnrolments");
        }
    }
}
