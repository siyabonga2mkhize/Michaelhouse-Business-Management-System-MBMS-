namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialcreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Notifications",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        Message = c.String(),
                        IsRead = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        RelatedEntityType = c.String(),
                        RelatedEntityId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.TripRequests",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TeacherId = c.Int(nullable: false),
                        Title = c.String(nullable: false, maxLength: 200),
                        Description = c.String(),
                        DepartureTime = c.DateTime(nullable: false),
                        ReturnTime = c.DateTime(nullable: false),
                        Destination = c.String(),
                        MaxStudents = c.Int(nullable: false),
                        Status = c.String(maxLength: 20),
                        RejectionReason = c.String(maxLength: 500),
                        RequestedAt = c.DateTime(nullable: false),
                        ApprovedByAdminId = c.Int(),
                        ApprovedAt = c.DateTime(),
                        DestinationLat = c.Double(),
                        DestinationLng = c.Double(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.ApprovedByAdminId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId)
                .Index(t => t.ApprovedByAdminId);
            
            CreateTable(
                "dbo.TripSchedules",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TripRequestId = c.Int(nullable: false),
                        TeacherId = c.Int(nullable: false),
                        ScheduledDate = c.DateTime(nullable: false),
                        Status = c.String(maxLength: 20),
                        DriverId = c.Int(),
                        VehicleId = c.Int(),
                        Notes = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .ForeignKey("dbo.TripRequests", t => t.TripRequestId)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId)
                .Index(t => t.TripRequestId)
                .Index(t => t.TeacherId)
                .Index(t => t.DriverId)
                .Index(t => t.VehicleId);
            
            CreateTable(
                "dbo.TripStudents",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TripScheduleId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        IsPresentBefore = c.Boolean(),
                        IsPresentAfter = c.Boolean(),
                        MarkedBeforeBy = c.String(),
                        MarkedAfterBy = c.String(),
                        MarkedBeforeAt = c.DateTime(),
                        MarkedAfterAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.TripSchedules", t => t.TripScheduleId)
                .Index(t => t.TripScheduleId)
                .Index(t => t.StudentId);
            
            AddColumn("dbo.Drivers", "ImageUrl", c => c.String());
            AddColumn("dbo.Vehicles", "ImageUrl", c => c.String());
            DropTable("dbo.Trips");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.Trips",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Destination = c.String(nullable: false),
                        TripDate = c.DateTime(nullable: false),
                        DriverId = c.Int(),
                        VehicleId = c.Int(),
                        Status = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            DropForeignKey("dbo.TripSchedules", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.TripStudents", "TripScheduleId", "dbo.TripSchedules");
            DropForeignKey("dbo.TripStudents", "StudentId", "dbo.Students");
            DropForeignKey("dbo.TripSchedules", "TripRequestId", "dbo.TripRequests");
            DropForeignKey("dbo.TripSchedules", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TripSchedules", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.TripRequests", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TripRequests", "ApprovedByAdminId", "dbo.AppUsers");
            DropForeignKey("dbo.Notifications", "UserId", "dbo.AppUsers");
            DropIndex("dbo.TripStudents", new[] { "StudentId" });
            DropIndex("dbo.TripStudents", new[] { "TripScheduleId" });
            DropIndex("dbo.TripSchedules", new[] { "VehicleId" });
            DropIndex("dbo.TripSchedules", new[] { "DriverId" });
            DropIndex("dbo.TripSchedules", new[] { "TeacherId" });
            DropIndex("dbo.TripSchedules", new[] { "TripRequestId" });
            DropIndex("dbo.TripRequests", new[] { "ApprovedByAdminId" });
            DropIndex("dbo.TripRequests", new[] { "TeacherId" });
            DropIndex("dbo.Notifications", new[] { "UserId" });
            DropColumn("dbo.Vehicles", "ImageUrl");
            DropColumn("dbo.Drivers", "ImageUrl");
            DropTable("dbo.TripStudents");
            DropTable("dbo.TripSchedules");
            DropTable("dbo.TripRequests");
            DropTable("dbo.Notifications");
        }
    }
}
