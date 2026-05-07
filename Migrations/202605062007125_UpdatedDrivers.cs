namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdatedDrivers : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DriverApplications", "InterviewDate", c => c.DateTime());
            AddColumn("dbo.DriverApplications", "InterviewStatus", c => c.String());
            AddColumn("dbo.DriverApplications", "ReviewStage", c => c.String());
            AddColumn("dbo.DriverApplications", "ManagerComments", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.DriverApplications", "ManagerComments");
            DropColumn("dbo.DriverApplications", "ReviewStage");
            DropColumn("dbo.DriverApplications", "InterviewStatus");
            DropColumn("dbo.DriverApplications", "InterviewDate");
        }
    }
}
