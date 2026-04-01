namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddParentProfileFields : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Parents", "CellPhone", c => c.String(maxLength: 20));
            AddColumn("dbo.Parents", "WorkPhone", c => c.String(maxLength: 20));
            AddColumn("dbo.Parents", "HomePhone", c => c.String(maxLength: 20));
            AddColumn("dbo.Parents", "PhysicalAddress", c => c.String(maxLength: 300));
            AddColumn("dbo.Parents", "PostalAddress", c => c.String(maxLength: 300));
            AddColumn("dbo.Parents", "Relationship", c => c.String(maxLength: 100));
            AddColumn("dbo.Parents", "Occupation", c => c.String(maxLength: 200));
            AddColumn("dbo.Parents", "Employer", c => c.String(maxLength: 200));
            AddColumn("dbo.Parents", "EmergencyContactName", c => c.String(maxLength: 200));
            AddColumn("dbo.Parents", "EmergencyContactPhone", c => c.String(maxLength: 20));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Parents", "EmergencyContactPhone");
            DropColumn("dbo.Parents", "EmergencyContactName");
            DropColumn("dbo.Parents", "Employer");
            DropColumn("dbo.Parents", "Occupation");
            DropColumn("dbo.Parents", "Relationship");
            DropColumn("dbo.Parents", "PostalAddress");
            DropColumn("dbo.Parents", "PhysicalAddress");
            DropColumn("dbo.Parents", "HomePhone");
            DropColumn("dbo.Parents", "WorkPhone");
            DropColumn("dbo.Parents", "CellPhone");
        }
    }
}
