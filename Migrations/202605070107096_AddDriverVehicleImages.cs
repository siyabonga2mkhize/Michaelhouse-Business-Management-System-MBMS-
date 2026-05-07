using System.Data.Entity.Migrations;

public partial class AddDriverVehicleImages : DbMigration
{
    public override void Up()
    {
        AddColumn("dbo.Drivers", "ImageUrl", c => c.String());
        AddColumn("dbo.Vehicles", "ImageUrl", c => c.String());

    }

    public override void Down()
    {

    }


}
