namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FixedVers : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Drivers", "UserId", c => c.Int());
            CreateIndex("dbo.Drivers", "UserId");
            AddForeignKey("dbo.Drivers", "UserId", "dbo.AppUsers", "UserId");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Drivers", "UserId", "dbo.AppUsers");
            DropIndex("dbo.Drivers", new[] { "UserId" });
            DropColumn("dbo.Drivers", "UserId");
        }
    }
}
