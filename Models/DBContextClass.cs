using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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
        public DbSet<StreamEnrolment> StreamEnrolments { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Student)
                .WithMany(s => s.Applications)
                .HasForeignKey(a => a.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Parent)
                .WithMany()
                .HasForeignKey(a => a.ParentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Document>()
                .HasRequired(d => d.Application)
                .WithMany(a => a.Documents)
                .HasForeignKey(d => d.AppId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Document>()
                .HasRequired(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Parent>()
                .HasOptional(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<Student>()
                .HasOptional(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId);

            modelBuilder.Entity<Registration>()
                .HasRequired(r => r.Application)
                .WithMany()
                .HasForeignKey(r => r.AppId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Registration>()
                .HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentSubject>()
                .HasRequired(ss => ss.Student)
                .WithMany(s => s.StudentSubjects)
                .HasForeignKey(ss => ss.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentSubject>()
                .HasRequired(ss => ss.Subject)
                .WithMany(s => s.StudentSubjects)
                .HasForeignKey(ss => ss.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StreamEnrolment>()
                .HasRequired(se => se.Student)
                .WithMany()
                .HasForeignKey(se => se.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StreamEnrolment>()
                .HasRequired(se => se.Registration)
                .WithMany()
                .HasForeignKey(se => se.RegistrationId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Invoice>()
                .HasRequired(i => i.Registration)
                .WithMany()
                .HasForeignKey(i => i.RegistrationId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Invoice>()
                .HasRequired(i => i.Student)
                .WithMany()
                .HasForeignKey(i => i.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Invoice>()
                .HasRequired(i => i.Parent)
                .WithMany()
                .HasForeignKey(i => i.ParentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Payment>()
                .HasRequired(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .WillCascadeOnDelete(false);
        }
    }
}
