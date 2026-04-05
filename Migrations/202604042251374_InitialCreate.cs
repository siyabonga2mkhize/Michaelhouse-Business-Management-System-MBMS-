namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Students", "ClassId", "dbo.SchoolClasses");
            DropForeignKey("dbo.ClassSubjects", "ClassId", "dbo.SchoolClasses");
            DropForeignKey("dbo.ClassSubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Enrollments", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Enrollments", "SubjectId", "dbo.Subjects");
            DropIndex("dbo.Students", new[] { "ClassId" });
            DropIndex("dbo.ClassSubjects", new[] { "ClassId" });
            DropIndex("dbo.ClassSubjects", new[] { "SubjectId" });
            DropIndex("dbo.Enrollments", new[] { "StudentId" });
            DropIndex("dbo.Enrollments", new[] { "SubjectId" });
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
            
            AddColumn("dbo.Applications", "GradeApplying", c => c.Int(nullable: false));
            AddColumn("dbo.Applications", "AdditionalNotes", c => c.String());
            AddColumn("dbo.Students", "DOB", c => c.DateTime(nullable: false));
            AddColumn("dbo.Students", "HomeLanguage", c => c.String(maxLength: 50));
            AddColumn("dbo.Students", "IdNumber", c => c.String(maxLength: 20));
            AddColumn("dbo.Students", "PreviousSchool", c => c.String(maxLength: 200));
            AddColumn("dbo.Students", "CurrentGrade", c => c.String(maxLength: 50));
            AddColumn("dbo.Students", "MedicalConditions", c => c.String(maxLength: 500));
            AddColumn("dbo.Students", "UserId", c => c.Int());
            AddColumn("dbo.Students", "StudentNumber", c => c.String(nullable: false));
            AddColumn("dbo.Subjects", "Category", c => c.String(maxLength: 100));
            AddColumn("dbo.Subjects", "IsCompulsory", c => c.Boolean(nullable: false));
            AddColumn("dbo.Subjects", "ApplicableGrades", c => c.String());
            AddColumn("dbo.Teachers", "UserId", c => c.Int());
            AlterColumn("dbo.Students", "FirstName", c => c.String(nullable: false, maxLength: 200));
            AlterColumn("dbo.Students", "LastName", c => c.String(nullable: false, maxLength: 200));
            AlterColumn("dbo.Students", "Gender", c => c.String(nullable: false));
            CreateIndex("dbo.Students", "UserId");
            CreateIndex("dbo.Teachers", "UserId");
            AddForeignKey("dbo.Teachers", "UserId", "dbo.AppUsers", "UserId");
            AddForeignKey("dbo.Students", "UserId", "dbo.AppUsers", "UserId");
            DropColumn("dbo.Students", "DateOfBirth");
            DropTable("dbo.SchoolClasses");
            DropTable("dbo.ClassSubjects");
            DropTable("dbo.Enrollments");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.Enrollments",
                c => new
                    {
                        EnrollmentId = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        EnrollmentDate = c.DateTime(nullable: false),
                        AcademicYear = c.String(),
                    })
                .PrimaryKey(t => t.EnrollmentId);
            
            CreateTable(
                "dbo.ClassSubjects",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ClassId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        TeacherName = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.SchoolClasses",
                c => new
                    {
                        ClassId = c.Int(nullable: false, identity: true),
                        ClassName = c.String(),
                        GradeLevel = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ClassId);
            
            AddColumn("dbo.Students", "DateOfBirth", c => c.DateTime(nullable: false));
            DropForeignKey("dbo.Registrations", "StudentId", "dbo.Students");
            DropForeignKey("dbo.Registrations", "AppId", "dbo.Applications");
            DropForeignKey("dbo.Students", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.StudentSubjects", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Teachers", "UserId", "dbo.AppUsers");
            DropForeignKey("dbo.StudentSubjects", "StudentId", "dbo.Students");
            DropIndex("dbo.Registrations", new[] { "StudentId" });
            DropIndex("dbo.Registrations", new[] { "AppId" });
            DropIndex("dbo.Teachers", new[] { "UserId" });
            DropIndex("dbo.StudentSubjects", new[] { "SubjectId" });
            DropIndex("dbo.StudentSubjects", new[] { "StudentId" });
            DropIndex("dbo.Students", new[] { "UserId" });
            AlterColumn("dbo.Students", "Gender", c => c.String(nullable: false, maxLength: 10));
            AlterColumn("dbo.Students", "LastName", c => c.String(nullable: false, maxLength: 50));
            AlterColumn("dbo.Students", "FirstName", c => c.String(nullable: false, maxLength: 50));
            DropColumn("dbo.Teachers", "UserId");
            DropColumn("dbo.Subjects", "ApplicableGrades");
            DropColumn("dbo.Subjects", "IsCompulsory");
            DropColumn("dbo.Subjects", "Category");
            DropColumn("dbo.Students", "StudentNumber");
            DropColumn("dbo.Students", "UserId");
            DropColumn("dbo.Students", "MedicalConditions");
            DropColumn("dbo.Students", "CurrentGrade");
            DropColumn("dbo.Students", "PreviousSchool");
            DropColumn("dbo.Students", "IdNumber");
            DropColumn("dbo.Students", "HomeLanguage");
            DropColumn("dbo.Students", "DOB");
            DropColumn("dbo.Applications", "AdditionalNotes");
            DropColumn("dbo.Applications", "GradeApplying");
            DropTable("dbo.Registrations");
            DropTable("dbo.StudentSubjects");
            CreateIndex("dbo.Enrollments", "SubjectId");
            CreateIndex("dbo.Enrollments", "StudentId");
            CreateIndex("dbo.ClassSubjects", "SubjectId");
            CreateIndex("dbo.ClassSubjects", "ClassId");
            CreateIndex("dbo.Students", "ClassId");
            AddForeignKey("dbo.Enrollments", "SubjectId", "dbo.Subjects", "SubjectId", cascadeDelete: true);
            AddForeignKey("dbo.Enrollments", "StudentId", "dbo.Students", "StudentId", cascadeDelete: true);
            AddForeignKey("dbo.ClassSubjects", "SubjectId", "dbo.Subjects", "SubjectId", cascadeDelete: true);
            AddForeignKey("dbo.ClassSubjects", "ClassId", "dbo.SchoolClasses", "ClassId", cascadeDelete: true);
            AddForeignKey("dbo.Students", "ClassId", "dbo.SchoolClasses", "ClassId");
        }
    }
}
