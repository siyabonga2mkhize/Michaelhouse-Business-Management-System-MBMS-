namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherAttendances", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Attendances", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Attendances", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StreamEnrolments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropIndex("dbo.Subjects", new[] { "TeacherId" });
            DropIndex("dbo.StudentMarks", new[] { "Subject_SubjectId" });
            DropIndex("dbo.TeacherAttendances", new[] { "TeacherId" });
            DropIndex("dbo.TimetableSlots", new[] { "Subject_SubjectId" });
            DropIndex("dbo.Attendances", new[] { "StudentId" });
            DropIndex("dbo.Attendances", new[] { "SubjectId" });
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
            AddForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Students", "StudentId");
            AddForeignKey("dbo.YearResults", "StudentId", "dbo.Students", "StudentId", cascadeDelete: true);
            DropColumn("dbo.Students", "StudentNumber");
            DropColumn("dbo.Students", "Gender");
            DropColumn("dbo.Students", "GradeLevel");
            DropColumn("dbo.Students", "EnrollmentDate");
            DropColumn("dbo.Students", "ClassId");
            DropColumn("dbo.Subjects", "GradeLevel");
            DropColumn("dbo.Subjects", "TeacherId");
            DropColumn("dbo.StudentMarks", "Subject_SubjectId");
            DropColumn("dbo.Teachers", "Specialization");
            DropColumn("dbo.Teachers", "HireDate");
            DropColumn("dbo.TimetableSlots", "Subject_SubjectId");
            DropTable("dbo.TeacherAttendances");
            DropTable("dbo.Attendances");
        }
        
        public override void Down()
        {
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
                .PrimaryKey(t => t.AttendanceId);
            
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
                .PrimaryKey(t => t.TeacherAttendanceId);
            
            AddColumn("dbo.TimetableSlots", "Subject_SubjectId", c => c.Int());
            AddColumn("dbo.Teachers", "HireDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.Teachers", "Specialization", c => c.String());
            AddColumn("dbo.StudentMarks", "Subject_SubjectId", c => c.Int());
            AddColumn("dbo.Subjects", "TeacherId", c => c.Int());
            AddColumn("dbo.Subjects", "GradeLevel", c => c.Int(nullable: false));
            AddColumn("dbo.Students", "ClassId", c => c.Int());
            AddColumn("dbo.Students", "EnrollmentDate", c => c.DateTime(nullable: false));
            AddColumn("dbo.Students", "GradeLevel", c => c.Int(nullable: false));
            AddColumn("dbo.Students", "Gender", c => c.String(nullable: false));
            AddColumn("dbo.Students", "StudentNumber", c => c.String(nullable: false));
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StreamEnrolments", "TeacherId", "dbo.Students");
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
            CreateIndex("dbo.Attendances", "SubjectId");
            CreateIndex("dbo.Attendances", "StudentId");
            CreateIndex("dbo.TimetableSlots", "Subject_SubjectId");
            CreateIndex("dbo.TeacherAttendances", "TeacherId");
            CreateIndex("dbo.StudentMarks", "Subject_SubjectId");
            CreateIndex("dbo.Subjects", "TeacherId");
            AddForeignKey("dbo.YearResults", "StudentId", "dbo.Students", "StudentId");
            AddForeignKey("dbo.StreamEnrolments", "StudentId", "dbo.Students", "StudentId");
            AddForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers", "TeacherId");
            AddForeignKey("dbo.Attendances", "SubjectId", "dbo.Subjects", "SubjectId", cascadeDelete: true);
            AddForeignKey("dbo.Attendances", "StudentId", "dbo.Students", "StudentId", cascadeDelete: true);
            AddForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects", "SubjectId");
            AddForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects", "SubjectId");
            AddForeignKey("dbo.TeacherAttendances", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
            AddForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers", "TeacherId");
        }
    }
}
