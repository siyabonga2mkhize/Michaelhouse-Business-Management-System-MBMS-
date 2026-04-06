namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StreamEnrolments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropIndex("dbo.StreamEnrolments", new[] { "StudentId" });
            CreateTable(
                "dbo.Categories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Products",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 1000),
                        Price = c.Double(nullable: false),
                        QuantityInStock = c.Int(nullable: false),
                        ReorderLevel = c.Int(nullable: false),
                        ImageUrl = c.String(maxLength: 500),
                        IsActive = c.Boolean(nullable: false),
                        CategoryId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Categories", t => t.CategoryId)
                .Index(t => t.CategoryId);
            
            CreateTable(
                "dbo.OrderItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        OrderId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitPrice = c.Double(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Orders", t => t.OrderId, cascadeDelete: true)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .Index(t => t.OrderId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.Orders",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        OrderNumber = c.String(nullable: false, maxLength: 50),
                        CustomerEmail = c.String(nullable: false, maxLength: 200),
                        CustomerName = c.String(nullable: false, maxLength: 100),
                        OrderDate = c.DateTime(nullable: false),
                        TotalAmount = c.Double(nullable: false),
                        Status = c.String(maxLength: 50),
                        Notes = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.StreamEnrolments", "TeacherId", c => c.Int(nullable: false));
            AlterColumn("dbo.Payments", "Status", c => c.String());
            CreateIndex("dbo.StreamEnrolments", "TeacherId");
            AddForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
            AddForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
            AddForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
            AddForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Students", "StudentId");
            AddForeignKey("dbo.YearResults", "StudentId", "dbo.Students", "StudentId", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Students");
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.OrderItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.OrderItems", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.Products", "CategoryId", "dbo.Categories");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers");
            DropIndex("dbo.OrderItems", new[] { "ProductId" });
            DropIndex("dbo.OrderItems", new[] { "OrderId" });
            DropIndex("dbo.Products", new[] { "CategoryId" });
            DropIndex("dbo.StreamEnrolments", new[] { "TeacherId" });
            AlterColumn("dbo.Payments", "Status", c => c.String(nullable: false));
            DropColumn("dbo.StreamEnrolments", "TeacherId");
            DropTable("dbo.Orders");
            DropTable("dbo.OrderItems");
            DropTable("dbo.Products");
            DropTable("dbo.Categories");
            CreateIndex("dbo.StreamEnrolments", "StudentId");
            AddForeignKey("dbo.YearResults", "StudentId", "dbo.Students", "StudentId");
            AddForeignKey("dbo.StreamEnrolments", "StudentId", "dbo.Students", "StudentId");
            AddForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers", "TeacherId");
            AddForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers", "TeacherId");
        }
    }
}
