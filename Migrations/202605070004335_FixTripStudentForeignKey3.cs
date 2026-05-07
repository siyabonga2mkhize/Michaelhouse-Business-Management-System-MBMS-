namespace Michaelhouse.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class FixTripStudentForeignKey3 : DbMigration
    {
        public override void Up()
        {
            // Drop the shadow column if it exists
            Sql("IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TripStudents') AND name = 'TripSchedule_Id') " +
                "ALTER TABLE dbo.TripStudents DROP COLUMN TripSchedule_Id;");
        }

        public override void Down()
        {
        }
    }
}
