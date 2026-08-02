namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class StuModelUpdate : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.Students", "Gender");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Students", "Gender", c => c.String(nullable: false));
        }
    }
}
