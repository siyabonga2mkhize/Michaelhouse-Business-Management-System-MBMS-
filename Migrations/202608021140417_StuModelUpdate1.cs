namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class StuModelUpdate1 : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Students", "StudentNumber", c => c.String());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Students", "StudentNumber", c => c.String(nullable: false));
        }
    }
}
