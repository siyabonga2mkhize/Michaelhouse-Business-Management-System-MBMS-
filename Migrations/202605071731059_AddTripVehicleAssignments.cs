namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddTripVehicleAssignments : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.TripVehicleAssignments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TripScheduleId = c.Int(nullable: false),
                        VehicleId = c.Int(nullable: false),
                        DriverId = c.Int(nullable: false),
                        AllocatedSeats = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId)
                .ForeignKey("dbo.TripSchedules", t => t.TripScheduleId, cascadeDelete: true)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId)
                .Index(t => t.TripScheduleId)
                .Index(t => t.VehicleId)
                .Index(t => t.DriverId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TripVehicleAssignments", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.TripVehicleAssignments", "TripScheduleId", "dbo.TripSchedules");
            DropForeignKey("dbo.TripVehicleAssignments", "DriverId", "dbo.Drivers");
            DropIndex("dbo.TripVehicleAssignments", new[] { "DriverId" });
            DropIndex("dbo.TripVehicleAssignments", new[] { "VehicleId" });
            DropIndex("dbo.TripVehicleAssignments", new[] { "TripScheduleId" });
            DropTable("dbo.TripVehicleAssignments");
        }
    }
}
