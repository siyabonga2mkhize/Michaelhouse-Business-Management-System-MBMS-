namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialcreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StudentAttendanceTokens",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        TripScheduleId = c.Int(nullable: false),
                        Token = c.String(),
                        Type = c.String(),
                        IsUsed = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        ExpiryDate = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .ForeignKey("dbo.TripSchedules", t => t.TripScheduleId, cascadeDelete: true)
                .Index(t => t.StudentId)
                .Index(t => t.TripScheduleId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StudentAttendanceTokens", "TripScheduleId", "dbo.TripSchedules");
            DropForeignKey("dbo.StudentAttendanceTokens", "StudentId", "dbo.Students");
            DropIndex("dbo.StudentAttendanceTokens", new[] { "TripScheduleId" });
            DropIndex("dbo.StudentAttendanceTokens", new[] { "StudentId" });
            DropTable("dbo.StudentAttendanceTokens");
        }
    }
}
