namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AdReviewModelUpdate : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime());
        }
    }
}
