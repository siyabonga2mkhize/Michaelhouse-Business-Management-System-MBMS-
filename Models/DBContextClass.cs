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
            //this.Configuration.ProxyCreationEnabled = false;
            Database.CommandTimeout = 60;
        }

        public DbSet<Parent> Parents { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AdminReview> AdminReviews { get; set; }
        public DbSet<AppUser> Users { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<StudentSubject> StudentSubjects { get; set; }
        
        //Store
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

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
                .WillCascadeOnDelete(false);
        }
    }
}
