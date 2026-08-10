namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class MakeAssetIdNullableInJobCard : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.JobCards", "AssetId", c => c.Int(nullable: true));
        }
        
        public override void Down()
        {
        }
    }
}
