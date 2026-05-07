namespace Michaelhouse.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate1 : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
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
            CreateIndex("dbo.Subjects", "TeacherId");
            CreateIndex("dbo.StudentMarks", "Subject_SubjectId");
            CreateIndex("dbo.TimetableSlots", "Subject_SubjectId");
            AddForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers", "TeacherId");
            AddForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects", "SubjectId");
            AddForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects", "SubjectId");
            AddForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers", "TeacherId", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Attendances", "SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.Attendances", "StudentId", "dbo.Students");
            DropForeignKey("dbo.TimetableSlots", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.StudentMarks", "Subject_SubjectId", "dbo.Subjects");
            DropForeignKey("dbo.TeacherAttendances", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.Subjects", "TeacherId", "dbo.Teachers");
            DropIndex("dbo.Attendances", new[] { "SubjectId" });
            DropIndex("dbo.Attendances", new[] { "StudentId" });
            DropIndex("dbo.TimetableSlots", new[] { "Subject_SubjectId" });
            DropIndex("dbo.TeacherAttendances", new[] { "TeacherId" });
            DropIndex("dbo.StudentMarks", new[] { "Subject_SubjectId" });
            DropIndex("dbo.Subjects", new[] { "TeacherId" });
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
            DropTable("dbo.Attendances");
            DropTable("dbo.TeacherAttendances");
            AddForeignKey("dbo.TimetableSlots", "TeacherId", "dbo.Teachers", "TeacherId");
        }
    }
}
