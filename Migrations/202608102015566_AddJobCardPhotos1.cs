namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddJobCardPhotos1 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.JobCardPhotoes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        JobCardId = c.Int(nullable: false),
                        FileName = c.String(),
                        Caption = c.String(),
                        UploadedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.JobCards", t => t.JobCardId, cascadeDelete: true)
                .Index(t => t.JobCardId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.JobCardPhotoes", "JobCardId", "dbo.JobCards");
            DropIndex("dbo.JobCardPhotoes", new[] { "JobCardId" });
            DropTable("dbo.JobCardPhotoes");
        }
    }
}
