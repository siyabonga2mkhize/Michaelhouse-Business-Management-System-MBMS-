using Michaelhouse.Controllers;
using System.Configuration;
using System.Data.Common;
using System.Data.Entity;

namespace Michaelhouse.Models
{
    public class DBContextClass : DbContext
    {
        public DBContextClass() : base(GetDefaultConnectionString())
        {
            Database.CommandTimeout = 60;
        }

        public DBContextClass(DbConnection existingConnection, bool contextOwnsConnection)
            : base(existingConnection, contextOwnsConnection)
        {
            Database.CommandTimeout = 60;
        }

        private static string GetDefaultConnectionString()
        {
            var configured = ConfigurationManager.ConnectionStrings["MichaelHouse"];
            if (configured != null && !string.IsNullOrWhiteSpace(configured.ConnectionString))
                return configured.ConnectionString;

            return @"Data Source=AZASMACBOOK\SQLEXPRESS;Initial Catalog=MichaelHouse;Integrated Security=True;Connection Timeout=120;MultipleActiveResultSets=True;";
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

        // ─── Attendance & Timetable ───────────────────────────────────────
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<TeacherAttendance> TeacherAttendances { get; set; }
        public DbSet<Period> Periods { get; set; }
        public DbSet<TimetableSlot> TimetableSlots { get; set; }

        // ─── Finance ──────────────────────────────────────────────────────
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }

        // ─── Store ────────────────────────────────────────────────────────
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // ─── Driver ───────────────────────────────────────────────────────
        public DbSet<DriverApplication> DriverApplications { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<DriverAvailability> DriverAvailabilities { get; set; }
        public DbSet<DriverDocument> DriverDocuments { get; set; }

        // ─── Vehicle ──────────────────────────────────────────────────────
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleIssue> VehicleIssues { get; set; }

        // ─── Inventory & Suppliers (Store) ────────────────────────────────
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierProduct> SupplierProducts { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }

        // ─── Trips Management ─────────────────────────────────────────────
        public DbSet<TripRequest> TripRequests { get; set; }
        public DbSet<TripSchedule> TripSchedules { get; set; }
        public DbSet<TripStudent> TripStudents { get; set; }
        public DbSet<TripVehicleAssignment> TripVehicleAssignments { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<StudentAttendanceToken> StudentAttendanceTokens { get; set; }
        public DbSet<StudentQRCode> StudentQRCodes { get; set; }

        // ─── Boarding / Residence ─────────────────────────────────────────
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
        public DbSet<LeaveRequest> LeaveRequests { get; set; }

        // ─── Visitor Access ───────────────────────────────────────────────
        public DbSet<VisitorAccessRequest> VisitorAccessRequests { get; set; }
        public DbSet<TermCalendar> TermCalendars { get; set; }
        public DbSet<VisitorScanLog> VisitorScanLogs { get; set; }

        // ─── Maintenance ──────────────────────────────────────────────────
        public DbSet<MaintenanceStaff> MaintenanceStaff { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<JobCard> JobCards { get; set; }
        public DbSet<JobCardPhoto> JobCardPhotos { get; set; }
        public DbSet<MaintenanceInventory> MaintenanceInventory { get; set; }
        public DbSet<JobCardPart> JobCardParts { get; set; }
        public DbSet<PreventiveSchedule> PreventiveSchedules { get; set; }
        public DbSet<ShiftPattern> ShiftPatterns { get; set; }
        public DbSet<StaffShift> StaffShifts { get; set; }

        // ══════════════════════════════════════════════════════════════════
        // CAFETERIA & SPORTS
        // ══════════════════════════════════════════════════════════════════
        public DbSet<Sport> Sports { get; set; }
        public DbSet<Coach> Coaches { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamMembership> TeamMemberships { get; set; }
        public DbSet<CoachRecommendation> CoachRecommendations { get; set; }
        public DbSet<SportsActivity> SportsActivities { get; set; }
        public DbSet<CoachMessage> CoachMessages { get; set; }
        public DbSet<CoachRequirement> CoachRequirements { get; set; }

        public DbSet<DiningHall> DiningHalls { get; set; }
        public DbSet<MealSlot> MealSlots { get; set; }
        public DbSet<Allergen> Allergens { get; set; }
        public DbSet<DietaryCategory> DietaryCategories { get; set; }

        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuItemAllergen> MenuItemAllergens { get; set; }
        public DbSet<MenuItemDietaryTag> MenuItemDietaryTags { get; set; }

        public DbSet<WeeklyMenu> WeeklyMenus { get; set; }
        public DbSet<MenuDay> MenuDays { get; set; }
        public DbSet<MenuMeal> MenuMeals { get; set; }
        public DbSet<MenuMealComponent> MenuMealComponents { get; set; }
        public DbSet<MenuMealAlternative> MenuMealAlternatives { get; set; }
        public DbSet<MenuValidationIssue> MenuValidationIssues { get; set; }

        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeIngredient> RecipeIngredients { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<StockOrder> StockOrders { get; set; }
        public DbSet<StockOrderLine> StockOrderLines { get; set; }

        public DbSet<StudentDietaryRecord> StudentDietaryRecords { get; set; }
        public DbSet<DietaryChangeRequest> DietaryChangeRequests { get; set; }

        public DbSet<MealPlan> MealPlans { get; set; }
        public DbSet<MealPlanItem> MealPlanItems { get; set; }
        public DbSet<MealSelection> MealSelections { get; set; }
        public DbSet<MealService> MealServices { get; set; }
        public DbSet<ProductionRecord> ProductionRecords { get; set; }
        public DbSet<WasteRecord> WasteRecords { get; set; }

        public DbSet<CafeteriaAuditLog> CafeteriaAuditLogs { get; set; }
        public DbSet<CafeteriaSetting> CafeteriaSettings { get; set; }
        public DbSet<SportsMealRecommendationRule> SportsMealRecommendationRules { get; set; }

        // ─── Legacy (still used by old CafeteriaController until Phase 3) ─
        public DbSet<Selection> Selections { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<RSVP> RSVPs { get; set; }

        // ══════════════════════════════════════════════════════════════════
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ─── Core / Registration ─────────────────────────────────────
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

            // ─── Teacher ────────────────────────────────────────────────
            modelBuilder.Entity<Teacher>()
                .HasOptional(t => t.User).WithMany()
                .HasForeignKey(t => t.UserId);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasRequired(tsg => tsg.Teacher).WithMany(t => t.SubjectAssignments)
                .HasForeignKey(tsg => tsg.TeacherId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasRequired(tsg => tsg.Subject).WithMany()
                .HasForeignKey(tsg => tsg.SubjectId).WillCascadeOnDelete(false);

            // ─── Timetable ──────────────────────────────────────────────
            modelBuilder.Entity<TimetableSlot>()
                .HasRequired(ts => ts.Subject).WithMany()
                .HasForeignKey(ts => ts.SubjectId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TimetableSlot>()
                .HasRequired(ts => ts.Period).WithMany()
                .HasForeignKey(ts => ts.PeriodId).WillCascadeOnDelete(false);

            // ─── Store ──────────────────────────────────────────────────
            modelBuilder.Entity<Category>().HasKey(c => c.Id);
            modelBuilder.Entity<Category>().Property(c => c.Name).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Category>().Property(c => c.Description).HasMaxLength(500);

            modelBuilder.Entity<Product>().HasKey(p => p.Id);
            modelBuilder.Entity<Product>().Property(p => p.Name).IsRequired().HasMaxLength(200);
            modelBuilder.Entity<Product>().Property(p => p.Description).HasMaxLength(1000);
            modelBuilder.Entity<Product>().Property(p => p.ImageUrl).HasMaxLength(500);
            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category).WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Order>().HasKey(o => o.Id);
            modelBuilder.Entity<Order>().Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
            modelBuilder.Entity<Order>().Property(o => o.CustomerEmail).IsRequired().HasMaxLength(200);
            modelBuilder.Entity<Order>().Property(o => o.CustomerName).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Order>().Property(o => o.Status).HasMaxLength(50);
            modelBuilder.Entity<Order>().Property(o => o.Notes).HasMaxLength(500);

            modelBuilder.Entity<OrderItem>().HasKey(oi => oi.Id);
            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Order).WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId).WillCascadeOnDelete(true);
            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId);

            modelBuilder.Entity<StreamEnrolment>()
                .HasOptional(se => se.Teacher).WithMany(t => t.StreamEnrolments)
                .HasForeignKey(se => se.TeacherId).WillCascadeOnDelete(false);

            // ─── Assessment & Marks ─────────────────────────────────────
            modelBuilder.Entity<Assessment>()
                .HasRequired(a => a.Subject).WithMany()
                .HasForeignKey(a => a.SubjectId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentMark>()
                .HasRequired(sm => sm.Assessment).WithMany(a => a.StudentMarks)
                .HasForeignKey(sm => sm.AssessmentmentId).WillCascadeOnDelete(true);

            modelBuilder.Entity<StudentMark>()
                .HasRequired(sm => sm.Student).WithMany()
                .HasForeignKey(sm => sm.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TermResult>()
                .HasRequired(tr => tr.Student).WithMany()
                .HasForeignKey(tr => tr.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TermResult>()
                .HasRequired(tr => tr.Subject).WithMany()
                .HasForeignKey(tr => tr.SubjectId).WillCascadeOnDelete(false);

            modelBuilder.Entity<YearResult>()
                .HasRequired(yr => yr.Student).WithMany()
                .HasForeignKey(yr => yr.StudentId);

            // ─── Trips ──────────────────────────────────────────────────
            modelBuilder.Entity<TripRequest>()
                .HasRequired(tr => tr.Teacher).WithMany()
                .HasForeignKey(tr => tr.TeacherId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripRequest>()
                .HasOptional(tr => tr.ApprovedBy).WithMany()
                .HasForeignKey(tr => tr.ApprovedByAdminId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .HasRequired(ts => ts.TripRequest).WithMany()
                .HasForeignKey(ts => ts.TripRequestId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .HasRequired(ts => ts.Teacher).WithMany()
                .HasForeignKey(ts => ts.TeacherId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .HasOptional(ts => ts.Driver).WithMany()
                .HasForeignKey(ts => ts.DriverId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .HasOptional(ts => ts.Vehicle).WithMany()
                .HasForeignKey(ts => ts.VehicleId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripStudent>()
                .HasRequired(ts => ts.TripSchedule).WithMany(t => t.TripStudents)
                .HasForeignKey(ts => ts.TripScheduleId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripStudent>()
                .HasRequired(ts => ts.Student).WithMany()
                .HasForeignKey(ts => ts.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Notification>()
                .HasRequired(n => n.User).WithMany()
                .HasForeignKey(n => n.UserId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasRequired(tva => tva.TripSchedule).WithMany(ts => ts.VehicleAssignments)
                .HasForeignKey(tva => tva.TripScheduleId).WillCascadeOnDelete(true);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasRequired(tva => tva.Driver).WithMany()
                .HasForeignKey(tva => tva.DriverId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasRequired(tva => tva.Vehicle).WithMany()
                .HasForeignKey(tva => tva.VehicleId).WillCascadeOnDelete(false);

            // ─── Suppliers / Purchase Orders / Stock Movements ─────────
            modelBuilder.Entity<SupplierProduct>()
                .HasRequired(sp => sp.Supplier).WithMany(s => s.SupplierProducts)
                .HasForeignKey(sp => sp.SupplierId).WillCascadeOnDelete(false);

            modelBuilder.Entity<SupplierProduct>()
                .HasRequired(sp => sp.Product).WithMany()
                .HasForeignKey(sp => sp.ProductId).WillCascadeOnDelete(false);

            modelBuilder.Entity<PurchaseOrder>()
                .HasRequired(po => po.Supplier).WithMany(s => s.PurchaseOrders)
                .HasForeignKey(po => po.SupplierId).WillCascadeOnDelete(false);

            modelBuilder.Entity<PurchaseOrderLine>()
                .HasRequired(pol => pol.PurchaseOrder).WithMany(po => po.LineItems)
                .HasForeignKey(pol => pol.PurchaseOrderId).WillCascadeOnDelete(true);

            modelBuilder.Entity<PurchaseOrderLine>()
                .HasRequired(pol => pol.Product).WithMany()
                .HasForeignKey(pol => pol.ProductId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StockMovement>()
                .HasRequired(sm => sm.Product).WithMany()
                .HasForeignKey(sm => sm.ProductId).WillCascadeOnDelete(false);

            // ─── Boarding House ─────────────────────────────────────────
            modelBuilder.Entity<Residence>()
                .HasOptional(r => r.HouseMaster).WithMany()
                .HasForeignKey(r => r.HouseMasterId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Room>()
                .HasRequired(r => r.Residence).WithMany(r => r.Rooms)
                .HasForeignKey(r => r.ResidenceId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Bed>()
                .HasRequired(b => b.Room).WithMany(r => r.Beds)
                .HasForeignKey(b => b.RoomId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Bed>()
                .HasOptional(b => b.OccupiedByStudent).WithMany()
                .HasForeignKey(b => b.OccupiedByStudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Residence).WithMany()
                .HasForeignKey(r => r.ResidenceId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Room).WithMany()
                .HasForeignKey(r => r.RoomId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Bed).WithMany()
                .HasForeignKey(r => r.BedId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Residence).WithMany()
                .HasForeignKey(r => r.ResidenceId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Room).WithMany()
                .HasForeignKey(r => r.RoomId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Bed).WithMany()
                .HasForeignKey(r => r.BedId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Student>()
                .HasOptional(s => s.StudentProfile).WithRequired(sp => sp.Student);

            modelBuilder.Entity<StudentQRCode>()
                .HasRequired(q => q.Student).WithMany()
                .HasForeignKey(q => q.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<QRScanRecord>()
                .HasRequired(q => q.Student).WithMany()
                .HasForeignKey(q => q.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<QRScanRecord>()
                .HasOptional(q => q.Residence).WithMany()
                .HasForeignKey(q => q.ResidenceId).WillCascadeOnDelete(false);

            modelBuilder.Entity<QRScanRecord>()
                .HasOptional(q => q.HouseMaster).WithMany()
                .HasForeignKey(q => q.HouseMasterId).WillCascadeOnDelete(false);

            modelBuilder.Entity<AIResidenceRecommendation>()
                .HasRequired(a => a.Student).WithMany()
                .HasForeignKey(a => a.StudentId).WillCascadeOnDelete(false);
            modelBuilder.Entity<AIResidenceRecommendation>()
                .HasRequired(a => a.Residence).WithMany()
                .HasForeignKey(a => a.ResidenceId).WillCascadeOnDelete(false);
            modelBuilder.Entity<AIResidenceRecommendation>()
                .HasRequired(a => a.Room).WithMany()
                .HasForeignKey(a => a.RoomId).WillCascadeOnDelete(false);

            modelBuilder.Entity<AIAllocationHistory>()
                .HasRequired(a => a.Student).WithMany()
                .HasForeignKey(a => a.StudentId).WillCascadeOnDelete(false);
            modelBuilder.Entity<AIAllocationHistory>()
                .HasRequired(a => a.Residence).WithMany()
                .HasForeignKey(a => a.ResidenceId).WillCascadeOnDelete(false);
            modelBuilder.Entity<AIAllocationHistory>()
                .HasRequired(a => a.Room).WithMany()
                .HasForeignKey(a => a.RoomId).WillCascadeOnDelete(false);

            modelBuilder.Entity<AIWaitingList>()
                .HasRequired(w => w.Student).WithMany()
                .HasForeignKey(w => w.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<AIRecommendationOverride>()
                .HasRequired(o => o.Recommendation).WithMany()
                .HasForeignKey(o => o.RecommendationId).WillCascadeOnDelete(false);

            modelBuilder.Entity<AIAlert>()
                .HasRequired(a => a.Residence).WithMany()
                .HasForeignKey(a => a.ResidenceId).WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceMovement>()
                .HasRequired(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).WillCascadeOnDelete(false);
            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.FromResidence).WithMany()
                .HasForeignKey(r => r.FromResidenceId).WillCascadeOnDelete(false);
            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.ToResidence).WithMany()
                .HasForeignKey(r => r.ToResidenceId).WillCascadeOnDelete(false);
            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.FromRoom).WithMany()
                .HasForeignKey(r => r.FromRoomId).WillCascadeOnDelete(false);
            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.ToRoom).WithMany()
                .HasForeignKey(r => r.ToRoomId).WillCascadeOnDelete(false);

            modelBuilder.Entity<EmergencyAlert>().HasKey(e => e.AlertId);
            modelBuilder.Entity<EmergencyAlert>()
                .HasMany(e => e.StudentConfirmations).WithRequired(c => c.Alert)
                .HasForeignKey(c => c.AlertId).WillCascadeOnDelete(true);

            modelBuilder.Entity<StudentSafetyConfirmation>().HasKey(c => c.ConfirmationId);
            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasRequired(c => c.Student).WithMany()
                .HasForeignKey(c => c.StudentId);
            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasRequired(c => c.Alert).WithMany(a => a.StudentConfirmations)
                .HasForeignKey(c => c.AlertId);

            modelBuilder.Entity<LeaveRequest>()
                .HasRequired(l => l.Student).WithMany()
                .HasForeignKey(l => l.StudentId).WillCascadeOnDelete(false);
            modelBuilder.Entity<LeaveRequest>()
                .HasRequired(l => l.Parent).WithMany()
                .HasForeignKey(l => l.ParentId).WillCascadeOnDelete(false);
            modelBuilder.Entity<LeaveRequest>()
                .HasOptional(l => l.Residence).WithMany()
                .HasForeignKey(l => l.ResidenceId).WillCascadeOnDelete(false);
            modelBuilder.Entity<LeaveRequest>()
                .HasOptional(l => l.HouseMaster).WithMany()
                .HasForeignKey(l => l.HouseMasterId).WillCascadeOnDelete(false);

            // ══════════════════════════════════════════════════════════════
            // CAFETERIA / SPORTS
            // ══════════════════════════════════════════════════════════════

            // ─── Team / Coach / Sport ────────────────────────────────
            modelBuilder.Entity<Team>()
                .HasOptional(t => t.Coach).WithMany(c => c.Teams)
                .HasForeignKey(t => t.CoachID).WillCascadeOnDelete(false);

            modelBuilder.Entity<Team>()
                .HasOptional(t => t.Sport).WithMany(s => s.Teams)
                .HasForeignKey(t => t.SportId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Coach>()
                .HasOptional(c => c.Sport).WithMany()
                .HasForeignKey(c => c.SportId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TeamMembership>()
                .HasRequired(m => m.Team).WithMany(t => t.TeamMemberships)
                .HasForeignKey(m => m.TeamId).WillCascadeOnDelete(false);

            modelBuilder.Entity<TeamMembership>()
                .HasRequired(m => m.Student).WithMany()
                .HasForeignKey(m => m.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<SportsActivity>()
                .HasRequired(a => a.Team).WithMany(t => t.Activities)
                .HasForeignKey(a => a.TeamId).WillCascadeOnDelete(false);

            modelBuilder.Entity<SportsActivity>()
                .HasOptional(a => a.Coach).WithMany()
                .HasForeignKey(a => a.CoachId).WillCascadeOnDelete(false);

            modelBuilder.Entity<CoachMessage>()
                .HasRequired(m => m.Team).WithMany(t => t.Messages)
                .HasForeignKey(m => m.TeamId).WillCascadeOnDelete(false);

            modelBuilder.Entity<CoachMessage>()
                .HasOptional(m => m.Coach).WithMany()
                .HasForeignKey(m => m.CoachId).WillCascadeOnDelete(false);

            modelBuilder.Entity<CoachRequirement>()
                .HasRequired(r => r.Team).WithMany()
                .HasForeignKey(r => r.TeamId).WillCascadeOnDelete(false);

            modelBuilder.Entity<CoachRequirement>()
                .HasOptional(r => r.Coach).WithMany()
                .HasForeignKey(r => r.CoachId).WillCascadeOnDelete(false);

            modelBuilder.Entity<CoachRequirement>()
                .HasOptional(r => r.MealSlot).WithMany()
                .HasForeignKey(r => r.MealSlotId).WillCascadeOnDelete(false);

            modelBuilder.Entity<CoachRecommendation>()
                .HasRequired(c => c.Coach).WithMany()
                .HasForeignKey(c => c.CoachID).WillCascadeOnDelete(false);

            modelBuilder.Entity<CoachRecommendation>()
                .HasRequired(c => c.Team).WithMany()
                .HasForeignKey(c => c.TeamID).WillCascadeOnDelete(false);

            // ─── Menu ────────────────────────────────────────────────
            modelBuilder.Entity<MenuDay>()
                .HasRequired(d => d.WeeklyMenu).WithMany(w => w.Days)
                .HasForeignKey(d => d.WeeklyMenuId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuMeal>()
                .HasRequired(m => m.MenuDay).WithMany(d => d.Meals)
                .HasForeignKey(m => m.MenuDayId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuMeal>()
                .HasRequired(m => m.MealSlot).WithMany()
                .HasForeignKey(m => m.MealSlotId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuMealComponent>()
                .HasRequired(c => c.MenuMeal).WithMany(m => m.Components)
                .HasForeignKey(c => c.MenuMealId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuMealComponent>()
                .HasRequired(c => c.MenuItem).WithMany()
                .HasForeignKey(c => c.MenuItemId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuMealAlternative>()
                .HasRequired(a => a.MenuMeal).WithMany(m => m.Alternatives)
                .HasForeignKey(a => a.MenuMealId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuMealAlternative>()
                .HasRequired(a => a.MenuItem).WithMany()
                .HasForeignKey(a => a.MenuItemId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuItemAllergen>()
                .HasRequired(a => a.MenuItem).WithMany(m => m.Allergens)
                .HasForeignKey(a => a.MenuItemId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuItemAllergen>()
                .HasRequired(a => a.Allergen).WithMany()
                .HasForeignKey(a => a.AllergenId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuItemDietaryTag>()
                .HasRequired(t => t.MenuItem).WithMany(m => m.DietaryTags)
                .HasForeignKey(t => t.MenuItemId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuItemDietaryTag>()
                .HasRequired(t => t.DietaryCategory).WithMany()
                .HasForeignKey(t => t.DietaryCategoryId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuValidationIssue>()
                .HasRequired(v => v.WeeklyMenu).WithMany(w => w.ValidationIssues)
                .HasForeignKey(v => v.WeeklyMenuId).WillCascadeOnDelete(false);

            // ─── Recipe / Stock ──────────────────────────────────────
            modelBuilder.Entity<RecipeIngredient>()
                .HasRequired(ri => ri.Recipe).WithMany(r => r.Ingredients)
                .HasForeignKey(ri => ri.RecipeId).WillCascadeOnDelete(false);

            modelBuilder.Entity<RecipeIngredient>()
                .HasRequired(ri => ri.InventoryItem).WithMany()
                .HasForeignKey(ri => ri.InventoryItemId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StockOrderLine>()
                .HasRequired(l => l.StockOrder).WithMany(o => o.Lines)
                .HasForeignKey(l => l.StockOrderId).WillCascadeOnDelete(true);

            modelBuilder.Entity<StockOrderLine>()
                .HasRequired(l => l.InventoryItem).WithMany()
                .HasForeignKey(l => l.InventoryItemId).WillCascadeOnDelete(false);

            // ─── Meal Plan / Selection / Service ─────────────────────
            modelBuilder.Entity<MealPlan>()
                .HasRequired(m => m.Student).WithMany()
                .HasForeignKey(m => m.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealPlan>()
                .HasRequired(m => m.WeeklyMenu).WithMany()
                .HasForeignKey(m => m.WeeklyMenuId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealPlanItem>()
                .HasRequired(i => i.MealPlan).WithMany(m => m.Items)
                .HasForeignKey(i => i.MealPlanId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealPlanItem>()
                .HasRequired(i => i.MenuMeal).WithMany()
                .HasForeignKey(i => i.MenuMealId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealSelection>()
                .HasRequired(s => s.Student).WithMany()
                .HasForeignKey(s => s.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealSelection>()
                .HasRequired(s => s.MenuMeal).WithMany()
                .HasForeignKey(s => s.MenuMealId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealSelection>()
                .HasRequired(s => s.MenuItem).WithMany()
                .HasForeignKey(s => s.MenuItemId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealService>()
                .HasRequired(s => s.Student).WithMany()
                .HasForeignKey(s => s.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealService>()
                .HasRequired(s => s.MenuMeal).WithMany()
                .HasForeignKey(s => s.MenuMealId).WillCascadeOnDelete(false);

            modelBuilder.Entity<MealService>()
                .HasRequired(s => s.DiningHall).WithMany()
                .HasForeignKey(s => s.DiningHallId).WillCascadeOnDelete(false);

            // ─── Dietary ─────────────────────────────────────────────
            modelBuilder.Entity<StudentDietaryRecord>()
                .HasRequired(d => d.Student).WithMany()
                .HasForeignKey(d => d.StudentId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentDietaryRecord>()
                .HasOptional(d => d.Allergen).WithMany()
                .HasForeignKey(d => d.AllergenId).WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentDietaryRecord>()
                .HasOptional(d => d.DietaryCategory).WithMany()
                .HasForeignKey(d => d.DietaryCategoryId).WillCascadeOnDelete(false);

            modelBuilder.Entity<DietaryChangeRequest>()
                .HasRequired(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentId).WillCascadeOnDelete(false);

            // ─── Sports Meal Rules ───────────────────────────────────
            modelBuilder.Entity<SportsMealRecommendationRule>()
                .HasOptional(r => r.MealSlot).WithMany()
                .HasForeignKey(r => r.MealSlotId).WillCascadeOnDelete(false);

            // ─── Unique key on CafeteriaSetting.Key ──────────────────
            modelBuilder.Entity<CafeteriaSetting>()
                .HasIndex(s => s.Key).IsUnique();

            // ─── Legacy entities still referenced ────────────────────
            modelBuilder.Entity<Selection>()
                .HasRequired(s => s.Student).WithMany()
                .HasForeignKey(s => s.StudentID).WillCascadeOnDelete(false);

            modelBuilder.Entity<RSVP>()
                .HasRequired(r => r.Event).WithMany()
                .HasForeignKey(r => r.EventID).WillCascadeOnDelete(false);

            modelBuilder.Entity<RSVP>()
                .HasRequired(r => r.Student).WithMany()
                .HasForeignKey(r => r.StudentID).WillCascadeOnDelete(false);
        }
    }
}