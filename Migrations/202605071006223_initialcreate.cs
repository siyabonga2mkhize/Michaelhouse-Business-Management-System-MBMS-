namespace Michaelhouse.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class initialcreate : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.TripStudents", new[] { "TripScheduleId" });
            DropIndex("dbo.TripStudents", new[] { "TripSchedule_Id" });
            //DropColumn("dbo.TripStudents", "TripScheduleId");
            //RenameColumn(table: "dbo.TripStudents", name: "TripSchedule_Id", newName: "TripScheduleId");
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


        }


        public override void Down()
        {
            DropForeignKey("dbo.StockMovements", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "PurchaseOrderId", "dbo.PurchaseOrders");
            DropForeignKey("dbo.PurchaseOrders", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "ProductId", "dbo.Products");
            DropIndex("dbo.TripStudents", new[] { "TripScheduleId" });
            DropIndex("dbo.StockMovements", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrders", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "ProductId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "PurchaseOrderId" });
            AlterColumn("dbo.TripStudents", "TripScheduleId", c => c.Int());
            DropColumn("dbo.TripRequests", "DestinationLng");
            DropColumn("dbo.TripRequests", "DestinationLat");
            DropTable("dbo.StockMovements");
            DropTable("dbo.SupplierProducts");
            DropTable("dbo.Suppliers");
            DropTable("dbo.PurchaseOrders");
            DropTable("dbo.PurchaseOrderLines");
            RenameColumn(table: "dbo.TripStudents", name: "TripScheduleId", newName: "TripSchedule_Id");
            AddColumn("dbo.TripStudents", "TripScheduleId", c => c.Int(nullable: false));
            CreateIndex("dbo.TripStudents", "TripSchedule_Id");
            CreateIndex("dbo.TripStudents", "TripScheduleId");
        }
    }
}
