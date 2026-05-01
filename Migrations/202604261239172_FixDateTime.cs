
namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FixDateTime : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.DriverApplications", "DateSubmitted", c => c.DateTime());
            AlterColumn("dbo.DriverApplications", "ReviewedDate", c => c.DateTime());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.DriverApplications", "ReviewedDate", c => c.DateTime(nullable: false));
            AlterColumn("dbo.DriverApplications", "DateSubmitted", c => c.DateTime(nullable: false));
        }
    }
}
