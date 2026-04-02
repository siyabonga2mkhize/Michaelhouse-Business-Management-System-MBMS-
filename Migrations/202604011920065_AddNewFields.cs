namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddNewFields : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Applications", "GradeApplying", c => c.Int(nullable: false));
            AddColumn("dbo.Applications", "AdditionalNotes", c => c.String());
            AddColumn("dbo.Students", "FirstName", c => c.String(nullable: false, maxLength: 200));
            AddColumn("dbo.Students", "LastName", c => c.String(nullable: false, maxLength: 200));
            AddColumn("dbo.Students", "HomeLanguage", c => c.String(maxLength: 50));
            AddColumn("dbo.Students", "IdNumber", c => c.String(maxLength: 20));
            AddColumn("dbo.Students", "PreviousSchool", c => c.String(maxLength: 200));
            AddColumn("dbo.Students", "CurrentGrade", c => c.String(maxLength: 50));
            AddColumn("dbo.Students", "MedicalConditions", c => c.String(maxLength: 500));
            AddColumn("dbo.Students", "UserId", c => c.Int());
            DropColumn("dbo.Students", "Name");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Students", "Name", c => c.String(nullable: false, maxLength: 200));
            DropColumn("dbo.Students", "UserId");
            DropColumn("dbo.Students", "MedicalConditions");
            DropColumn("dbo.Students", "CurrentGrade");
            DropColumn("dbo.Students", "PreviousSchool");
            DropColumn("dbo.Students", "IdNumber");
            DropColumn("dbo.Students", "HomeLanguage");
            DropColumn("dbo.Students", "LastName");
            DropColumn("dbo.Students", "FirstName");
            DropColumn("dbo.Applications", "AdditionalNotes");
            DropColumn("dbo.Applications", "GradeApplying");
        }
    }
}
