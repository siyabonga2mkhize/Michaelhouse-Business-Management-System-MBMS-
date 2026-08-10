namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialcreate2 : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.JobCardParts", "ExternalPartName");
            DropColumn("dbo.JobCardParts", "ExternalManufacturer");
            DropColumn("dbo.JobCardParts", "ExternalUnitCost");
        }
        
        public override void Down()
        {
            AddColumn("dbo.JobCardParts", "ExternalUnitCost", c => c.Decimal(precision: 18, scale: 2));
            AddColumn("dbo.JobCardParts", "ExternalManufacturer", c => c.String());
            AddColumn("dbo.JobCardParts", "ExternalPartName", c => c.String());
        }
    }
}
