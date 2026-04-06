namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.StudentMarks", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students");
            DropIndex("dbo.StudentMarks", new[] { "SubjectId" });
            RenameColumn(table: "dbo.StudentMarks", name: "SubjectId", newName: "Subject_SubjectId");
            CreateTable(
                "dbo.Assessments",
                c => new
                    {
                        AssessmentId = c.Int(nullable: false, identity: true),
                        TeacherId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        Stream = c.Int(nullable: false),
                        Title = c.String(nullable: false, maxLength: 200),
                        AssessmentType = c.String(nullable: false, maxLength: 50),
                        Term = c.Int(nullable: false),
                        AcademicYear = c.Int(nullable: false),
                        ScheduledDate = c.DateTime(nullable: false),
                        TotalMarks = c.Decimal(nullable: false, precision: 18, scale: 2),
                        WeightingPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Notes = c.String(maxLength: 500),
                        MarksCaptureClosed = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.AssessmentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.TeacherSubjectGrades",
                c => new
                    {
                        TeacherSubjectGradeId = c.Int(nullable: false, identity: true),
                        TeacherId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        Stream = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.TeacherSubjectGradeId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.TimetableSlots",
                c => new
                    {
                        SlotId = c.Int(nullable: false, identity: true),
                        AcademicYear = c.Int(nullable: false),
                        DayOfWeek = c.Int(nullable: false),
                        PeriodId = c.Int(nullable: false),
                        TeacherId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        Stream = c.Int(nullable: false),
                        Subject_SubjectId = c.Int(),
                    })
                .PrimaryKey(t => t.SlotId)
                .ForeignKey("dbo.Periods", t => t.PeriodId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .ForeignKey("dbo.Teachers", t => t.TeacherId)
                .ForeignKey("dbo.Subjects", t => t.Subject_SubjectId)
                .Index(t => t.PeriodId)
                .Index(t => t.TeacherId)
                .Index(t => t.SubjectId)
                .Index(t => t.Subject_SubjectId);
            
            CreateTable(
                "dbo.Periods",
                c => new
                    {
                        PeriodId = c.Int(nullable: false, identity: true),
                        PeriodNumber = c.Int(nullable: false),
                        StartTime = c.Time(nullable: false, precision: 7),
                        EndTime = c.Time(nullable: false, precision: 7),
                        Label = c.String(maxLength: 50),
                        IsBreak = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.PeriodId);
            
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
                        Status = c.String(nullable: false),
                        PaymentReference = c.String(),
                        ProofEmailSent = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.PaymentId)
                .ForeignKey("dbo.Invoices", t => t.InvoiceId)
                .Index(t => t.InvoiceId);
            
            CreateTable(
                "dbo.StreamEnrolments",
                c => new
                    {
                        StreamEnrolmentId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        RegistrationId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        Stream = c.Int(nullable: false),
                        TakesMathematics = c.Boolean(nullable: false),
                        EnrolledAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.StreamEnrolmentId)
                .ForeignKey("dbo.Registrations", t => t.RegistrationId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId)
                .Index(t => t.RegistrationId);
            
            CreateTable(
                "dbo.TermResults",
                c => new
                    {
                        TermResultId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        Term = c.Int(nullable: false),
                        AcademicYear = c.Int(nullable: false),
                        TermMarkPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Symbol = c.String(maxLength: 2),
                        Passed = c.Boolean(nullable: false),
                        CalculatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TermResultId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .ForeignKey("dbo.Subjects", t => t.SubjectId)
                .Index(t => t.StudentId)
                .Index(t => t.SubjectId);
            
            CreateTable(
                "dbo.YearResults",
                c => new
                    {
                        YearResultId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        Grade = c.Int(nullable: false),
                        AcademicYear = c.Int(nullable: false),
                        OverallPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PromotionStatus = c.String(nullable: false, maxLength: 20),
                        AdminNotes = c.String(maxLength: 500),
                        CalculatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.YearResultId)
                .ForeignKey("dbo.Students", t => t.StudentId)
                .Index(t => t.StudentId);
            
            AddColumn("dbo.StudentSubjects", "Stream", c => c.Int(nullable: false));
            AddColumn("dbo.StudentSubjects", "IsCompulsory", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "Name", c => c.String(nullable: false, maxLength: 200));
            AddColumn("dbo.Subjects", "Code", c => c.String(maxLength: 50));
            AddColumn("dbo.Subjects", "Stream", c => c.Int(nullable: false));
            AddColumn("dbo.Subjects", "IsLanguage", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "IsMathsOption", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "RequiresMaths", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "SortOrder", c => c.Int(nullable: false));
            AddColumn("dbo.StudentMarks", "AssessmentmentId", c => c.Int(nullable: false));
            AddColumn("dbo.StudentMarks", "MarksObtained", c => c.Decimal(precision: 18, scale: 2));
            AddColumn("dbo.StudentMarks", "IsAbsent", c => c.Boolean(nullable: false));
            AddColumn("dbo.StudentMarks", "TeacherComment", c => c.String(maxLength: 300));
            AddColumn("dbo.StudentMarks", "CapturedAt", c => c.DateTime());
            AlterColumn("dbo.StudentMarks", "Subject_SubjectId", c => c.Int());
            AlterColumn("dbo.Teachers", "FirstName", c => c.String(nullable: false, maxLength: 100));
            AlterColumn("dbo.Teachers", "LastName", c => c.String(nullable: false, maxLength: 100));
            AlterColumn("dbo.Teachers", "Email", c => c.String(nullable: false, maxLength: 200));
            AlterColumn("dbo.Teachers", "Phone", c => c.String(maxLength: 20));
            CreateIndex("dbo.StudentMarks", "AssessmentmentId");
            CreateIndex("dbo.StudentMarks", "Subject_SubjectId");
            AddForeignKey("dbo.StudentMarks", "AssessmentmentId", "dbo.Assessments", "AssessmentId", cascadeDelete: true);
            AddForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects", "SubjectId");
            AddForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students", "StudentId");
            DropColumn("dbo.StudentSubjects", "IsElective");
            DropColumn("dbo.Subjects", "SubjectName");
            DropColumn("dbo.Subjects", "SubjectCode");
            DropColumn("dbo.Subjects", "Category");
            DropColumn("dbo.Subjects", "Credits");
            DropColumn("dbo.StudentMarks", "AssessmentName");
            DropColumn("dbo.StudentMarks", "AssessmentType");
            DropColumn("dbo.StudentMarks", "MarkObtained");
            DropColumn("dbo.StudentMarks", "TotalMarks");
            DropColumn("dbo.StudentMarks", "Weight");
            DropColumn("dbo.StudentMarks", "Term");
            DropColumn("dbo.StudentMarks", "Year");
            DropColumn("dbo.Teachers", "EmployeeNumber");
            DropColumn("dbo.Teachers", "Department");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Teachers", "Department", c => c.String());
            AddColumn("dbo.Teachers", "EmployeeNumber", c => c.String());
            AddColumn("dbo.StudentMarks", "Year", c => c.Int(nullable: false));
            AddColumn("dbo.StudentMarks", "Term", c => c.Int(nullable: false));
            AddColumn("dbo.StudentMarks", "Weight", c => c.Double(nullable: false));
            AddColumn("dbo.StudentMarks", "TotalMarks", c => c.Double(nullable: false));
            AddColumn("dbo.StudentMarks", "MarkObtained", c => c.Double(nullable: false));
            AddColumn("dbo.StudentMarks", "AssessmentType", c => c.String());
            AddColumn("dbo.StudentMarks", "AssessmentName", c => c.String());
            AddColumn("dbo.Subjects", "Credits", c => c.Int(nullable: false));
            AddColumn("dbo.Subjects", "Category", c => c.String(maxLength: 100));
            AddColumn("dbo.Subjects", "SubjectCode", c => c.String(maxLength: 10));
            AddColumn("dbo.Subjects", "SubjectName", c => c.String(nullable: false, maxLength: 100));
            AddColumn("dbo.StudentSubjects", "IsElective", c => c.Boolean(nullable: false));
            DropForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.YearResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.TermResults", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TermResults", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StreamEnrolments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.StreamEnrolments", "RegistrationId", "dbo.Registrations");
            DropForeignKey("dbo.Invoices", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Invoices", "RegistrationId", "dbo.Registrations");
            DropForeignKey("dbo.Payments", "InvoiceId", "dbo.Invoices");
            DropForeignKey("dbo.Invoices", "ParentId", "dbo.Parents");
            DropForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "AssessmentmentId", "dbo.Assessments");
            DropForeignKey("dbo.Assessments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TimetableSlots", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TimetableSlots", "PeriodId", "dbo.Periods");
            DropForeignKey("dbo.TeacherSubjectGrades", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.TeacherSubjectGrades", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Assessments", "SubjectId", "dbo.Subjects");
            DropIndex("dbo.YearResults", new[] { "StudentId" });
            DropIndex("dbo.TermResults", new[] { "SubjectId" });
            DropIndex("dbo.TermResults", new[] { "StudentId" });
            DropIndex("dbo.StreamEnrolments", new[] { "RegistrationId" });
            DropIndex("dbo.StreamEnrolments", new[] { "StudentId" });
            DropIndex("dbo.Payments", new[] { "InvoiceId" });
            DropIndex("dbo.Invoices", new[] { "ParentId" });
            DropIndex("dbo.Invoices", new[] { "StudentId" });
            DropIndex("dbo.Invoices", new[] { "RegistrationId" });
            DropIndex("dbo.TimetableSlots", new[] { "Subject_SubjectId" });
            DropIndex("dbo.TimetableSlots", new[] { "SubjectId" });
            DropIndex("dbo.TimetableSlots", new[] { "TeacherId" });
            DropIndex("dbo.TimetableSlots", new[] { "PeriodId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "SubjectId" });
            DropIndex("dbo.TeacherSubjectGrades", new[] { "TeacherId" });
            DropIndex("dbo.Assessments", new[] { "SubjectId" });
            DropIndex("dbo.Assessments", new[] { "TeacherId" });
            DropIndex("dbo.StudentMarks", new[] { "Subject_SubjectId" });
            DropIndex("dbo.StudentMarks", new[] { "AssessmentmentId" });
            AlterColumn("dbo.Teachers", "Phone", c => c.String());
            AlterColumn("dbo.Teachers", "Email", c => c.String(nullable: false));
            AlterColumn("dbo.Teachers", "LastName", c => c.String(nullable: false, maxLength: 50));
            AlterColumn("dbo.Teachers", "FirstName", c => c.String(nullable: false, maxLength: 50));
            AlterColumn("dbo.StudentMarks", "Subject_SubjectId", c => c.Int(nullable: false));
            DropColumn("dbo.StudentMarks", "CapturedAt");
            DropColumn("dbo.StudentMarks", "TeacherComment");
            DropColumn("dbo.StudentMarks", "IsAbsent");
            DropColumn("dbo.StudentMarks", "MarksObtained");
            DropColumn("dbo.StudentMarks", "AssessmentmentId");
            DropColumn("dbo.Subjects", "SortOrder");
            DropColumn("dbo.Subjects", "RequiresMaths");
            DropColumn("dbo.Subjects", "IsMathsOption");
            DropColumn("dbo.Subjects", "IsLanguage");
            DropColumn("dbo.Subjects", "Stream");
            DropColumn("dbo.Subjects", "Code");
            DropColumn("dbo.Subjects", "Name");
            DropColumn("dbo.StudentSubjects", "IsCompulsory");
            DropColumn("dbo.StudentSubjects", "Stream");
            DropTable("dbo.YearResults");
            DropTable("dbo.TermResults");
            DropTable("dbo.StreamEnrolments");
            DropTable("dbo.Payments");
            DropTable("dbo.Invoices");
            DropTable("dbo.Periods");
            DropTable("dbo.TimetableSlots");
            DropTable("dbo.TeacherSubjectGrades");
            DropTable("dbo.Assessments");
            RenameColumn(table: "dbo.StudentMarks", name: "Subject_SubjectId", newName: "SubjectId");
            CreateIndex("dbo.StudentMarks", "SubjectId");
            AddForeignKey("dbo.StudentMarks", "StudentId", "dbo.Students", "StudentId", cascadeDelete: true);
            AddForeignKey("dbo.StudentMarks", "SubjectId", "dbo.Subjects", "SubjectId", cascadeDelete: true);
        }
    }
}
