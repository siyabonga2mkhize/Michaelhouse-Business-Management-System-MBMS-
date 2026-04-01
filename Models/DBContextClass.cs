using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;


namespace Michaelhouse.Models
{
    public class DBContextClass : DbContext 
    {
        public DBContextClass() : base("MichaelHouse")
        {
            Database.CommandTimeout = 60;
        }

        public DbSet<Parent> Parents { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AdminReview> AdminReviews { get; set; }
        public DbSet<AppUser> Users { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Application → Student (no cascade to avoid multiple cascade paths)
            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Student)
                .WithMany(s => s.Applications)
                .HasForeignKey(a => a.StudentId)
                .WillCascadeOnDelete(false);

            // Application → Parent (no cascade)
            modelBuilder.Entity<Application>()
                .HasRequired(a => a.Parent)
                .WithMany()
                .HasForeignKey(a => a.ParentId)
                .WillCascadeOnDelete(false);

            // Document → Application (cascade delete documents with application)
            modelBuilder.Entity<Document>()
                .HasRequired(d => d.Application)
                .WithMany(a => a.Documents)
                .HasForeignKey(d => d.AppId)
                .WillCascadeOnDelete(true);

            // Document → Student (no cascade)
            modelBuilder.Entity<Document>()
                .HasRequired(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .WillCascadeOnDelete(false);

            // Parent → AppUser (optional — UserId = 0 in dev mode)
            modelBuilder.Entity<Parent>()
                .HasOptional(p => p.User) //Remove Id if there's an err
                .WithMany()
                .HasForeignKey(p => p.UserId);
        }
    }
}
