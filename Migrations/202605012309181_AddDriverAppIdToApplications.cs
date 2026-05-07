namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDriverAppIdToApplications : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Applications", "DriverAppId", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Applications", "DriverAppId");
        }
    }
}
