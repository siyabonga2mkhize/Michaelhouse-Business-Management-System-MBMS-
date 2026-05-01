namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdatedStuff : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            AddColumn("dbo.AdminReviews", "DriverAppId", c => c.Int());
            AddColumn("dbo.DriverDocuments", "OtherDocumentType", c => c.String());
            AlterColumn("dbo.AdminReviews", "AppId", c => c.Int());
            CreateIndex("dbo.AdminReviews", "AppId");
            CreateIndex("dbo.AdminReviews", "DriverAppId");
            AddForeignKey("dbo.AdminReviews", "DriverAppId", "dbo.DriverApplications", "Id");
            AddForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications", "AppId");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.AdminReviews", "DriverAppId", "dbo.DriverApplications");
            DropIndex("dbo.AdminReviews", new[] { "DriverAppId" });
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            AlterColumn("dbo.AdminReviews", "AppId", c => c.Int(nullable: false));
            DropColumn("dbo.DriverDocuments", "OtherDocumentType");
            DropColumn("dbo.AdminReviews", "DriverAppId");
            CreateIndex("dbo.AdminReviews", "AppId");
            AddForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications", "AppId", cascadeDelete: true);
        }
    }
}
