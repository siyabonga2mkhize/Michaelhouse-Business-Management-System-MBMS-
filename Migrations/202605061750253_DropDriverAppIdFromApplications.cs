namespace Michaelhouse.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class DropDriverAppIdFromApplications : DbMigration
    {
        public override void Up()
        {
            Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Applications' AND COLUMN_NAME = 'DriverAppId') " +
                "ALTER TABLE dbo.Applications DROP COLUMN DriverAppId;");
        }

        public override void Down()
        {
            // Optionally recreate the column if you ever roll back
            Sql("ALTER TABLE dbo.Applications ADD DriverAppId INT NULL;");
        }
    }
}
