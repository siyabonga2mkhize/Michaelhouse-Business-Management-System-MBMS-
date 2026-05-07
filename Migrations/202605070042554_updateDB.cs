using System.Data.Entity.Migrations;

namespace Michaelhouse.Migrations
{
    public partial class updateDB : DbMigration
    {
        public override void Up()
        {
            // Drop FK if it exists
            Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_dbo.TripStudents_dbo.TripSchedules_TripScheduleId')
    ALTER TABLE dbo.TripStudents DROP CONSTRAINT FK_dbo.TripStudents_dbo.TripSchedules_TripScheduleId;
");

            // Drop indexes if they exist
            Sql(@"
IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_TripScheduleId' AND object_id = object_id(N'[dbo].[TripStudents]', N'U'))
    DROP INDEX [IX_TripScheduleId] ON [dbo].[TripStudents];
IF EXISTS (SELECT name FROM sys.indexes WHERE name = N'IX_TripSchedule_Id' AND object_id = object_id(N'[dbo].[TripStudents]', N'U'))
    DROP INDEX [IX_TripSchedule_Id] ON [dbo].[TripStudents];
");

            // If a shadow column exists, rename it to TripScheduleId
            Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TripStudents') AND name = 'TripSchedule_Id')
BEGIN
    EXEC('EXEC sp_rename ''dbo.TripStudents.TripSchedule_Id'', ''TripScheduleId'', ''COLUMN''');
END
");

            // Ensure TripScheduleId column exists (add as nullable if missing), then make non-nullable
            Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TripStudents') AND name = 'TripScheduleId')
BEGIN
    ALTER TABLE dbo.TripStudents ADD TripScheduleId INT NULL;
END
");

            AlterColumn("dbo.TripStudents", "TripScheduleId", c => c.Int(nullable: false));

            CreateIndex("dbo.TripStudents", "TripScheduleId");
            AddForeignKey("dbo.TripStudents", "TripScheduleId", "dbo.TripSchedules", "Id", cascadeDelete: false);
        }

        public override void Down()
        {
            DropForeignKey("dbo.TripStudents", "TripScheduleId", "dbo.TripSchedules");
            DropIndex("dbo.TripStudents", new[] { "TripScheduleId" });
            AlterColumn("dbo.TripStudents", "TripScheduleId", c => c.Int());
            // preserve existing Down() semantics minimally
        }
    }
}
