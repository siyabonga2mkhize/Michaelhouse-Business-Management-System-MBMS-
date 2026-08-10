namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialcreate1 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.JobCardParts", "ExternalPartName", c => c.String());
            AddColumn("dbo.JobCardParts", "ExternalManufacturer", c => c.String());
            AddColumn("dbo.JobCardParts", "ExternalUnitCost", c => c.Decimal(precision: 18, scale: 2));
        }
        
        public override void Down()
        {
            DropColumn("dbo.JobCardParts", "ExternalUnitCost");
            DropColumn("dbo.JobCardParts", "ExternalManufacturer");
            DropColumn("dbo.JobCardParts", "ExternalPartName");
        }
    }
}
