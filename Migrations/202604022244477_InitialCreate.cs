namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AdminReviews",
                c => new
                    {
                        ReviewId = c.Int(nullable: false, identity: true),
                        AppId = c.Int(nullable: false),
                        AdminId = c.String(nullable: false),
                        Date = c.DateTime(nullable: false),
                        Decision = c.String(nullable: false),
                        AdminNotes = c.String(),
                        AgreedWithAi = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.ReviewId)
                .ForeignKey("dbo.Applications", t => t.AppId, cascadeDelete: true)
                .Index(t => t.AppId);
            
            CreateTable(
                "dbo.Applications",
                c => new
                    {
                        AppId = c.Int(nullable: false, identity: true),
                        ParentId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Date = c.DateTime(nullable: false),
                        ApplicationYear = c.Int(nullable: false),
                        GradeApplying = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        AiReviewSummary = c.String(),
                        AiRecommendation = c.String(),
                        AdditionalNotes = c.String(),
                    })
                .PrimaryKey(t => t.AppId)
                .ForeignKey("dbo.Parents", t => t.ParentId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.ParentId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.Documents",
                c => new
                    {
                        DId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        AppId = c.Int(nullable: false),
                        Type = c.String(nullable: false, maxLength: 100),
                        FilePath = c.String(nullable: false),
                        FileName = c.String(),
                        ContentType = c.String(),
                        UploadedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.DId)
                .ForeignKey("dbo.Applications", t => t.AppId, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId)
                .Index(t => t.AppId);
            
            CreateTable(
                "dbo.Students",
                c => new
                    {
                        StudentId = c.Int(nullable: false, identity: true),
                        FirstName = c.String(nullable: false, maxLength: 200),
                        LastName = c.String(nullable: false, maxLength: 200),
                        DOB = c.DateTime(nullable: false),
                        HomeLanguage = c.String(maxLength: 50),
                        IdNumber = c.String(maxLength: 20),
                        PreviousSchool = c.String(maxLength: 200),
                        CurrentGrade = c.String(maxLength: 50),
                        MedicalConditions = c.String(maxLength: 500),
                        ParentId = c.Int(nullable: false),
                        UserId = c.Int(),
                    })
                .PrimaryKey(t => t.StudentId)
                .ForeignKey("dbo.Parents", t => t.ParentId, cascadeDelete: true)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.ParentId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Parents",
                c => new
                    {
                        ParentId = c.Int(nullable: false, identity: true),
                        UserId = c.Int(),
                        Name = c.String(nullable: false, maxLength: 200),
                        Contact = c.String(nullable: false, maxLength: 200),
                        CellPhone = c.String(maxLength: 20),
                        WorkPhone = c.String(maxLength: 20),
                        HomePhone = c.String(maxLength: 20),
                        PhysicalAddress = c.String(maxLength: 300),
                        PostalAddress = c.String(maxLength: 300),
                        Relationship = c.String(maxLength: 100),
                        Occupation = c.String(maxLength: 200),
                        Employer = c.String(maxLength: 200),
                        EmergencyContactName = c.String(maxLength: 200),
                        EmergencyContactPhone = c.String(maxLength: 20),
                    })
                .PrimaryKey(t => t.ParentId)
                .ForeignKey("dbo.AppUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AppUsers",
                c => new
                    {
                        UserId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 200),
                        Email = c.String(nullable: false, maxLength: 200),
                        PasswordHash = c.String(nullable: false),
                        Role = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.UserId);
            
            CreateTable(
                "dbo.StudentSubjects",
                c => new
                    {
                        StudentSubjectId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        IsElective = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.StudentSubjectId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.Subjects",
                c => new
                    {
                        SubjectId = c.Int(nullable: false, identity: true),
                        SubjectName = c.String(nullable: false, maxLength: 100),
                        SubjectCode = c.String(maxLength: 10),
                        Category = c.String(maxLength: 100),
                        IsCompulsory = c.Boolean(nullable: false),
                        ApplicableGrades = c.String(),
                        Credits = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.SubjectId);
            
            CreateTable(
                "dbo.Invoices",
                c => new
                    {
                        InvoiceId = c.Int(nullable: false, identity: true),
                        InvoiceNumber = c.String(nullable: false),
                        RegistrationId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        ParentId = c.Int(nullable: false),
                        InvoiceType = c.String(nullable: false),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Description = c.String(),
                        CreatedDate = c.DateTime(nullable: false),
                        DueDate = c.DateTime(nullable: false),
                        Status = c.String(),
                    })
                .PrimaryKey(t => t.InvoiceId)
                .ForeignKey("dbo.Parents", t => t.ParentId)
                .ForeignKey("dbo.Registrations", t => t.RegistrationId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.RegistrationId)
                .Index(t => t.StudentId)
                .Index(t => t.ParentId);
            
            CreateTable(
                "dbo.Payments",
                c => new
                    {
                        PaymentId = c.Int(nullable: false, identity: true),
                        InvoiceId = c.Int(nullable: false),
                        AmountPaid = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PaymentDate = c.DateTime(nullable: false),
                        StripeChargeId = c.String(),
                        StripePaymentIntentId = c.String(),
                        Status = c.String(),
                        PaymentReference = c.String(),
                        ProofEmailSent = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.PaymentId)
                .ForeignKey("dbo.Invoices", t => t.InvoiceId)
                .Index(t => t.InvoiceId);
            
            CreateTable(
                "dbo.Registrations",
                c => new
                    {
                        RegistrationId = c.Int(nullable: false, identity: true),
                        AppId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        GradeEnrolling = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        CompletedAt = c.DateTime(),
                        Notes = c.String(),
                    })
                .PrimaryKey(t => t.RegistrationId)
                .ForeignKey("dbo.Applications", t => t.AppId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.AppId)
                .Index(t => t.StudentId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Invoices", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Invoices", "RegistrationId", "dbo.Registrations");
            DropForeignKey("dbo.Registrations", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Registrations", "AppId", "dbo.Applications");
            DropForeignKey("dbo.Payments", "InvoiceId", "dbo.Invoices");
            DropForeignKey("dbo.Invoices", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.AdminReviews", "AppId", "dbo.Applications");
            DropForeignKey("dbo.Applications", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Applications", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Documents", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Students", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.StudentSubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentSubjects", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Students", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.Parents", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.Documents", "AppId", "dbo.Applications");
            DropIndex("dbo.Registrations", new[] { "StudentId" });
            DropIndex("dbo.Registrations", new[] { "AppId" });
            DropIndex("dbo.Payments", new[] { "InvoiceId" });
            DropIndex("dbo.Invoices", new[] { "ParentId" });
            DropIndex("dbo.Invoices", new[] { "StudentId" });
            DropIndex("dbo.Invoices", new[] { "RegistrationId" });
            DropIndex("dbo.StudentSubjects", new[] { "SubjectId" });
            DropIndex("dbo.StudentSubjects", new[] { "StudentId" });
            DropIndex("dbo.Parents", new[] { "UserId" });
            DropIndex("dbo.Students", new[] { "UserId" });
            DropIndex("dbo.Students", new[] { "ParentId" });
            DropIndex("dbo.Documents", new[] { "AppId" });
            DropIndex("dbo.Documents", new[] { "StudentId" });
            DropIndex("dbo.Applications", new[] { "StudentId" });
            DropIndex("dbo.Applications", new[] { "ParentId" });
            DropIndex("dbo.AdminReviews", new[] { "AppId" });
            DropTable("dbo.Registrations");
            DropTable("dbo.Payments");
            DropTable("dbo.Invoices");
            DropTable("dbo.Subjects");
            DropTable("dbo.StudentSubjects");
            DropTable("dbo.AppUsers");
            DropTable("dbo.Parents");
            DropTable("dbo.Students");
            DropTable("dbo.Documents");
            DropTable("dbo.Applications");
            DropTable("dbo.AdminReviews");
        }
    }
}
