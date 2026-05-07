namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Trips_Rebuild : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.TripStudentManifests",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TripId = c.Int(nullable: false),
                        VehicleId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .ForeignKey("dbo.Trips", t => t.TripId, cascadeDelete: true)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId, cascadeDelete: true)
                .Index(t => t.TripId)
                .Index(t => t.VehicleId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.TripVehicles",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TripId = c.Int(nullable: false),
                        VehicleId = c.Int(nullable: false),
                        DriverId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId, cascadeDelete: true)
                .ForeignKey("dbo.Trips", t => t.TripId, cascadeDelete: true)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId, cascadeDelete: true)
                .Index(t => t.TripId)
                .Index(t => t.VehicleId)
                .Index(t => t.DriverId);
            
            AddColumn("dbo.Trips", "StartTime", c => c.DateTime(nullable: false));
            AddColumn("dbo.Trips", "EndTime", c => c.DateTime(nullable: false));
            AddColumn("dbo.Trips", "PassengerCount", c => c.Int(nullable: false));
            AddColumn("dbo.Trips", "DateCreated", c => c.DateTime(nullable: false));
            CreateIndex("dbo.Trips", "DriverId");
            AddForeignKey("dbo.Trips", "DriverId", "dbo.Drivers", "Id");
            DropColumn("dbo.Trips", "TripDate");
            DropColumn("dbo.Trips", "VehicleId");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Trips", "VehicleId", c => c.Int());
            AddColumn("dbo.Trips", "TripDate", c => c.DateTime(nullable: false));
            DropForeignKey("dbo.TripVehicles", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.TripVehicles", "TripId", "dbo.Trips");
            DropForeignKey("dbo.TripVehicles", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.TripStudentManifests", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.TripStudentManifests", "TripId", "dbo.Trips");
            DropForeignKey("dbo.TripStudentManifests", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Trips", "DriverId", "dbo.Drivers");
            DropIndex("dbo.TripVehicles", new[] { "DriverId" });
            DropIndex("dbo.TripVehicles", new[] { "VehicleId" });
            DropIndex("dbo.TripVehicles", new[] { "TripId" });
            DropIndex("dbo.TripStudentManifests", new[] { "StudentId" });
            DropIndex("dbo.TripStudentManifests", new[] { "VehicleId" });
            DropIndex("dbo.TripStudentManifests", new[] { "TripId" });
            DropIndex("dbo.Trips", new[] { "DriverId" });
            DropColumn("dbo.Trips", "DateCreated");
            DropColumn("dbo.Trips", "PassengerCount");
            DropColumn("dbo.Trips", "EndTime");
            DropColumn("dbo.Trips", "StartTime");
            DropTable("dbo.TripVehicles");
            DropTable("dbo.TripStudentManifests");
        }
    }
}
