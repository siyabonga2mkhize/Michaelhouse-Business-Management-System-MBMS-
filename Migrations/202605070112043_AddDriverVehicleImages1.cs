using System.Data.Entity.Migrations;

public partial class AddDriverVehicleImages1 : DbMigration
{

    public override void Up()
    {
        Sql("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Drivers') AND name = 'PasswordHash') " +
            "ALTER TABLE dbo.Drivers ADD PasswordHash NVARCHAR(MAX) NULL");
    }


    public override void Down()
    {
    }
}
