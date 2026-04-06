using System.Data.Entity;

namespace Michaelhouse.Models
{
    public class DBContextClass : DbContext
    {
        public DBContextClass() : base("name=MichaelHouse")
        {
            Database.CommandTimeout = 60;
        }

        public DbSet<AppUser> Users { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AdminReview> AdminReviews { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<StudentSubject> StudentSubjects { get; set; }

        
        //Store
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<StreamEnrolment> StreamEnrolments { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<TeacherSubjectGrade> TeacherSubjectGrades { get; set; }
        public DbSet<Period> Periods { get; set; }
        public DbSet<TimetableSlot> TimetableSlots { get; set; }
        public DbSet<Assessment> Assessments { get; set; }
        public DbSet<StudentMark> StudentMarks { get; set; }
        public DbSet<TermResult> TermResults { get; set; }
        public DbSet<YearResult> YearResults { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        { 
            base.OnModelCreating(modelBuilder);

            // ── Existing relationships ────────────────────────────────────────

            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Student).WithMany(s => s.Applications)
                .HasForeignKey(a => a.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Parent).WithMany()
                .HasForeignKey(a => a.ParentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Document>()
                .HasRequired(d => d.Application).WithMany(a => a.Documents)
                .HasForeignKey(d => d.AppId).WillCascadeOnDelete(true);

            modelBuilder.Entity<Document>()
                .HasRequired(d => d.Student).WithMany()
                .HasForeignKey(d => d.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Parent>()
                .HasOptional(p => p.User).WithMany()
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<Student>()
                .HasOptional(s => s.User).WithMany()
                .HasForeignKey(s => s.UserId);

            modelBuilder.Entity<Registration>()
                .HasRequired(r => r.Application).WithMany()
                .HasForeignKey(r => r.AppId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Registration>()
                .HasRequired(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentSubject>()
                .HasRequired(ss => ss.Student).WithMany(s => s.StudentSubjects)
                .HasForeignKey(ss => ss.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentSubject>()
                .HasRequired(ss => ss.Subject).WithMany(s => s.StudentSubjects)
                .HasForeignKey(ss => ss.SubjectId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StreamEnrolment>()
                .HasRequired(se => se.Student).WithMany()
                .HasForeignKey(se => se.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StreamEnrolment>()
                .HasRequired(se => se.Registration).WithMany()
                .HasForeignKey(se => se.RegistrationId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Invoice>()
                .HasRequired(i => i.Registration).WithMany()
                .HasForeignKey(i => i.RegistrationId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Invoice>()
                .HasRequired(i => i.Student).WithMany()
                .HasForeignKey(i => i.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Invoice>()
                .HasRequired(i => i.Parent).WithMany()
                .HasForeignKey(i => i.ParentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Payment>()
                .HasRequired(p => p.Invoice).WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId).WillCascadeOnDelete(false);

            // ── Teacher relationships ─────────────────────────────────────────

            modelBuilder.Entity<Teacher>()
                .HasOptional(t => t.User).WithMany()
                .HasForeignKey(t => t.UserId);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasRequired(tsg => tsg.Teacher)
                .WithMany(t => t.SubjectAssignments)
                .HasForeignKey(tsg => tsg.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasRequired(tsg => tsg.Subject).WithMany()
                .HasForeignKey(tsg => tsg.SubjectId)
                .WillCascadeOnDelete(false);

            // ── Timetable relationships ───────────────────────────────────────

            modelBuilder.Entity<TimetableSlot>()
                .HasRequired(ts => ts.Teacher)
                .WithMany(t => t.TimetableSlots)
                .HasForeignKey(ts => ts.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TimetableSlot>()
                .HasRequired(ts => ts.Subject).WithMany()
                .HasForeignKey(ts => ts.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TimetableSlot>()
                .HasRequired(ts => ts.Period).WithMany()
                .HasForeignKey(ts => ts.PeriodId)
                .WillCascadeOnDelete(false);


            // ========== NEW SCHOOL STORE CONFIGURATIONS ==========

            // Category Configuration
            modelBuilder.Entity<Category>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<Category>()
                .Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Category>()
                .Property(c => c.Description)
                .HasMaxLength(500);

            // Product Configuration
            modelBuilder.Entity<Product>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Product>()
                .Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<Product>()
                .Property(p => p.Description)
                .HasMaxLength(1000);

            //modelBuilder.Entity<Product>()
             //   .Property(p => p.Price)
              //  .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.ImageUrl)
                .HasMaxLength(500);

            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(false);

            // Order Configuration
            modelBuilder.Entity<Order>()
                .HasKey(o => o.Id);    

            modelBuilder.Entity<Order>()
                .Property(o => o.OrderNumber)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<Order>()
                .Property(o => o.CustomerEmail)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<Order>()
                .Property(o => o.CustomerName)
                .IsRequired()
                .HasMaxLength(100);

            //modelBuilder.Entity<Order>()
            //    .Property(o => o.TotalAmount)
            //    .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasMaxLength(50);

            modelBuilder.Entity<Order>()
                .Property(o => o.Notes)
                .HasMaxLength(500);

            // OrderItem Configuration
            modelBuilder.Entity<OrderItem>()
                .HasKey(oi => oi.Id);

            //modelBuilder.Entity<OrderItem>()
            //    .Property(oi => oi.UnitPrice)
            //    .HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
            modelBuilder.Entity<StreamEnrolment>()
                .HasRequired(se => se.Student)
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Assessment>()
                .HasRequired(a => a.Subject)
                .WithMany()
                .HasForeignKey(a => a.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentMark>()
                .HasRequired(sm => sm.Assessment)
                .WithMany(a => a.StudentMarks)
                .HasForeignKey(sm => sm.AssessmentmentId)
                .WillCascadeOnDelete(true);     // deleting an assessment removes its marks

            modelBuilder.Entity<StudentMark>()
                .HasRequired(sm => sm.Student)
                .WithMany()
                .HasForeignKey(sm => sm.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TermResult>()
                .HasRequired(tr => tr.Student)
                .WithMany()
                .HasForeignKey(tr => tr.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TermResult>()
                .HasRequired(tr => tr.Subject)
                .WithMany()
                .HasForeignKey(tr => tr.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<YearResult>()
                .HasRequired(yr => yr.Student)
                .WithMany()
                .HasForeignKey(yr => yr.StudentId)
            modelBuilder.Entity<Payment>()
                .HasRequired(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .WillCascadeOnDelete(false);
        }
    }
}