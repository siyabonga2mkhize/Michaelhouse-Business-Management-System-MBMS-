using Microsoft.EntityFrameworkCore;

namespace Michaelhouse.Models
{
    public class DBContextClass : DbContext
    {
        public DBContextClass(DbContextOptions<DBContextClass> options) : base(options)
        {
        }

        // ─── Core User Entities ──────────────────────────────────────────
        public DbSet<AppUser> Users { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        // ─── Application & Registration ──────────────────────────────────
        public DbSet<Application> Applications { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AdminReview> AdminReviews { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<StreamEnrolment> StreamEnrolments { get; set; }

        // ─── Academic Entities ────────────────────────────────────────────
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<StudentSubject> StudentSubjects { get; set; }
        public DbSet<TeacherSubjectGrade> TeacherSubjectGrades { get; set; }
        public DbSet<Assessment> Assessments { get; set; }
        public DbSet<StudentMark> StudentMarks { get; set; }
        public DbSet<TermResult> TermResults { get; set; }
        public DbSet<YearResult> YearResults { get; set; }
        public DbSet<ClassSubject> ClassSubjects { get; set; }
        public DbSet<SchoolClass> SchoolClasses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }

        // ─── Attendance & Timetable ───────────────────────────────────────
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<TeacherAttendance> TeacherAttendances { get; set; }
        public DbSet<Period> Periods { get; set; }
        public DbSet<TimetableSlot> TimetableSlots { get; set; }

        // ─── Finance ──────────────────────────────────────────────────────
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }

        // Store
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // ─── Driver ─────────────────────────────────────────────────────
        public DbSet<DriverApplication> DriverApplications { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<DriverAvailability> DriverAvailabilities { get; set; }
        public DbSet<DriverDocument> DriverDocuments { get; set; }

        // ─── Vehicle ───────────────────────────────────────────────────
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleIssue> VehicleIssues { get; set; }

        // ── Inventory & Suppliers ─────────────────────────────────────────────────
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierProduct> SupplierProducts { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        // ─── Trips Management ───────────────────────────────────────────────
        public DbSet<TripRequest> TripRequests { get; set; }
        public DbSet<TripSchedule> TripSchedules { get; set; }
        public DbSet<TripStudent> TripStudents { get; set; }
        public DbSet<TripVehicleAssignment> TripVehicleAssignments { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<StudentAttendanceToken> StudentAttendanceTokens { get; set; }
        public DbSet<StudentQRCode> StudentQRCodes { get; set; }

        // Boarding/residence allocation entities
        public DbSet<Residence> Residences { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Bed> Beds { get; set; }
        public DbSet<ResidenceAssignment> ResidenceAssignments { get; set; }
        public DbSet<DisciplinaryConflict> DisciplinaryConflicts { get; set; }
        public DbSet<StudentProfile> StudentProfiles { get; set; }
        public DbSet<RoomScoreAudit> RoomScoreAudits { get; set; }
        public DbSet<AIResidenceRecommendation> AIResidenceRecommendations { get; set; }
        public DbSet<AIAllocationHistory> AIAllocationHistories { get; set; }
        public DbSet<ResidenceMovement> ResidenceMovements { get; set; }
        public DbSet<AIWaitingList> AIWaitingLists { get; set; }
        public DbSet<HouseMaster> HouseMasters { get; set; }
        public DbSet<AIAlert> AIAlerts { get; set; }
        public DbSet<AIRecommendationOverride> AIRecommendationOverrides { get; set; }
        public DbSet<QRScanRecord> QRScanRecords { get; set; }
        public DbSet<ResidenceAllocation> ResidenceAllocations { get; set; }
        public DbSet<EmergencyAlert> EmergencyAlerts { get; set; }
        public DbSet<StudentSafetyConfirmation> StudentSafetyConfirmations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Existing relationships ───────────────────────────
            modelBuilder.Entity<Application>()
                .HasOne(a => a.Student).WithMany(s => s.Applications)
                .HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Application>()
                .HasOne(a => a.Parent).WithMany()
                .HasForeignKey(a => a.ParentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Application).WithMany(a => a.Documents)
                .HasForeignKey(d => d.AppId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Student).WithMany()
                .HasForeignKey(d => d.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Parent>()
                .HasOne(p => p.User).WithMany()
                .HasForeignKey(p => p.UserId).IsRequired(false);

            modelBuilder.Entity<Student>()
                .HasOne(s => s.User).WithMany()
                .HasForeignKey(s => s.UserId).IsRequired(false);

            modelBuilder.Entity<Registration>()
                .HasOne(r => r.Application).WithMany()
                .HasForeignKey(r => r.AppId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Registration>()
                .HasOne(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentSubject>()
                .HasOne(ss => ss.Student).WithMany(s => s.StudentSubjects)
                .HasForeignKey(ss => ss.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentSubject>()
                .HasOne(ss => ss.Subject).WithMany(s => s.StudentSubjects)
                .HasForeignKey(ss => ss.SubjectId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StreamEnrolment>()
                .HasOne(se => se.Student).WithMany()
                .HasForeignKey(se => se.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StreamEnrolment>()
                .HasOne(se => se.Registration).WithMany()
                .HasForeignKey(se => se.RegistrationId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Registration).WithMany()
                .HasForeignKey(i => i.RegistrationId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Student).WithMany()
                .HasForeignKey(i => i.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Parent).WithMany()
                .HasForeignKey(i => i.ParentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice).WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);

            // ── Teacher relationships ─────────────────────────────────────────
            modelBuilder.Entity<Teacher>()
                .HasOne(t => t.User).WithMany()
                .HasForeignKey(t => t.UserId).IsRequired(false);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasOne(tsg => tsg.Teacher).WithMany(t => t.SubjectAssignments)
                .HasForeignKey(tsg => tsg.TeacherId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasOne(tsg => tsg.Subject).WithMany()
                .HasForeignKey(tsg => tsg.SubjectId).OnDelete(DeleteBehavior.Restrict);

            // ── Timetable relationships ───────────────────────────────────────
            modelBuilder.Entity<TimetableSlot>()
                .HasOne(ts => ts.Subject).WithMany()
                .HasForeignKey(ts => ts.SubjectId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TimetableSlot>()
                .HasOne(ts => ts.Period).WithMany()
                .HasForeignKey(ts => ts.PeriodId).OnDelete(DeleteBehavior.Restrict);

            // ========== SCHOOL STORE CONFIGURATIONS ==========
            modelBuilder.Entity<Category>()
                .HasKey(c => c.Id);
            modelBuilder.Entity<Category>()
                .Property(c => c.Name).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Category>()
                .Property(c => c.Description).HasMaxLength(500);

            modelBuilder.Entity<Product>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<Product>()
                .Property(p => p.Name).IsRequired().HasMaxLength(200);
            modelBuilder.Entity<Product>()
                .Property(p => p.Description).HasMaxLength(1000);
            modelBuilder.Entity<Product>()
                .Property(p => p.ImageUrl).HasMaxLength(500);
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category).WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasKey(o => o.Id);
            modelBuilder.Entity<Order>()
                .Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
            modelBuilder.Entity<Order>()
                .Property(o => o.CustomerEmail).IsRequired().HasMaxLength(200);
            modelBuilder.Entity<Order>()
                .Property(o => o.CustomerName).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Order>()
                .Property(o => o.Status).HasMaxLength(50);
            modelBuilder.Entity<Order>()
                .Property(o => o.Notes).HasMaxLength(500);

            modelBuilder.Entity<OrderItem>()
                .HasKey(oi => oi.Id);
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order).WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId);

            // ── StreamEnrolment ─────────────────────────────────────────────────
            modelBuilder.Entity<StreamEnrolment>()
                .HasOne(se => se.Teacher).WithMany(t => t.StreamEnrolments)
                .HasForeignKey(se => se.TeacherId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // ── Assessment & Marks ───────────────────────────────────────────
            modelBuilder.Entity<Assessment>()
                .HasOne(a => a.Subject).WithMany()
                .HasForeignKey(a => a.SubjectId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentMark>()
                .HasOne(sm => sm.Assessment).WithMany(a => a.StudentMarks)
                .HasForeignKey(sm => sm.AssessmentmentId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentMark>()
                .HasOne(sm => sm.Student).WithMany()
                .HasForeignKey(sm => sm.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TermResult>()
                .HasOne(tr => tr.Student).WithMany()
                .HasForeignKey(tr => tr.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TermResult>()
                .HasOne(tr => tr.Subject).WithMany()
                .HasForeignKey(tr => tr.SubjectId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<YearResult>()
                .HasOne(yr => yr.Student).WithMany()
                .HasForeignKey(yr => yr.StudentId);

            // ========== TRIP MANAGEMENT RELATIONSHIPS ==========

            modelBuilder.Entity<TripRequest>()
                .HasOne(tr => tr.Teacher).WithMany()
                .HasForeignKey(tr => tr.TeacherId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripRequest>()
                .HasOne(tr => tr.ApprovedBy).WithMany()
                .HasForeignKey(tr => tr.ApprovedByAdminId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripSchedule>()
                .HasOne(ts => ts.TripRequest).WithMany()
                .HasForeignKey(ts => ts.TripRequestId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripSchedule>()
                .HasOne(ts => ts.Teacher).WithMany()
                .HasForeignKey(ts => ts.TeacherId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripSchedule>()
                .HasOne(ts => ts.Driver).WithMany()
                .HasForeignKey(ts => ts.DriverId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripSchedule>()
                .HasOne(ts => ts.Vehicle).WithMany()
                .HasForeignKey(ts => ts.VehicleId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripStudent>()
                .HasOne(ts => ts.TripSchedule).WithMany(t => t.TripStudents)
                .HasForeignKey(ts => ts.TripScheduleId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripStudent>()
                .HasOne(ts => ts.Student).WithMany()
                .HasForeignKey(ts => ts.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User).WithMany()
                .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasOne(tva => tva.TripSchedule).WithMany(ts => ts.VehicleAssignments)
                .HasForeignKey(tva => tva.TripScheduleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasOne(tva => tva.Driver).WithMany()
                .HasForeignKey(tva => tva.DriverId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasOne(tva => tva.Vehicle).WithMany()
                .HasForeignKey(tva => tva.VehicleId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripRequest>()
                .Property(t => t.Status).HasMaxLength(20);
            modelBuilder.Entity<TripRequest>()
                .Property(t => t.RejectionReason).HasMaxLength(500);
            modelBuilder.Entity<TripRequest>()
                .Property(t => t.Title).HasMaxLength(200);

            modelBuilder.Entity<TripSchedule>()
                .Property(t => t.Status).HasMaxLength(20);
            modelBuilder.Entity<TripSchedule>()
                .Property(t => t.Notes).HasMaxLength(500);

            // ── Suppliers & PurchaseOrders ────────────────────────────────────────
            modelBuilder.Entity<SupplierProduct>()
                .HasOne(sp => sp.Supplier).WithMany(s => s.SupplierProducts)
                .HasForeignKey(sp => sp.SupplierId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<SupplierProduct>()
                .HasOne(sp => sp.Product).WithMany()
                .HasForeignKey(sp => sp.ProductId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.Supplier).WithMany(s => s.PurchaseOrders)
                .HasForeignKey(po => po.SupplierId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PurchaseOrderLine>()
                .HasOne(pol => pol.PurchaseOrder).WithMany(po => po.LineItems)
                .HasForeignKey(pol => pol.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PurchaseOrderLine>()
                .HasOne(pol => pol.Product).WithMany()
                .HasForeignKey(pol => pol.ProductId).OnDelete(DeleteBehavior.Restrict);

            // ── Stock Movements ───────────────────────────────────────────────────
            modelBuilder.Entity<StockMovement>()
                .HasOne(sm => sm.Product).WithMany()
                .HasForeignKey(sm => sm.ProductId).OnDelete(DeleteBehavior.Restrict);

            // ======================================================================
            // BOARDING HOUSE MANAGEMENT RELATIONSHIPS
            // ======================================================================

            modelBuilder.Entity<Residence>()
                .HasOne(r => r.HouseMaster).WithMany()
                .HasForeignKey(r => r.HouseMasterId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Room>()
                .HasOne(r => r.Residence).WithMany(res => res.Rooms)
                .HasForeignKey(r => r.ResidenceId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Bed>()
                .HasOne(b => b.Room).WithMany(r => r.Beds)
                .HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasOne(ra => ra.Student).WithMany()
                .HasForeignKey(ra => ra.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasOne(ra => ra.Residence).WithMany()
                .HasForeignKey(ra => ra.ResidenceId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasOne(ra => ra.Room).WithMany()
                .HasForeignKey(ra => ra.RoomId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasOne(ra => ra.Bed).WithMany()
                .HasForeignKey(ra => ra.BedId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DisciplinaryConflict>()
                .HasOne<Student>().WithMany()
                .HasForeignKey(dc => dc.StudentAId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DisciplinaryConflict>()
                .HasOne<Student>().WithMany()
                .HasForeignKey(dc => dc.StudentBId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentProfile>()
                .HasOne(sp => sp.Student).WithMany()
                .HasForeignKey(sp => sp.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RoomScoreAudit>()
                .HasOne<Room>().WithMany()
                .HasForeignKey(rsa => rsa.RoomId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RoomScoreAudit>()
                .HasOne<Student>().WithMany()
                .HasForeignKey(rsa => rsa.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AIResidenceRecommendation>()
                .HasOne(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AIAllocationHistory>()
                .HasOne(h => h.Student).WithMany()
                .HasForeignKey(h => h.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AIWaitingList>()
                .HasOne(w => w.Student).WithMany()
                .HasForeignKey(w => w.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AIRecommendationOverride>()
                .HasOne(o => o.Recommendation).WithMany()
                .HasForeignKey(o => o.RecommendationId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AIAlert>()
                .HasOne(a => a.Residence).WithMany()
                .HasForeignKey(a => a.ResidenceId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOne(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOne(r => r.FromResidence).WithMany()
                .HasForeignKey(r => r.FromResidenceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOne(r => r.ToResidence).WithMany()
                .HasForeignKey(r => r.ToResidenceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOne(r => r.FromRoom).WithMany()
                .HasForeignKey(r => r.FromRoomId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOne(r => r.ToRoom).WithMany()
                .HasForeignKey(r => r.ToRoomId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmergencyAlert>()
                .HasKey(e => e.AlertId);

            modelBuilder.Entity<EmergencyAlert>()
                .HasMany(e => e.StudentConfirmations).WithOne(c => c.Alert)
                .HasForeignKey(c => c.AlertId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasKey(c => c.ConfirmationId);

            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasOne(c => c.Student).WithMany()
                .HasForeignKey(c => c.StudentId);

            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasOne(c => c.Alert).WithMany(a => a.StudentConfirmations)
                .HasForeignKey(c => c.AlertId);
        }
    }
}
