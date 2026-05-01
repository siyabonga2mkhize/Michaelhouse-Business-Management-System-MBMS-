namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MyApplications : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DriverApplications", "PublicTokenHash", c => c.String());
            AddColumn("dbo.DriverApplications", "PublicTokenExpiry", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.DriverApplications", "PublicTokenExpiry");
            DropColumn("dbo.DriverApplications", "PublicTokenHash");
        }
    }
}
