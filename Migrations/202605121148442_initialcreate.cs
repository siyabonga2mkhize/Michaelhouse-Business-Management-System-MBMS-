namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialcreate : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DriverApplications", "InterviewDateTime", c => c.DateTime());
            AddColumn("dbo.DriverApplications", "InterviewMeetingLink", c => c.String());
            AddColumn("dbo.DriverApplications", "InterviewEmailSent", c => c.Boolean(nullable: false));
            AddColumn("dbo.DriverApplications", "InterviewEmailSentAt", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.DriverApplications", "InterviewEmailSentAt");
            DropColumn("dbo.DriverApplications", "InterviewEmailSent");
            DropColumn("dbo.DriverApplications", "InterviewMeetingLink");
            DropColumn("dbo.DriverApplications", "InterviewDateTime");
        }
    }
}
