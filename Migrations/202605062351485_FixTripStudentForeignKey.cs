namespace Michaelhouse.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class FixTripStudentForeignKey : DbMigration
    {
        public override void Up()
        {
            Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TripStudents' AND COLUMN_NAME = 'TripSchedule_Id') " +
       "ALTER TABLE dbo.TripStudents DROP COLUMN TripSchedule_Id;");
        }

        public override void Down()
        {
        }
    }
}
