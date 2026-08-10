using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    // ───────────────────────────────────────────────────────────────────
    // MAINTENANCE STAFF — extended with advanced fields
    // ───────────────────────────────────────────────────────────────────
    public class MaintenanceStaff
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [Display(Name = "Staff Number")]
        public string StaffNumber { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        // ─── Skills & Job Details ──────────────────────────────
        [Required]
        [Display(Name = "Skill Type")]
        public string SkillType { get; set; }

        [Display(Name = "All Skills")]
        public string Skills { get; set; }

        [Display(Name = "Proficiency Level")]
        public string ProficiencyLevel { get; set; }

        [Display(Name = "Certifications")]
        public string Certifications { get; set; }

        [Display(Name = "Certification Expiry")]
        public DateTime? CertificationExpiry { get; set; }

        [Display(Name = "Job Title")]
        public string JobTitle { get; set; }

        [Display(Name = "Employee Type")]
        public string EmployeeType { get; set; }

        // ─── Contact & Emergency ────────────────────────────────
        [Display(Name = "Emergency Contact Name")]
        public string EmergencyContactName { get; set; }

        [Display(Name = "Emergency Contact Phone")]
        public string EmergencyContactPhone { get; set; }

        // ─── Shift & Schedule ────────────────────────────────────
        [Required]
        [Display(Name = "Shift Start")]
        public TimeSpan ShiftStart { get; set; }

        [Required]
        [Display(Name = "Shift End")]
        public TimeSpan ShiftEnd { get; set; }

        [Display(Name = "Current Status")]
        public string CurrentStatus { get; set; }

        [Display(Name = "Date Joined")]
        public DateTime DateJoined { get; set; }

        public bool IsActive { get; set; }

        // ─── Performance Metrics ──────────────────────────────
        [Display(Name = "Jobs Completed (This Month)")]
        public int JobsCompletedThisMonth { get; set; }

        [Display(Name = "Average Response Time (mins)")]
        public int AvgResponseTime { get; set; }

        [Display(Name = "On‑Time Completion Rate (%)")]
        public int OnTimeCompletionRate { get; set; }

        [Display(Name = "Attendance Score (%)")]
        public int AttendanceScore { get; set; }

        [Display(Name = "Leave Balance (days)")]
        public int LeaveBalance { get; set; }

        // ─── Photo ────────────────────────────────────────────────
        [Display(Name = "Profile Photo")]
        public string PhotoUrl { get; set; }
        [Display(Name = "Receipt File")]
        public string ReceiptFileName { get; set; }

        // ─── Link to AppUser ─────────────────────────────────────
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; }

        // ─── Navigation collections ──────────────────────────────
        public virtual ICollection<JobCard> JobCards { get; set; }
        public virtual ICollection<LeaveRequest> LeaveRequests { get; set; }
        public virtual ICollection<StaffShift> StaffShifts { get; set; }

        public MaintenanceStaff()
        {
            CurrentStatus = "Available";
            DateJoined = DateTime.Now;
            IsActive = true;
            JobCards = new HashSet<JobCard>();
            LeaveRequests = new HashSet<LeaveRequest>();
            StaffShifts = new HashSet<StaffShift>();
            JobsCompletedThisMonth = 0;
            AvgResponseTime = 0;
            OnTimeCompletionRate = 0;
            AttendanceScore = 100;
            LeaveBalance = 15;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LEAVE REQUEST
    // ──────────────────────────────────────────────────────────────────────────
    public class LeaveRequest
    {
        public int Id { get; set; }

        [Required]
        public int StaffId { get; set; }

        [Required]
        public string Type { get; set; } // Sick, Annual, Emergency, Other

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Display(Name = "Reason")]
        public string Reason { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Pending, Approved, Denied

        [Display(Name = "Requested At")]
        public DateTime RequestedAt { get; set; }

        [Display(Name = "Approved By")]
        public int? ApprovedByUserId { get; set; }

        [Display(Name = "Approved At")]
        public DateTime? ApprovedAt { get; set; }

        [ForeignKey("StaffId")]
        public virtual MaintenanceStaff Staff { get; set; }

        [ForeignKey("ApprovedByUserId")]
        public virtual AppUser ApprovedBy { get; set; }

        public LeaveRequest()
        {
            Status = "Pending";
            RequestedAt = DateTime.Now;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SHIFT PATTERN (Template)
    // ──────────────────────────────────────────────────────────────────────────
    public class ShiftPattern
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } // Morning, Afternoon, Night

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public string Description { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // STAFF SHIFT (Actual assignment for a specific date)
    // ──────────────────────────────────────────────────────────────────────────
    public class StaffShift
    {
        public int Id { get; set; }

        [Required]
        public int StaffId { get; set; }

        [Required]
        public int ShiftPatternId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        public string Notes { get; set; }

        [ForeignKey("StaffId")]
        public virtual MaintenanceStaff Staff { get; set; }

        [ForeignKey("ShiftPatternId")]
        public virtual ShiftPattern ShiftPattern { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ASSET – extended with warranty and cost tracking
    // ──────────────────────────────────────────────────────────────────────────
    public class Asset
    {
        // ── NEW: Make AssetId nullable ──────────────────────────────────────────
        // So we can have job cards that are NOT linked to a registered asset.
        public int? AssetId { get; set; }

        // ── NEW: Manual entry fields ────────────────────────────────────────────
        // These are used when someone reports a fault on an unregistered asset.
        [Display(Name = "Manual Asset Name")]
        public string ManualAssetName { get; set; }

        [Display(Name = "Manual Asset Location")]
        public string ManualAssetLocation { get; set; }

        [Display(Name = "Manual Category")]
        public string ManualCategory { get; set; }
        public int Id { get; set; }

        [Required]
        [Display(Name = "Asset Name")]
        public string AssetName { get; set; }

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required]
        [Display(Name = "Building / Location")]
        public string LocationBuilding { get; set; }

        [Display(Name = "Room / Area")]
        public string LocationRoom { get; set; }

        public string Description { get; set; }

        [Display(Name = "Condition")]
        public string ConditionRating { get; set; }

        [Display(Name = "QR Code")]
        public string QrCode { get; set; }

        [Display(Name = "Health Score")]
        public int HealthScore { get; set; }

        [Display(Name = "Fault Count")]
        public int FaultCount { get; set; }

        public string Status { get; set; }

        [Display(Name = "Date Registered")]
        public DateTime DateRegistered { get; set; }

        public int RegisteredById { get; set; }

        // ─── New advanced fields ──────────────────────────────────
        [Display(Name = "Warranty Expiry")]
        [DataType(DataType.Date)]
        public DateTime? WarrantyExpiry { get; set; }

        [Display(Name = "Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }

        [Display(Name = "Purchase Cost (R)")]
        public decimal? PurchaseCost { get; set; }

        [Display(Name = "Total Maintenance Cost (R)")]
        public decimal? TotalMaintenanceCost { get; set; }

        [Display(Name = "Model / Serial No.")]
        public string ModelSerial { get; set; }

        [Display(Name = "Supplier")]
        public string Supplier { get; set; }

        public virtual ICollection<JobCard> JobCards { get; set; }

        public Asset()
        {
            ConditionRating = "Good";
            HealthScore = 100;
            FaultCount = 0;
            Status = "Active";
            DateRegistered = DateTime.Now;
            JobCards = new HashSet<JobCard>();
            TotalMaintenanceCost = 0;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // JOB CARD
    // ──────────────────────────────────────────────────────────────────────────
    public class JobCard
    {
        
            public int Id { get; set; }

            [Display(Name = "Job Reference")]
            public string JobReference { get; set; }

            [Required]
            public string Title { get; set; }

            public string Description { get; set; }

            [Display(Name = "Job Type")]
            public string JobType { get; set; }

            [Required]
            public string Priority { get; set; }

            public string Status { get; set; }

            [Display(Name = "Photo Before")]
            public string PhotoBefore { get; set; }

            [Display(Name = "Photo After")]
            public string PhotoAfter { get; set; }

            [Display(Name = "Completion Notes")]
            public string CompletionNotes { get; set; }

            [Display(Name = "Final Condition")]
            public string FinalCondition { get; set; }

            [Display(Name = "Date Created")]
            public DateTime DateCreated { get; set; }

            [Display(Name = "Date Assigned")]
            public DateTime? DateAssigned { get; set; }

            [Display(Name = "Date Completed")]
            public DateTime? DateCompleted { get; set; }

            [Display(Name = "Due Date")]
            public DateTime? DueDate { get; set; }

            [Display(Name = "Response Time (mins)")]
            public int? ResponseTimeMinutes { get; set; }

            // ─── Cost tracking ──────────────────────────────────────────────────
            [Display(Name = "Labour Cost (R)")]
            public decimal? LabourCost { get; set; }

            [Display(Name = "Parts Cost (R)")]
            public decimal? PartsCost { get; set; }

            [Display(Name = "Total Cost (R)")]
            public decimal? TotalCost { get; set; }

            // ─── Asset (now nullable) ──────────────────────────────────────────
            public int? AssetId { get; set; }

            public int? AssignedToId { get; set; }

            public int ReportedById { get; set; }

            // ─── NEW: Manual entry fields ──────────────────────────────────────
            [Display(Name = "Manual Asset Name")]
            public string ManualAssetName { get; set; }

            [Display(Name = "Manual Asset Location")]
            public string ManualAssetLocation { get; set; }

            [Display(Name = "Manual Category")]
            public string ManualCategory { get; set; }

            // ─── Navigation properties ─────────────────────────────────────────
            [ForeignKey("AssetId")]
            public virtual Asset Asset { get; set; }

            [ForeignKey("AssignedToId")]
            public virtual MaintenanceStaff AssignedTo { get; set; }

            public JobCard()
            {
                JobReference = string.Empty;
                JobType = "Reactive";
                Priority = "Medium";
                Status = "Pending";
                DateCreated = DateTime.Now;
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // MAINTENANCE INVENTORY
        // ──────────────────────────────────────────────────────────────────────────
        public class MaintenanceInventory
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; }

        [Required]
        public string Category { get; set; }

        [Display(Name = "Stock Level")]
        public int StockLevel { get; set; }

        [Display(Name = "Minimum Stock")]
        public int MinimumStock { get; set; }

        [Display(Name = "Unit")]
        public string Unit { get; set; }

        [Display(Name = "Unit Cost (R)")]
        public decimal UnitCost { get; set; }

        public string Supplier { get; set; }

        [Display(Name = "Last Restocked")]
        public DateTime? LastRestocked { get; set; }

        public bool IsLowStock
        {
            get { return StockLevel <= MinimumStock; }
        }

        public MaintenanceInventory()
        {
            StockLevel = 0;
            MinimumStock = 5;
            Unit = "pieces";
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // JOB CARD PART
    // ──────────────────────────────────────────────────────────────────────────
    public class JobCardPart
    {
        public int Id { get; set; }

        public int JobCardId { get; set; }

        public int? InventoryItemId { get; set; } // now nullable

        public int QuantityUsed { get; set; }

        public DateTime DateUsed { get; set; }


        public virtual JobCard JobCard { get; set; }
        public virtual MaintenanceInventory InventoryItem { get; set; }

        public JobCardPart()
        {
            DateUsed = DateTime.Now;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PREVENTIVE SCHEDULE
    // ──────────────────────────────────────────────────────────────────────────
    public class PreventiveSchedule
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Schedule Name")]
        public string ScheduleName { get; set; }

        [Required]
        public int AssetId { get; set; }

        [Required]
        [Display(Name = "Task Description")]
        public string TaskDescription { get; set; }

        [Required]
        public string Frequency { get; set; }

        [Required]
        [Display(Name = "Skill Required")]
        public string SkillRequired { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Display(Name = "Next Due Date")]
        public DateTime NextDueDate { get; set; }

        [Display(Name = "Last Completed")]
        public DateTime? LastCompletedDate { get; set; }

        public string Status { get; set; }

        public string ComplianceStatus { get; set; }

        public int CreatedById { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey("AssetId")]
        public virtual Asset Asset { get; set; }

        public PreventiveSchedule()
        {
            Status = "Active";
            ComplianceStatus = "Green";
            CreatedAt = DateTime.Now;
            StartDate = DateTime.Now;
            NextDueDate = DateTime.Now;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DASHBOARD HELPER MODELS (not DB tables)
    // ──────────────────────────────────────────────────────────────────────────
    public class BuildingHealthModel
    {
        public string Name { get; set; }
        public int OpenJobs { get; set; }
        public int TotalAssets { get; set; }
        public string HealthColor { get; set; }
    }

    public class WorkerStatModel
    {
        public string Name { get; set; }
        public string Skill { get; set; }
        public string Status { get; set; }
        public int CompletedMonth { get; set; }
        public double AvgResponseTime { get; set; }
        public int OnTimeRate { get; set; }
    }

    public class WorkerPerformanceModel
    {
        public string Name { get; set; }
        public string Skill { get; set; }
        public int Completed { get; set; }
        public int AvgTime { get; set; }
        public int OnTimeRate { get; set; }
    }

    public class StaffDetailsViewModel
    {
        public MaintenanceStaff Staff { get; set; }
        public List<JobCard> RecentJobs { get; set; }
        public List<LeaveRequest> LeaveRequests { get; set; }
        public List<StaffShift> UpcomingShifts { get; set; }
        public int TotalJobsCompleted { get; set; }
        public double AvgResponseTime { get; set; }
        public int OnTimeRate { get; set; }
    }

    public class LeaveRequestViewModel
    {
        [Required]
        public string Type { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public string Reason { get; set; }
    }
    public class JobCardPhoto
    {
        public int Id { get; set; }
        public int JobCardId { get; set; }
        public string FileName { get; set; }
        public string Caption { get; set; } // optional
        public DateTime UploadedAt { get; set; }

        [ForeignKey("JobCardId")]
        public virtual JobCard JobCard { get; set; }
    }
}

