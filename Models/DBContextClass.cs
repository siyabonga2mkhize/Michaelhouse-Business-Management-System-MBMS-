using Michaelhouse.Controllers;
using Michaelhouse.Models.Cafeteria;
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

            if (configured != null &&
                !string.IsNullOrWhiteSpace(configured.ConnectionString))
            {
                return configured.ConnectionString;
            }

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
        public DbSet<SchoolClass> SchoolClasses { get; set; }


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

        // ─── Cafeteria ────────────────────────────────────────────────────
        public DbSet<MealMenu> MealMenus { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuScheduleItem> MenuScheduleItems { get; set; }

        public DbSet<Ingredient> Ingredients { get; set; }

        public DbSet<Recipe> Recipes { get; set; }

        public DbSet<RecipeIngredient> RecipeIngredients { get; set; }

        public DbSet<MealPlan> MealPlans { get; set; }

        public DbSet<MealPlanItem> MealPlanItems { get; set; }
        public DbSet<StudentFaceSignature> StudentFaceSignatures { get; set; }

        public DbSet<MealCollection> MealCollections { get; set; }

        // ─── Driver ───────────────────────────────────────────────────────
        public DbSet<DriverApplication> DriverApplications { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<DriverAvailability> DriverAvailabilities { get; set; }
        public DbSet<DriverDocument> DriverDocuments { get; set; }

        // ─── Vehicle ──────────────────────────────────────────────────────
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<VehicleIssue> VehicleIssues { get; set; }

        // ─── Inventory & Suppliers ────────────────────────────────────────
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

        // ─── Attendance & Verification ───────────────────────────────────
        public DbSet<StudentAttendanceToken> StudentAttendanceTokens { get; set; }
        public DbSet<StudentQRCode> StudentQRCodes { get; set; }

        // ─── Boarding / Residence Entities ────────────────────────────────
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
        // ─── Leave Requests ─────────────────────
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<Schoolcalendarevent.SchoolCalendarEvent> SchoolCalendarEvents { get; set; }

        // ─── Visitor Access Management ───────────────────────────────────
        public DbSet<VisitorAccessRequest> VisitorAccessRequests { get; set; }
        public DbSet<TermCalendar> TermCalendars { get; set; }
        public DbSet<VisitorScanLog> VisitorScanLogs { get; set; }
        public DbSet<CampusRule> CampusRules { get; set; }
        public DbSet<SportEvent> SportEvents { get; set; }
        public DbSet<EventVenue> EventVenues { get; set; }
        public DbSet<CafeteriaEvent> CafeteriaEvents { get; set; }
        public DbSet<EventMenuTemplate> EventMenuTemplates { get; set; }
        public DbSet<EventMenuTemplateItem> EventMenuTemplateItems { get; set; }
        public DbSet<EventStaffAssignment> EventStaffAssignments { get; set; }
        public DbSet<CafeteriaEventMenuItem> CafeteriaEventMenuItems { get; set; }

        // ─── Cafeteria ingredient inventory ───────────────────────────────
        public DbSet<IngredientSupplier> IngredientSuppliers { get; set; }
        public DbSet<IngredientPurchaseOrder> IngredientPurchaseOrders { get; set; }
        public DbSet<IngredientPurchaseOrderLine> IngredientPurchaseOrderLines { get; set; }
        public DbSet<IngredientDelivery> IngredientDeliveries { get; set; }
        public DbSet<IngredientDeliveryLine> IngredientDeliveryLines { get; set; }
        public DbSet<IngredientStockTransaction> IngredientStockTransactions { get; set; }
        public DbSet<KitchenIngredientIssue> KitchenIngredientIssues { get; set; }

        public DbSet<EventRsvp> EventRsvps { get; set; }
        public DbSet<EventRsvpGuest> EventRsvpGuests { get; set; }

        // ─── UC19 Generate Feast Plan ───
        public DbSet<EventFeastPlan> EventFeastPlans { get; set; }
        public DbSet<EventFeastPlanItem> EventFeastPlanItems { get; set; }
        public DbSet<EventFeastPlanHistory> EventFeastPlanHistories { get; set; }
        public DbSet<MenuItemBuffetTag> MenuItemBuffetTags { get; set; }
        public DbSet<EventDishLeftover> EventDishLeftovers { get; set; }

        public DbSet<StudentSportStatus> StudentSportStatuses { get; set; }

        public DbSet<SportPriority> SportPriorities { get; set; }

        // ─── Maintenance Management ─────────────────────────────────────
        public DbSet<MaintenanceStaff> MaintenanceStaff { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<JobCard> JobCards { get; set; }
        public DbSet<JobCardPhoto> JobCardPhotos { get; set; }
        public DbSet<MaintenanceInventory> MaintenanceInventory { get; set; }
        public DbSet<JobCardPart> JobCardParts { get; set; }
        public DbSet<PreventiveSchedule> PreventiveSchedules { get; set; }
        public DbSet<ShiftPattern> ShiftPatterns { get; set; }
        public DbSet<StaffShift> StaffShifts { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UC19 — leftover records keep their history if a meal or plan is removed
            modelBuilder.Entity<EventDishLeftover>()
                .HasRequired(l => l.MenuItem)
                .WithMany()
                .HasForeignKey(l => l.MenuItemId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<EventDishLeftover>()
                .HasOptional(l => l.FeastPlan)
                .WithMany()
                .HasForeignKey(l => l.FeastPlanId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuItemBuffetTag>()
                .HasRequired(t => t.MenuItem)
                .WithMany()
                .HasForeignKey(t => t.MenuItemId)
                .WillCascadeOnDelete(true);

            // ── Applications & Documents ─────────────────────────────────

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

            // ── Academic Relationships ───────────────────────────────────

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

            modelBuilder.Entity<StreamEnrolment>()
                .HasOptional(se => se.Teacher)
                .WithMany(t => t.StreamEnrolments)
                .HasForeignKey(se => se.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Teacher>()
                .HasOptional(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasRequired(tsg => tsg.Teacher)
                .WithMany(t => t.SubjectAssignments)
                .HasForeignKey(tsg => tsg.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TeacherSubjectGrade>()
                .HasRequired(tsg => tsg.Subject)
                .WithMany()
                .HasForeignKey(tsg => tsg.SubjectId)
                .WillCascadeOnDelete(false);

            // ── Assessment & Marks ───────────────────────────────────────

            modelBuilder.Entity<Assessment>()
                .HasRequired(a => a.Subject)
                .WithMany()
                .HasForeignKey(a => a.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StudentMark>()
                .HasRequired(sm => sm.Assessment)
                .WithMany(a => a.StudentMarks)
                .HasForeignKey(sm => sm.AssessmentmentId)
                .WillCascadeOnDelete(true);

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
                .HasForeignKey(yr => yr.StudentId);

            // ── Finance ──────────────────────────────────────────────────

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

            // ── Cafeteria ────────────────────────────────────────────────
            // Explicitly map every MenuScheduleItem relationship so EF6
            // does not create convention-based foreign keys such as
            // MenuItem_Id, MenuItem_Id1, or MealMenu_Id.

            modelBuilder.Entity<MealMenu>()
                .HasKey(mm => mm.Id);

            modelBuilder.Entity<MealMenu>()
                .Property(mm => mm.SpecialEventNotes)
                .HasMaxLength(2000);

            modelBuilder.Entity<MealMenu>()
                .Property(mm => mm.RejectionReason)
                .HasMaxLength(2000);

            modelBuilder.Entity<MealMenu>()
                .HasMany(mm => mm.ScheduleItems)
                .WithRequired(ms => ms.MealMenu)
                .HasForeignKey(ms => ms.MealMenuId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<MealMenu>()
                .HasOptional(mm => mm.RegeneratedFromMenu)
                .WithMany()
                .HasForeignKey(mm => mm.RegeneratedFromMenuId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuItem>()
                .HasKey(mi => mi.Id);

            modelBuilder.Entity<MenuItem>()
                .Property(mi => mi.Name)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<MenuItem>()
                .Property(mi => mi.DietaryClassification)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<MenuItem>()
                .Property(mi => mi.CaloriesPerPortion)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MenuItem>()
                .Property(mi => mi.ProteinGramsPerPortion)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MenuItem>()
                .Property(mi => mi.CarbohydrateGramsPerPortion)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MenuItem>()
                .Property(mi => mi.FatGramsPerPortion)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MenuItem>()
                .HasMany(mi => mi.ScheduleItems)
                .WithRequired(ms => ms.MenuItem)
                .HasForeignKey(ms => ms.MenuItemId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuItem>()
                .HasMany(mi => mi.SubstitutionScheduleItems)
                .WithOptional(ms => ms.SubstitutionMenuItem)
                .HasForeignKey(ms => ms.SubstitutionMenuItemId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuScheduleItem>()
                .HasKey(ms => ms.Id);

            modelBuilder.Entity<MenuScheduleItem>()
                .HasRequired(ms => ms.MealMenu)
                .WithMany(mm => mm.ScheduleItems)
                .HasForeignKey(ms => ms.MealMenuId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<MenuScheduleItem>()
                .HasRequired(ms => ms.MenuItem)
                .WithMany(mi => mi.ScheduleItems)
                .HasForeignKey(ms => ms.MenuItemId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MenuScheduleItem>()
                .HasOptional(ms => ms.SubstitutionMenuItem)
                .WithMany(mi => mi.SubstitutionScheduleItems)
                .HasForeignKey(ms => ms.SubstitutionMenuItemId)
                .WillCascadeOnDelete(false);

            // ── Cafeteria inventory: no cascading deletes from shared
            //    records (Ingredient, Supplier); lines go with their header
            modelBuilder.Entity<IngredientSupplier>().HasRequired(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientSupplier>().HasRequired(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientPurchaseOrder>().HasRequired(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientPurchaseOrder>().HasOptional(x => x.ShortfallOf).WithMany().HasForeignKey(x => x.ShortfallOfOrderId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientPurchaseOrderLine>().HasRequired(x => x.PurchaseOrder).WithMany(o => o.Lines).HasForeignKey(x => x.PurchaseOrderId).WillCascadeOnDelete(true);
            modelBuilder.Entity<IngredientPurchaseOrderLine>().HasRequired(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientDelivery>().HasOptional(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientDelivery>().HasRequired(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientDeliveryLine>().HasRequired(x => x.Delivery).WithMany(d => d.Lines).HasForeignKey(x => x.DeliveryId).WillCascadeOnDelete(true);
            modelBuilder.Entity<IngredientDeliveryLine>().HasRequired(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientDeliveryLine>().HasOptional(x => x.PurchaseOrderLine).WithMany().HasForeignKey(x => x.PurchaseOrderLineId).WillCascadeOnDelete(false);
            modelBuilder.Entity<IngredientStockTransaction>().HasRequired(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).WillCascadeOnDelete(false);

            modelBuilder.Entity<Ingredient>()
    .HasKey(i => i.Id);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.Name)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.Unit)
                .IsRequired()
                .HasMaxLength(30);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.CaloriesPerUnit)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.ProteinGramsPerUnit)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.CarbohydrateGramsPerUnit)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.FatGramsPerUnit)
                .HasPrecision(18, 4);


            modelBuilder.Entity<Recipe>()
                .HasKey(r => r.Id);

            modelBuilder.Entity<Recipe>()
                .Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<Recipe>()
                .Property(r => r.PreparationNotes)
                .HasMaxLength(1000);


            modelBuilder.Entity<RecipeIngredient>()
                .HasKey(ri => ri.Id);

            modelBuilder.Entity<RecipeIngredient>()
                .Property(ri => ri.QuantityPerStandardPortion)
                .HasPrecision(18, 4);

            modelBuilder.Entity<RecipeIngredient>()
                .Property(ri => ri.PreparationNotes)
                .HasMaxLength(500);


            modelBuilder.Entity<Recipe>()
                .HasMany(r => r.RecipeIngredients)
                .WithRequired(ri => ri.Recipe)
                .HasForeignKey(ri => ri.RecipeId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Ingredient>()
                .HasMany(i => i.RecipeIngredients)
                .WithRequired(ri => ri.Ingredient)
                .HasForeignKey(ri => ri.IngredientId)
                .WillCascadeOnDelete(false);


            modelBuilder.Entity<MenuItem>()
                .HasOptional(mi => mi.Recipe)
                .WithMany(r => r.MenuItems)
                .HasForeignKey(mi => mi.RecipeId)
                .WillCascadeOnDelete(false);

            // ── Timetable Relationships ──────────────────────────────────

            modelBuilder.Entity<TimetableSlot>()
                .HasRequired(ts => ts.Subject)
                .WithMany()
                .HasForeignKey(ts => ts.SubjectId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TimetableSlot>()
                .HasRequired(ts => ts.Period)
                .WithMany()
                .HasForeignKey(ts => ts.PeriodId)
                .WillCascadeOnDelete(false);

            // ── School Store ─────────────────────────────────────────────

            modelBuilder.Entity<Category>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<Category>()
                .Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Category>()
                .Property(c => c.Description)
                .HasMaxLength(500);

            modelBuilder.Entity<Product>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Product>()
                .Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<Product>()
                .Property(p => p.Description)
                .HasMaxLength(1000);

            modelBuilder.Entity<Product>()
                .Property(p => p.ImageUrl)
                .HasMaxLength(500);

            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(false);

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

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasMaxLength(50);

            modelBuilder.Entity<Order>()
                .Property(o => o.Notes)
                .HasMaxLength(500);

            modelBuilder.Entity<OrderItem>()
                .HasKey(oi => oi.Id);

            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<OrderItem>()
                .HasRequired(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId);

            // ── Trip Management ──────────────────────────────────────────

            modelBuilder.Entity<TripRequest>()
                .HasRequired(tr => tr.Teacher)
                .WithMany()
                .HasForeignKey(tr => tr.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripRequest>()
                .HasOptional(tr => tr.ApprovedBy)
                .WithMany()
                .HasForeignKey(tr => tr.ApprovedByAdminId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripRequest>()
                .Property(t => t.Status)
                .HasMaxLength(20);

            modelBuilder.Entity<TripRequest>()
                .Property(t => t.RejectionReason)
                .HasMaxLength(500);

            modelBuilder.Entity<TripRequest>()
                .Property(t => t.Title)
                .HasMaxLength(200);

            modelBuilder.Entity<TripSchedule>()
                .HasRequired(ts => ts.TripRequest)
                .WithMany()
                .HasForeignKey(ts => ts.TripRequestId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .HasRequired(ts => ts.Teacher)
                .WithMany()
                .HasForeignKey(ts => ts.TeacherId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .HasOptional(ts => ts.Driver)
                .WithMany()
                .HasForeignKey(ts => ts.DriverId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .HasOptional(ts => ts.Vehicle)
                .WithMany()
                .HasForeignKey(ts => ts.VehicleId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripSchedule>()
                .Property(t => t.Status)
                .HasMaxLength(20);

            modelBuilder.Entity<TripSchedule>()
                .Property(t => t.Notes)
                .HasMaxLength(500);

            modelBuilder.Entity<TripStudent>()
                .HasRequired(ts => ts.TripSchedule)
                .WithMany(t => t.TripStudents)
                .HasForeignKey(ts => ts.TripScheduleId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripStudent>()
                .HasRequired(ts => ts.Student)
                .WithMany()
                .HasForeignKey(ts => ts.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Notification>()
                .HasRequired(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasRequired(tva => tva.TripSchedule)
                .WithMany(ts => ts.VehicleAssignments)
                .HasForeignKey(tva => tva.TripScheduleId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasRequired(tva => tva.Driver)
                .WithMany()
                .HasForeignKey(tva => tva.DriverId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TripVehicleAssignment>()
                .HasRequired(tva => tva.Vehicle)
                .WithMany()
                .HasForeignKey(tva => tva.VehicleId)
                .WillCascadeOnDelete(false);

            // ── Suppliers & Inventory ─────────────────────────────────────

            modelBuilder.Entity<SupplierProduct>()
                .HasRequired(sp => sp.Supplier)
                .WithMany(s => s.SupplierProducts)
                .HasForeignKey(sp => sp.SupplierId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SupplierProduct>()
                .HasRequired(sp => sp.Product)
                .WithMany()
                .HasForeignKey(sp => sp.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PurchaseOrder>()
                .HasRequired(po => po.Supplier)
                .WithMany(s => s.PurchaseOrders)
                .HasForeignKey(po => po.SupplierId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PurchaseOrderLine>()
                .HasRequired(pol => pol.PurchaseOrder)
                .WithMany(po => po.LineItems)
                .HasForeignKey(pol => pol.PurchaseOrderId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<PurchaseOrderLine>()
                .HasRequired(pol => pol.Product)
                .WithMany()
                .HasForeignKey(pol => pol.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StockMovement>()
                .HasRequired(sm => sm.Product)
                .WithMany()
                .HasForeignKey(sm => sm.ProductId)
                .WillCascadeOnDelete(false);

            // ── Boarding House / Residence Management ────────────────────

            modelBuilder.Entity<Residence>()
                .ToTable("Residences")
                .HasOptional(r => r.HouseMaster)
                .WithMany()
                .HasForeignKey(r => r.HouseMasterId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Room>()
                .HasRequired(r => r.Residence)
                .WithMany(r => r.Rooms)
                .HasForeignKey(r => r.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Bed>()
                .HasRequired(b => b.Room)
                .WithMany(r => r.Beds)
                .HasForeignKey(b => b.RoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Bed>()
                .HasOptional(b => b.OccupiedByStudent)
                .WithMany()
                .HasForeignKey(b => b.OccupiedByStudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Residence)
                .WithMany()
                .HasForeignKey(r => r.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Room)
                .WithMany()
                .HasForeignKey(r => r.RoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAssignment>()
                .HasRequired(r => r.Bed)
                .WithMany()
                .HasForeignKey(r => r.BedId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Residence)
                .WithMany()
                .HasForeignKey(r => r.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Room)
                .WithMany()
                .HasForeignKey(r => r.RoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceAllocation>()
                .HasRequired(r => r.Bed)
                .WithMany()
                .HasForeignKey(r => r.BedId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RoomScoreAudit>()
                .HasKey(rsa => rsa.RoomScoreAuditId);

            modelBuilder.Entity<DisciplinaryConflict>()
                .HasKey(dc => dc.DisciplinaryConflictId);

            modelBuilder.Entity<Student>()
                .HasOptional(s => s.StudentProfile)
                .WithRequired(sp => sp.Student);

            modelBuilder.Entity<StudentQRCode>()
                .HasRequired(q => q.Student)
                .WithMany()
                .HasForeignKey(q => q.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<QRScanRecord>()
                .HasRequired(q => q.Student)
                .WithMany()
                .HasForeignKey(q => q.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<QRScanRecord>()
                .HasOptional(q => q.Residence)
                .WithMany()
                .HasForeignKey(q => q.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<QRScanRecord>()
                .HasOptional(q => q.HouseMaster)
                .WithMany()
                .HasForeignKey(q => q.HouseMasterId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIResidenceRecommendation>()
                .HasRequired(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIResidenceRecommendation>()
                .HasRequired(a => a.Residence)
                .WithMany()
                .HasForeignKey(a => a.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIResidenceRecommendation>()
                .HasRequired(a => a.Room)
                .WithMany()
                .HasForeignKey(a => a.RoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIAllocationHistory>()
                .HasRequired(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIAllocationHistory>()
                .HasRequired(a => a.Residence)
                .WithMany()
                .HasForeignKey(a => a.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIAllocationHistory>()
                .HasRequired(a => a.Room)
                .WithMany()
                .HasForeignKey(a => a.RoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIWaitingList>()
                .HasRequired(w => w.Student)
                .WithMany()
                .HasForeignKey(w => w.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIRecommendationOverride>()
                .HasRequired(o => o.Recommendation)
                .WithMany()
                .HasForeignKey(o => o.RecommendationId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<AIAlert>()
                .HasRequired(a => a.Residence)
                .WithMany()
                .HasForeignKey(a => a.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceMovement>()
                .HasRequired(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.FromResidence)
                .WithMany()
                .HasForeignKey(r => r.FromResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.ToResidence)
                .WithMany()
                .HasForeignKey(r => r.ToResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.FromRoom)
                .WithMany()
                .HasForeignKey(r => r.FromRoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ResidenceMovement>()
                .HasOptional(r => r.ToRoom)
                .WithMany()
                .HasForeignKey(r => r.ToRoomId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<EmergencyAlert>()
                .HasKey(e => e.AlertId);

            modelBuilder.Entity<EmergencyAlert>()
                .HasMany(e => e.StudentConfirmations)
                .WithRequired(c => c.Alert)
                .HasForeignKey(c => c.AlertId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasKey(c => c.ConfirmationId);

            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasRequired(c => c.Student)
                .WithMany()
                .HasForeignKey(c => c.StudentId);

            modelBuilder.Entity<StudentSafetyConfirmation>()
                .HasRequired(c => c.Alert)
                .WithMany(a => a.StudentConfirmations)
                .HasForeignKey(c => c.AlertId);

            modelBuilder.Entity<LeaveRequest>()
                .HasRequired(l => l.Student)
                .WithMany()
                .HasForeignKey(l => l.StudentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<LeaveRequest>()
                .HasRequired(l => l.Parent)
                .WithMany()
                .HasForeignKey(l => l.ParentId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<LeaveRequest>()
                .HasOptional(l => l.Residence)
                .WithMany()
                .HasForeignKey(l => l.ResidenceId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<LeaveRequest>()
                .HasOptional(l => l.HouseMaster)
                .WithMany()
                .HasForeignKey(l => l.HouseMasterId)
                .WillCascadeOnDelete(false);

            // ── Campus Rules ─────────────────────────────────────────────

            modelBuilder.Entity<CampusRule>()
                .HasKey(cr => cr.CampusRuleId);

            modelBuilder.Entity<CampusRule>()
                .Property(cr => cr.RuleName)
                .IsRequired()
                .HasMaxLength(200);
        }
    }
}