namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Add_UserId_To_DriverApplication : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.DriverAvailabilities", "DriverApplication_Id", "dbo.DriverApplications");
            DropIndex("dbo.DriverAvailabilities", new[] { "DriverApplication_Id" });
            AddColumn("dbo.DriverApplications", "UserId", c => c.Int());
            AddColumn("dbo.Drivers", "PasswordHash", c => c.String());
            CreateIndex("dbo.DriverApplications", "UserId");
            CreateIndex("dbo.DriverAvailabilities", "DriverId");
            AddForeignKey("dbo.DriverApplications", "UserId", "dbo.AppUsers", "UserId");
            AddForeignKey("dbo.DriverAvailabilities", "DriverId", "dbo.Drivers", "Id", cascadeDelete: true);
            DropColumn("dbo.DriverAvailabilities", "DriverApplication_Id");
        }
        
        public override void Down()
        {
            AddColumn("dbo.DriverAvailabilities", "DriverApplication_Id", c => c.Int());
            DropForeignKey("dbo.DriverAvailabilities", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.DriverApplications", "UserId", "dbo.AppUsers");
            DropIndex("dbo.DriverAvailabilities", new[] { "DriverId" });
            DropIndex("dbo.DriverApplications", new[] { "UserId" });
            DropColumn("dbo.Drivers", "PasswordHash");
            DropColumn("dbo.DriverApplications", "UserId");
            CreateIndex("dbo.DriverAvailabilities", "DriverApplication_Id");
            AddForeignKey("dbo.DriverAvailabilities", "DriverApplication_Id", "dbo.DriverApplications", "Id");
        }
    }
}
