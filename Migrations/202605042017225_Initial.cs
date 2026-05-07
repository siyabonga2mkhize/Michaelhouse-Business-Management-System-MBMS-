namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Invoices", "OrderId", "dbo.Orders");
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            DropIndex("dbo.Invoices", new[] { "OrderId" });
            DropColumn("dbo.StreamEnrolments", "StudentId");
            RenameColumn(table: "dbo.StreamEnrolments", name: "TeacherId", newName: "StudentId");
            RenameIndex(table: "dbo.StreamEnrolments", name: "IX_TeacherId", newName: "IX_StudentId");
            CreateTable(
                "dbo.TeacherAttendances",
                c => new
                    {
                        TeacherAttendanceId = c.Int(nullable: false, identity: true),
                        Date = c.DateTime(nullable: false),
                        SignInTime = c.DateTime(),
                        SignOutTime = c.DateTime(),
                        Status = c.Int(nullable: false),
                        IsVerified = c.Boolean(nullable: false),
                        TeacherId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.TeacherAttendanceId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.TeacherId);
            
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
                "dbo.Attendances",
                c => new
                    {
                        AttendanceId = c.Int(nullable: false, identity: true),
                        Date = c.DateTime(nullable: false),
                        Status = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        RecordedBy = c.String(),
                    })
                .PrimaryKey(t => t.AttendanceId)
                .ForeignKey("dbo.Students", t => t.StudentId, cascadeDelete: true)
                .ForeignKey("dbo.Subjects", t => t.SubjectId, cascadeDelete: true)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);
            
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
                "dbo.Trips",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Destination = c.String(nullable: false),
                        TripDate = c.DateTime(nullable: false),
                        DriverId = c.Int(),
                        VehicleId = c.Int(),
                        Status = c.String(),
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
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.AdminReviews", "DriverAppId", c => c.Int());
            AddColumn("dbo.Students", "StudentNumber", c => c.String(nullable: false));
            AddColumn("dbo.Students", "Gender", c => c.String(nullable: false));
            AddColumn("dbo.Students", "GradeLevel", c => c.Int(nullable: false));
            AddColumn("dbo.Students", "EnrollmentDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.Students", "ClassId", c => c.Int());
            AddColumn("dbo.Subjects", "GradeLevel", c => c.Int(nullable: false));
            AddColumn("dbo.Subjects", "TeacherId", c => c.Int());
            AddColumn("dbo.StudentMarks", "Subject_SubjectId", c => c.Int());
            AddColumn("dbo.Teachers", "Specialization", c => c.String());
            AddColumn("dbo.Teachers", "HireDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.TimetableSlots", "Subject_SubjectId", c => c.Int());
            AlterColumn("dbo.AdminReviews", "AppId", c => c.Int());
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime());
            AlterColumn("dbo.StreamEnrolments", "TeacherId", c => c.Int());
            CreateIndex("dbo.AdminReviews", "AppId");
            CreateIndex("dbo.AdminReviews", "DriverAppId");
            CreateIndex("dbo.Subjects", "TeacherId");
            CreateIndex("dbo.StudentMarks", "Subject_SubjectId");
            CreateIndex("dbo.StreamEnrolments", "TeacherId");
            CreateIndex("dbo.TimetableSlots", "Subject_SubjectId");
            AddForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers", "TeacherId");
            AddForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects", "SubjectId");
            AddForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects", "SubjectId");
            AddForeignKey("dbo.AdminReviews", "DriverAppId", "dbo.DriverApplications", "Id");
            AddForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications", "AppId");
            AddForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers", "TeacherId");
            AddForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
            DropColumn("dbo.Invoices", "OrderId");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Invoices", "OrderId", c => c.Int());
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.StockMovements", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "PurchaseOrderId", "dbo.PurchaseOrders");
            DropForeignKey("dbo.PurchaseOrders", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "ProductId", "dbo.Products");
            DropForeignKey("dbo.DriverAvailabilities", "DriverId", "dbo.Drivers");
            DropForeignKey("dbo.Drivers", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.Attendances", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Attendances", "StudentId", "dbo.Students");
            DropForeignKey("dbo.AdminReviews", "DriverAppId", "dbo.DriverApplications");
            DropForeignKey("dbo.DriverApplications", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.DriverDocuments", "DriverApplicationId", "dbo.DriverApplications");
            DropForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TeacherAttendances", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers");
            DropIndex("dbo.StockMovements", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "ProductId" });
            DropIndex("dbo.SupplierProducts", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrders", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "ProductId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "PurchaseOrderId" });
            DropIndex("dbo.Drivers", new[] { "UserId" });
            DropIndex("dbo.DriverAvailabilities", new[] { "DriverId" });
            DropIndex("dbo.Attendances", new[] { "SubjectId" });
            DropIndex("dbo.Attendances", new[] { "StudentId" });
            DropIndex("dbo.DriverDocuments", new[] { "DriverApplicationId" });
            DropIndex("dbo.DriverApplications", new[] { "UserId" });
            DropIndex("dbo.TimetableSlots", new[] { "Subject_SubjectId" });
            DropIndex("dbo.TeacherAttendances", new[] { "TeacherId" });
            DropIndex("dbo.StreamEnrolments", new[] { "TeacherId" });
            DropIndex("dbo.StudentMarks", new[] { "Subject_SubjectId" });
            DropIndex("dbo.Subjects", new[] { "TeacherId" });
            DropIndex("dbo.AdminReviews", new[] { "DriverAppId" });
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            AlterColumn("dbo.StreamEnrolments", "TeacherId", c => c.Int(nullable: false));
            AlterColumn("dbo.AdminReviews", "Date", c => c.DateTime(nullable: false));
            AlterColumn("dbo.AdminReviews", "AppId", c => c.Int(nullable: false));
            DropColumn("dbo.TimetableSlots", "Subject_SubjectId");
            DropColumn("dbo.Teachers", "HireDate");
            DropColumn("dbo.Teachers", "Specialization");
            DropColumn("dbo.StudentMarks", "Subject_SubjectId");
            DropColumn("dbo.Subjects", "TeacherId");
            DropColumn("dbo.Subjects", "GradeLevel");
            DropColumn("dbo.Students", "ClassId");
            DropColumn("dbo.Students", "EnrollmentDate");
            DropColumn("dbo.Students", "GradeLevel");
            DropColumn("dbo.Students", "Gender");
            DropColumn("dbo.Students", "StudentNumber");
            DropColumn("dbo.AdminReviews", "DriverAppId");
            DropTable("dbo.Vehicles");
            DropTable("dbo.VehicleIssues");
            DropTable("dbo.Trips");
            DropTable("dbo.StockMovements");
            DropTable("dbo.SupplierProducts");
            DropTable("dbo.Suppliers");
            DropTable("dbo.PurchaseOrders");
            DropTable("dbo.PurchaseOrderLines");
            DropTable("dbo.Drivers");
            DropTable("dbo.DriverAvailabilities");
            DropTable("dbo.Attendances");
            DropTable("dbo.DriverDocuments");
            DropTable("dbo.DriverApplications");
            DropTable("dbo.TeacherAttendances");
            RenameIndex(table: "dbo.StreamEnrolments", name: "IX_StudentId", newName: "IX_TeacherId");
            RenameColumn(table: "dbo.StreamEnrolments", name: "StudentId", newName: "TeacherId");
            AddColumn("dbo.StreamEnrolments", "StudentId", c => c.Int(nullable: false));
            CreateIndex("dbo.Invoices", "OrderId");
            CreateIndex("dbo.AdminReviews", "AppId");
            AddForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers", "TeacherId");
            AddForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
            AddForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications", "AppId", cascadeDelete: true);
            AddForeignKey("dbo.Invoices", "OrderId", "dbo.Orders", "Id");
        }
    }
}
