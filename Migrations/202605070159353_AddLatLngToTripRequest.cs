using System;
using System.Data.Entity.Migrations;

public partial class AddLatLngToTripRequest : DbMigration
{
    public override void Up()
    {
        AddColumn("dbo.TripRequests", "DestinationLat", c => c.Double());
        AddColumn("dbo.TripRequests", "DestinationLng", c => c.Double());
    }
    
    public override void Down()
    {
        DropColumn("dbo.TripRequests", "DestinationLng");
        DropColumn("dbo.TripRequests", "DestinationLat");
    }
}
