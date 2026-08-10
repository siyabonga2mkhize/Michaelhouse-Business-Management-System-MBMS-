namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddManualFaultFields : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Assets", "AssetId", c => c.Int());
            AddColumn("dbo.Assets", "ManualAssetName", c => c.String());
            AddColumn("dbo.Assets", "ManualAssetLocation", c => c.String());
            AddColumn("dbo.Assets", "ManualCategory", c => c.String());
            AddColumn("dbo.JobCards", "ManualAssetName", c => c.String());
            AddColumn("dbo.JobCards", "ManualAssetLocation", c => c.String());
            AddColumn("dbo.JobCards", "ManualCategory", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.JobCards", "ManualCategory");
            DropColumn("dbo.JobCards", "ManualAssetLocation");
            DropColumn("dbo.JobCards", "ManualAssetName");
            DropColumn("dbo.Assets", "ManualCategory");
            DropColumn("dbo.Assets", "ManualAssetLocation");
            DropColumn("dbo.Assets", "ManualAssetName");
            DropColumn("dbo.Assets", "AssetId");
        }
    }
}
