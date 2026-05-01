namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDriverTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Drivers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FullName = c.String(),
                        IDNumber = c.String(),
                        PhoneNumber = c.String(),
                        Email = c.String(),
                        LicenceNumber = c.String(),
                        LicenceExpiryDate = c.DateTime(nullable: false),
                        HasPDP = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        DateCreated = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.DriverApplications", "AdminNotes", c => c.String());
            AddColumn("dbo.DriverApplications", "ReviewedDate", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.DriverApplications", "ReviewedDate");
            DropColumn("dbo.DriverApplications", "AdminNotes");
            DropTable("dbo.Drivers");
        }
    }
}
