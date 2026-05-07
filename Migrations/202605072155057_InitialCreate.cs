namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers");
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            DropColumn("dbo.StreamEnrolments", "StudentId");
            RenameColumn(table: "dbo.StreamEnrolments", name: "TeacherId", newName: "StudentId");
            RenameIndex(table: "dbo.StreamEnrolments", name: "IX_TeacherId", newName: "IX_StudentId");
            CreateTable(
                "dbo.DriverApplications",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false),
                        IDNumber = c.String(nullable: false),
                        PhoneNumber = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        LicenceNumber = c.String(nullable: false),
                        LicenceExpiryDate = c.DateTime(nullable: false),
                        HasPDP = c.Boolean(nullable: false),
                        DocumentPath = c.String(),
                        Status = c.String(),
                        AdminNotes = c.String(),
                        DateSubmitted = c.DateTime(),
                        ReviewedDate = c.DateTime(),
                        UserId = c.Int(),
                        PublicTokenHash = c.String(),
                        PublicTokenExpiry = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.DriverDocuments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DriverApplicationId = c.Int(nullable: false),
                        FilePath = c.String(),
                        DocumentType = c.String(),
                        OtherDocumentType = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DriverApplications", t => t.DriverApplicationId, cascadeDelete: true)
                .Index(t => t.DriverApplicationId);
            
            CreateTable(
                "dbo.DriverAvailabilities",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DriverId = c.Int(nullable: false),
                        StartDate = c.DateTime(),
                        EndDate = c.DateTime(nullable: false),
                        Reason = c.String(nullable: false),
                        DateCreated = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId, cascadeDelete: true)
                .Index(t => t.DriverId);
            
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
                        LicenceExpiryDate = c.DateTime(),
                        HasPDP = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        DateCreated = c.DateTime(),
                        PasswordHash = c.String(),
                        UserId = c.Int(),
                        ImageUrl = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Notifications",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        Message = c.String(),
                        IsRead = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        RelatedEntityType = c.String(),
                        RelatedEntityId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.PurchaseOrderLines",
                c => new
                    {
                        PurchaseOrderLineId = c.Int(nullable: false, identity: true),
                        PurchaseOrderId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        QuantityOrdered = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        QuantityReceived = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.PurchaseOrderLineId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.PurchaseOrders", t => t.PurchaseOrderId, cascadeDelete: true)
                .Index(t => t.PurchaseOrderId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.PurchaseOrders",
                c => new
                    {
                        PurchaseOrderId = c.Int(nullable: false, identity: true),
                        PoNumber = c.String(nullable: false, maxLength: 50),
                        SupplierId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        SentAt = c.DateTime(),
                        ReceivedAt = c.DateTime(),
                        ExpectedDelivery = c.DateTime(),
                        Notes = c.String(maxLength: 1000),
                        EmailSent = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.PurchaseOrderId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => t.SupplierId);
            
            CreateTable(
                "dbo.Suppliers",
                c => new
                    {
                        SupplierId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 200),
                        ContactPerson = c.String(maxLength: 100),
                        Email = c.String(maxLength: 200),
                        Phone = c.String(maxLength: 20),
                        Address = c.String(maxLength: 500),
                        Website = c.String(maxLength: 200),
                        PaymentTermsDays = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.SupplierId);
            
            CreateTable(
                "dbo.SupplierProducts",
                c => new
                    {
                        SupplierProductId = c.Int(nullable: false, identity: true),
                        SupplierId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SupplierSku = c.String(maxLength: 100),
                        IsPreferred = c.Boolean(nullable: false),
                        MinOrderQty = c.Int(nullable: false),
                        LeadTimeDays = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.SupplierProductId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => t.SupplierId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.StockMovements",
                c => new
                    {
                        StockMovementId = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        MovementType = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        StockAfter = c.Int(nullable: false),
                        Reference = c.String(maxLength: 500),
                        Notes = c.String(maxLength: 500),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.StockMovementId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.TripRequests",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TeacherId = c.Int(nullable: false),
                        Title = c.String(nullable: false, maxLength: 200),
                        Description = c.String(),
                        DepartureTime = c.DateTime(nullable: false),
                        ReturnTime = c.DateTime(nullable: false),
                        Destination = c.String(),
                        MaxStudents = c.Int(nullable: false),
                        Status = c.String(maxLength: 20),
                        RejectionReason = c.String(maxLength: 500),
                        RequestedAt = c.DateTime(nullable: false),
                        ApprovedByAdminId = c.Int(),
                        ApprovedAt = c.DateTime(),
                        DestinationLat = c.Double(),
                        DestinationLng = c.Double(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AppUsers", t => t.ApprovedByAdminId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId)
                .Index(t => t.ApprovedByAdminId);
            
            CreateTable(
                "dbo.TripSchedules",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TripRequestId = c.Int(nullable: false),
                        TeacherId = c.Int(nullable: false),
                        ScheduledDate = c.DateTime(nullable: false),
                        Status = c.String(maxLength: 20),
                        DriverId = c.Int(),
                        VehicleId = c.Int(),
                        Notes = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Drivers", t => t.DriverId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .ForeignKey("dbo.TripRequests", t => t.TripRequestId)
                .ForeignKey("dbo.Vehicles", t => t.VehicleId)
                .Index(t => t.TripRequestId)
                .Index(t => t.TeacherId)
                .Index(t => t.DriverId)
                .Index(t => t.VehicleId);
            
            CreateTable(
                "dbo.TripStudents",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TripScheduleId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        IsPresentBefore = c.Boolean(),
                        IsPresentAfter = c.Boolean(),
                        MarkedBeforeBy = c.String(),
                        MarkedAfterBy = c.String(),
                        MarkedBeforeAt = c.DateTime(),
                        MarkedAfterAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.TripSchedules", t => t.TripScheduleId)
                .Index(t => t.TripScheduleId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.Vehicles",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        VehicleNumber = c.String(nullable: false),
                        Model = c.String(nullable: false),
                        Type = c.String(nullable: false),
                        Capacity = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        DateAdded = c.DateTime(nullable: false),
                        ImageUrl = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.VehicleIssues",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DriverId = c.Int(nullable: false),
                        VehicleId = c.Int(nullable: false),
                        Description = c.String(),
                        DateReported = c.DateTime(nullable: false),
                        Status = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.AdminReviews", "DriverAppId", c => c.Int());
            AlterColumn("dbo.AdminReviews", "AppId", c => c.Int());
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime());
            AlterColumn("dbo.StreamEnrolments", "TeacherId", c => c.Int());
            CreateIndex("dbo.AdminReviews", "AppId");
            CreateIndex("dbo.AdminReviews", "DriverAppId");
            CreateIndex("dbo.StreamEnrolments", "TeacherId");
            AddForeignKey("dbo.AdminReviews", "DriverAppId", "dbo.DriverApplications", "Id");
            AddForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications", "AppId");
            AddForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers", "TeacherId");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.TripSchedules", "VehicleId", "dbo.Vehicles");
            DropForeignKey("dbo.TripStudents", "TripScheduleId", "dbo.TripSchedules");
            DropForeignKey("dbo.TripStudents", "StudentId", "dbo.Students");
            DropForeignKey("dbo.TripSchedules", "TripRequestId", "dbo.TripRequests");
            DropForeignKey("dbo.TripSchedules", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TripSchedules", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.TripRequests", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TripRequests", "ApprovedByAdminId", "dbo.AppUsers");
            DropForeignKey("dbo.StockMovements", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "PurchaseOrderId", "dbo.PurchaseOrders");
            DropForeignKey("dbo.PurchaseOrders", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "ProductId", "dbo.Products");
            DropForeignKey("dbo.Notifications", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.DriverAvailabilities", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.Drivers", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.AdminReviews", "DriverAppId", "dbo.DriverApplications");
            DropForeignKey("dbo.DriverApplications", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.DriverDocuments", "DriverApplicationId", "dbo.DriverApplications");
            DropIndex("dbo.TripStudents", new[] { "StudentId" });
            DropIndex("dbo.TripStudents", new[] { "TripScheduleId" });
            DropIndex("dbo.TripSchedules", new[] { "VehicleId" });
            DropIndex("dbo.TripSchedules", new[] { "DriverId" });
            DropIndex("dbo.TripSchedules", new[] { "TeacherId" });
            DropIndex("dbo.TripSchedules", new[] { "TripRequestId" });
            DropIndex("dbo.TripRequests", new[] { "ApprovedByAdminId" });
            DropIndex("dbo.TripRequests", new[] { "TeacherId" });
            DropIndex("dbo.StockMovements", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrders", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "ProductId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "PurchaseOrderId" });
            DropIndex("dbo.Notifications", new[] { "UserId" });
            DropIndex("dbo.Drivers", new[] { "UserId" });
            DropIndex("dbo.DriverAvailabilities", new[] { "DriverId" });
            DropIndex("dbo.DriverDocuments", new[] { "DriverApplicationId" });
            DropIndex("dbo.DriverApplications", new[] { "UserId" });
            DropIndex("dbo.StreamEnrolments", new[] { "TeacherId" });
            DropIndex("dbo.AdminReviews", new[] { "DriverAppId" });
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            AlterColumn("dbo.StreamEnrolments", "TeacherId", c => c.Int(nullable: false));
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime(nullable: false));
            AlterColumn("dbo.AdminReviews", "AppId", c => c.Int(nullable: false));
            DropColumn("dbo.AdminReviews", "DriverAppId");
            DropTable("dbo.VehicleIssues");
            DropTable("dbo.Vehicles");
            DropTable("dbo.TripStudents");
            DropTable("dbo.TripSchedules");
            DropTable("dbo.TripRequests");
            DropTable("dbo.StockMovements");
            DropTable("dbo.SupplierProducts");
            DropTable("dbo.Suppliers");
            DropTable("dbo.PurchaseOrders");
            DropTable("dbo.PurchaseOrderLines");
            DropTable("dbo.Notifications");
            DropTable("dbo.Drivers");
            DropTable("dbo.DriverAvailabilities");
            DropTable("dbo.DriverDocuments");
            DropTable("dbo.DriverApplications");
            RenameIndex(table: "dbo.StreamEnrolments", name: "IX_StudentId", newName: "IX_TeacherId");
            RenameColumn(table: "dbo.StreamEnrolments", name: "StudentId", newName: "TeacherId");
            AddColumn("dbo.StreamEnrolments", "StudentId", c => c.Int(nullable: false));
            CreateIndex("dbo.AdminReviews", "AppId");
            AddForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
            AddForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications", "AppId", cascadeDelete: true);
        }
    }
}
