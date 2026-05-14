namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialcreate : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DriverApplications", "AiReviewSummary", c => c.String());
            AddColumn("dbo.DriverApplications", "AiRecommendation", c => c.String());
            AddColumn("dbo.DriverApplications", "AiScore", c => c.Int());
            AddColumn("dbo.DriverApplications", "AiReviewComplete", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.DriverApplications", "AiReviewComplete");
            DropColumn("dbo.DriverApplications", "AiScore");
            DropColumn("dbo.DriverApplications", "AiRecommendation");
            DropColumn("dbo.DriverApplications", "AiReviewSummary");
        }
    }
}
