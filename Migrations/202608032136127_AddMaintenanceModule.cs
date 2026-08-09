namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMaintenanceModule : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Assets",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        AssetName = c.String(nullable: false),
                        Category = c.String(nullable: false),
                        LocationBuilding = c.String(nullable: false),
                        LocationRoom = c.String(),
                        Description = c.String(),
                        ConditionRating = c.String(),
                        QrCode = c.String(),
                        HealthScore = c.Int(nullable: false),
                        FaultCount = c.Int(nullable: false),
                        Status = c.String(),
                        DateRegistered = c.DateTime(nullable: false),
                        RegisteredById = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.JobCards",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        JobReference = c.String(),
                        Title = c.String(nullable: false),
                        Description = c.String(),
                        JobType = c.String(),
                        Priority = c.String(nullable: false),
                        Status = c.String(),
                        PhotoBefore = c.String(),
                        PhotoAfter = c.String(),
                        CompletionNotes = c.String(),
                        FinalCondition = c.String(),
                        DateCreated = c.DateTime(nullable: false),
                        DateAssigned = c.DateTime(),
                        DateCompleted = c.DateTime(),
                        DueDate = c.DateTime(),
                        ResponseTimeMinutes = c.Int(),
                        AssetId = c.Int(nullable: false),
                        AssignedToId = c.Int(),
                        ReportedById = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Assets", t => t.AssetId)
                .ForeignKey("dbo.MaintenanceStaffs", t => t.AssignedToId)
                .Index(t => t.AssetId)
                .Index(t => t.AssignedToId);
            
            CreateTable(
                "dbo.MaintenanceStaffs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false),
                        StaffNumber = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        Phone = c.String(nullable: false),
                        SkillType = c.String(nullable: false),
                        ShiftStart = c.Time(nullable: false, precision: 7),
                        ShiftEnd = c.Time(nullable: false, precision: 7),
                        CurrentStatus = c.String(),
                        DateJoined = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        UserId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.MaintenanceInventories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ItemName = c.String(nullable: false),
                        Category = c.String(nullable: false),
                        StockLevel = c.Int(nullable: false),
                        MinimumStock = c.Int(nullable: false),
                        Unit = c.String(),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Supplier = c.String(),
                        LastRestocked = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.JobCardParts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        JobCardId = c.Int(nullable: false),
                        InventoryItemId = c.Int(nullable: false),
                        QuantityUsed = c.Int(nullable: false),
                        DateUsed = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.MaintenanceInventories", t => t.InventoryItemId)
                .ForeignKey("dbo.JobCards", t => t.JobCardId, cascadeDelete: true)
                .Index(t => t.JobCardId)
                .Index(t => t.InventoryItemId);
            
            CreateTable(
                "dbo.PreventiveSchedules",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ScheduleName = c.String(nullable: false),
                        AssetId = c.Int(nullable: false),
                        TaskDescription = c.String(nullable: false),
                        Frequency = c.String(nullable: false),
                        SkillRequired = c.String(nullable: false),
                        StartDate = c.DateTime(nullable: false),
                        NextDueDate = c.DateTime(nullable: false),
                        LastCompletedDate = c.DateTime(),
                        Status = c.String(),
                        ComplianceStatus = c.String(),
                        CreatedById = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Assets", t => t.AssetId)
                .Index(t => t.AssetId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PreventiveSchedules", "AssetId", "dbo.Assets");
            DropForeignKey("dbo.JobCardParts", "JobCardId", "dbo.JobCards");
            DropForeignKey("dbo.JobCardParts", "InventoryItemId", "dbo.MaintenanceInventories");
            DropForeignKey("dbo.JobCards", "AssignedToId", "dbo.MaintenanceStaffs");
            DropForeignKey("dbo.MaintenanceStaffs", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.JobCards", "AssetId", "dbo.Assets");
            DropIndex("dbo.PreventiveSchedules", new[] { "AssetId" });
            DropIndex("dbo.JobCardParts", new[] { "InventoryItemId" });
            DropIndex("dbo.JobCardParts", new[] { "JobCardId" });
            DropIndex("dbo.MaintenanceStaffs", new[] { "UserId" });
            DropIndex("dbo.JobCards", new[] { "AssignedToId" });
            DropIndex("dbo.JobCards", new[] { "AssetId" });
            DropTable("dbo.PreventiveSchedules");
            DropTable("dbo.JobCardParts");
            DropTable("dbo.MaintenanceInventories");
            DropTable("dbo.MaintenanceStaffs");
            DropTable("dbo.JobCards");
            DropTable("dbo.Assets");
        }
    }
}
