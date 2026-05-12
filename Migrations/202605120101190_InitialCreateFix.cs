namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreateFix : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.VehicleIssues", "DriverId");
            CreateIndex("dbo.VehicleIssues", "VehicleId");
            AddForeignKey("dbo.VehicleIssues", "DriverId", "dbo.Drivers", "Id", cascadeDelete: true);
            AddForeignKey("dbo.VehicleIssues", "VehicleId", "dbo.Vehicles", "Id", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.VehicleIssues", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.VehicleIssues", "DriverId", "dbo.Drivers");
            DropIndex("dbo.VehicleIssues", new[] { "VehicleId" });
            DropIndex("dbo.VehicleIssues", new[] { "DriverId" });
        }
    }
}
