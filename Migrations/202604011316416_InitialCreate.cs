namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AdminReviews",
                c => new
                    {
                        ReviewId = c.Int(nullable: false, identity: true),
                        AppId = c.Int(nullable: false),
                        AdminId = c.String(nullable: false),
                        Date = c.DateTime(nullable: false),
                        Decision = c.String(nullable: false),
                        AdminNotes = c.String(),
                        AgreedWithAi = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.ReviewId)
                .ForeignKey("dbo.Applications", t => t.AppId, cascadeDelete: true)
                .Index(t => t.AppId);
            
            CreateTable(
                "dbo.Applications",
                c => new
                    {
                        AppId = c.Int(nullable: false, identity: true),
                        ParentId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Date = c.DateTime(nullable: false),
                        ApplicationYear = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        AiReviewSummary = c.String(),
                        AiRecommendation = c.String(),
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
                        Name = c.String(nullable: false, maxLength: 200),
                        DOB = c.DateTime(nullable: false),
                        ParentId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.StudentId)
                .ForeignKey("dbo.Parents", t => t.ParentId, cascadeDelete: true)
                .Index(t => t.ParentId);
            
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
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.Applications", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Applications", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Documents", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Students", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Parents", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.Documents", "AppId", "dbo.Applications");
            DropIndex("dbo.Parents", new[] { "UserId" });
            DropIndex("dbo.Students", new[] { "ParentId" });
            DropIndex("dbo.Documents", new[] { "AppId" });
            DropIndex("dbo.Documents", new[] { "StudentId" });
            DropIndex("dbo.Applications", new[] { "StudentId" });
            DropIndex("dbo.Applications", new[] { "ParentId" });
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            DropTable("dbo.AppUsers");
            DropTable("dbo.Parents");
            DropTable("dbo.Students");
            DropTable("dbo.Documents");
            DropTable("dbo.Applications");
            DropTable("dbo.AdminReviews");
        }
    }
}
