namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialcreate1 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.TripRequests", "RebookedFromId", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("dbo.TripRequests", "RebookedFromId");
        }
    }
}
