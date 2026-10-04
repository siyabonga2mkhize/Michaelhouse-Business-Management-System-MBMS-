using System;
using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    // ============================================================
    // Web API endpoints (api/mobile/*, MobileApiController) — data
    // classes from Michaelhouse.Mobile, Increment 1 & 2 only.
    // ============================================================

    // A Web API reply: Data is the server's object/list (null when the
    // server answered null, e.g. "no active emergency")
    public class MobileResult<T>
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public T? Data { get; set; }
    }

    // GET student/alerts
    public class StudentAlertDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string? Type { get; set; }
    }

    // GET student/emergency (null when there's no active alert)
    public class EmergencyAlertDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public string AssemblyPoint { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusMeters { get; set; }
        public bool SirenStopped { get; set; }
    }

    // POST student/emergency/confirm { latitude, longitude }
    public class EmergencyConfirmationDto
    {
        public bool Success { get; set; }
        public bool WithinGeofence { get; set; }
        public double Distance { get; set; }
        public string Message { get; set; } = "";
    }

    // GET housemaster/alerts
    public class HouseMasterAlertDto
    {
        public int Id { get; set; }
        public string Residence { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    // GET housemaster/residences
    public class HouseMasterResidenceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // GET housemaster/residences/{id}/rollcall
    public class RollCallStudentDto
    {
        public int StudentId { get; set; }
        public string Name { get; set; } = "";
    }

    // GET students/{id}/qr
    public class StudentQrDto
    {
        public int StudentId { get; set; }
        public string Value { get; set; } = "";
        public string ImageBase64 { get; set; } = "";
    }

    // POST qr/scan { qrValue }
    public class QrScanResultDto
    {
        public bool Approved { get; set; }
        public string? Reason { get; set; }
    }

    // POST api/residence-scan/lookup | check-in | check-out (MVC, House Master)
    public class ResidenceScanDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }

        public int StudentId { get; set; }
        public string Name { get; set; } = "";
        public string? StudentNumber { get; set; }
        public string? Residence { get; set; }
        public string? Room { get; set; }
        public string? Bed { get; set; }
        public bool HasAssignment { get; set; }
        public string Status { get; set; } = "";        // Inside / Outside / OnHoliday / WeekendLeave / Suspended / Archived
        public string StatusLabel { get; set; } = "";
        public string? LastCheckIn { get; set; }
        public string? LastCheckOut { get; set; }
        public bool CanCheckIn { get; set; }
        public bool CanCheckOut { get; set; }
    }

    // ============================================================
    // Report Asset Failure — api/fault-reports (MVC, any user)
    // ============================================================
    public class FaultFormDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public List<string> Priorities { get; set; } = new List<string>();
        public List<string> Categories { get; set; } = new List<string>();
        public List<AssetDto> Assets { get; set; } = new List<AssetDto>();
        public List<FaultReportSummaryDto> MyReports { get; set; } = new List<FaultReportSummaryDto>();
    }

    public class AssetDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Building { get; set; }
        public string? Room { get; set; }
        public string? Category { get; set; }

        public string Label => Name + (string.IsNullOrEmpty(Building) ? "" : " · " + Building) + (string.IsNullOrEmpty(Room) ? "" : ", " + Room);
    }

    public class FaultReportSummaryDto
    {
        public string JobReference { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Asset { get; set; }
        public string Priority { get; set; } = "";
        public string Status { get; set; } = "";
        public string Reported { get; set; } = "";
        public string? AssignedTo { get; set; }
    }

    public class FaultReportRequestDto
    {
        public int? AssetId { get; set; }
        public string? ManualAssetName { get; set; }
        public string? ManualAssetLocation { get; set; }
        public string? ManualCategory { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Priority { get; set; } = "";
        public string? PhotoBase64 { get; set; }
    }

    public class FaultReportResultDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? JobReference { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
    }

    // ============================================================
    // Request Permission to Leave — api/leave-requests (MVC, Student)
    // ============================================================
    public class LeaveFormDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public bool CanRequest { get; set; }
        public string? CannotRequestReason { get; set; }
        public string? Residence { get; set; }
        public string? HouseMaster { get; set; }
        public List<LeaveSummaryDto> MyRequests { get; set; } = new List<LeaveSummaryDto>();
    }

    public class LeaveSummaryDto
    {
        public int Id { get; set; }
        public string Destination { get; set; } = "";
        public string Departure { get; set; } = "";
        public string ExpectedReturn { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string? ParentComments { get; set; }
        public string? HouseMasterComments { get; set; }
        public bool HasCalendarConflict { get; set; }
        public string Submitted { get; set; } = "";
    }

    public class LeaveConflictsDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public bool HasConflict { get; set; }
        public string? Message { get; set; }
        public List<LeaveConflictDto> Conflicts { get; set; } = new List<LeaveConflictDto>();
    }

    public class LeaveConflictDto
    {
        public string Title { get; set; } = "";
        public string Start { get; set; } = "";
        public string End { get; set; } = "";
        public string? Category { get; set; }
    }

    public class LeaveRequestDto
    {
        public string Destination { get; set; } = "";
        public DateTime DepartureDateTime { get; set; }
        public DateTime ExpectedReturnDateTime { get; set; }
        public string Reason { get; set; } = "";
        public bool AcknowledgeConflict { get; set; }
    }

    // ============================================================
    // Request Visitor Access — api/visitor-requests (MVC, Student)
    // ============================================================
    public class VisitorFormDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? BoardingHouse { get; set; }
        public List<OptionValueDto> Zones { get; set; } = new List<OptionValueDto>();
        public string DefaultDate { get; set; } = "";
        public string DefaultStart { get; set; } = "14:00";
        public string DefaultEnd { get; set; } = "16:00";
        public List<VisitSummaryDto> MyVisits { get; set; } = new List<VisitSummaryDto>();
    }

    public class OptionValueDto
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class VisitSummaryDto
    {
        public int Id { get; set; }
        public string Visitor { get; set; } = "";
        public string? Relationship { get; set; }
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string Zone { get; set; } = "";
        public string? Purpose { get; set; }
        public string Status { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string? Remarks { get; set; }
        public string? PolicyFlag { get; set; }
        public string? GatePass { get; set; }
    }

    public class VisitorRequestDto
    {
        public string VisitorFullName { get; set; } = "";
        public string VisitorPhone { get; set; } = "";
        public string? VisitorIdOrPassport { get; set; }
        public string RelationshipToStudent { get; set; } = "";
        public bool IsParentOrGuardian { get; set; }
        public string VisitorEmail { get; set; } = "";
        public DateTime VisitDate { get; set; }
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string RequestedZone { get; set; } = "";
        public string PurposeOfVisit { get; set; } = "";
    }

    // ============================================================
    // Complete Job Card — api/worker-jobs (MVC, Maintenance Worker)
    // ============================================================
    public class WorkerJobsDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? Worker { get; set; }
        public string? Skill { get; set; }
        public string? CurrentStatus { get; set; }
        public List<JobSummaryDto> OpenJobs { get; set; } = new List<JobSummaryDto>();
        public List<JobSummaryDto> CompletedJobs { get; set; } = new List<JobSummaryDto>();
    }

    public class JobSummaryDto
    {
        public int Id { get; set; }
        public string JobReference { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Asset { get; set; }
        public string Priority { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Due { get; set; }
        public bool Overdue { get; set; }
        public string? Reported { get; set; }
        public string? Completed { get; set; }
        public string? FinalCondition { get; set; }
        public decimal? TotalCost { get; set; }
    }

    public class JobDetailDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public JobSummaryDto Job { get; set; } = new JobSummaryDto();
        public string? Description { get; set; }
        public string? Location { get; set; }
        public string? PhotoBeforeBase64 { get; set; }
        public List<string> FinalConditions { get; set; } = new List<string>();
        public List<InventoryItemDto> Inventory { get; set; } = new List<InventoryItemDto>();
    }

    public class InventoryItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Category { get; set; }
        public int Stock { get; set; }
        public string? Unit { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class CompleteJobRequestDto
    {
        public string? CompletionNotes { get; set; }
        public string FinalCondition { get; set; } = "";
        public string PhotoAfterBase64 { get; set; } = "";
        public List<PartUsedDto> Parts { get; set; } = new List<PartUsedDto>();
        public decimal LabourCost { get; set; }
    }

    public class PartUsedDto
    {
        public int InventoryId { get; set; }
        public int Quantity { get; set; }
    }

    // Reply to a submit: Error is all problems in one line, Errors one each
    public class SubmitResultDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string? Message { get; set; }
        public bool HasConflict { get; set; }
    }

    // POST ... { success = true }
    public class SuccessDto
    {
        public bool Success { get; set; }
    }
}
