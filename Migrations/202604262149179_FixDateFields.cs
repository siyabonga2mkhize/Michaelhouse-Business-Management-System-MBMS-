namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FixDateFields : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime());
            AlterColumn("dbo.DriverAvailabilities", "StartDate", c => c.DateTime());
            AlterColumn("dbo.DriverAvailabilities", "DateCreated", c => c.DateTime());
            AlterColumn("dbo.Drivers", "LicenceExpiryDate", c => c.DateTime());
            AlterColumn("dbo.Drivers", "DateCreated", c => c.DateTime());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Drivers", "DateCreated", c => c.DateTime(nullable: false));
            AlterColumn("dbo.Drivers", "LicenceExpiryDate", c => c.DateTime(nullable: false));
            AlterColumn("dbo.DriverAvailabilities", "DateCreated", c => c.DateTime(nullable: false));
            AlterColumn("dbo.DriverAvailabilities", "StartDate", c => c.DateTime(nullable: false));
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime(nullable: false));
        }
    }
}
