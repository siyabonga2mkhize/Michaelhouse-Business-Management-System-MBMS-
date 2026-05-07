using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    /*public class Trip
    {
        public int Id { get; set; }
        [Required]
        public string Destination { get; set; }
        [Required]
        public DateTime TripDate { get; set; }

        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }

        public string Status { get; set; } // Scheduled / Completed / Cancelled

        // Navigation (optional)
        public virtual Driver Driver { get; set; }
        public virtual Vehicle Vehicle { get; set; }
    }*/

    public class TripRequest
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        [Required]
        public string Title { get; set; }
        public string Description { get; set; }
        [Required]
        public DateTime DepartureTime { get; set; }
        [Required]
        public DateTime ReturnTime { get; set; }
        public string Destination { get; set; }
        [Required]
        public int MaxStudents { get; set; }
        public string Status { get; set; } // Pending, Approved, Rejected, Scheduled
        public string RejectionReason { get; set; }
        public DateTime RequestedAt { get; set; }
        public int? ApprovedByAdminId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        // Navigation properties
        public virtual Teacher Teacher { get; set; }
        public virtual AppUser ApprovedBy { get; set; }
    }
    public class TripSchedule
    {
        public int Id { get; set; }
        public int TripRequestId { get; set; }
        public int TeacherId { get; set; }
        [Required]
        public DateTime ScheduledDate { get; set; }
        public string Status { get; set; } // Draft, Confirmed, InProgress, Completed, Cancelled
        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }
        public string Notes { get; set; }

        // Navigation properties
        public virtual TripRequest TripRequest { get; set; }
        public virtual Teacher Teacher { get; set; }
        public virtual Driver Driver { get; set; }
        public virtual Vehicle Vehicle { get; set; }
        public virtual ICollection<TripStudent> TripStudents { get; set; }
    }

    public class TripStudent
    {
        public int Id { get; set; }
        public int TripScheduleId { get; set; }
        public int StudentId { get; set; }
        public bool? IsPresentBefore { get; set; }
        public bool? IsPresentAfter { get; set; }
        public string MarkedBeforeBy { get; set; }
        public string MarkedAfterBy { get; set; }
        public DateTime? MarkedBeforeAt { get; set; }
        public DateTime? MarkedAfterAt { get; set; }

        public virtual TripSchedule TripSchedule { get; set; }

        public virtual Student Student { get; set; }
    }

    public class TripViewModel
    {
        public TripRequest TripRequest { get; set; }
        public TripSchedule TripSchedule { get; set; }
        public List<TripStudent> TripStudents { get; set; }
    }

    public class TripRequestViewModel
    {
        public TripRequest TripRequest { get; set; }
        public List<Notification> Notifications { get; set; }
    }

    public class TripScheduleViewModel
    {
        public TripSchedule TripSchedule { get; set; }
        public List<TripStudent> TripStudents { get; set; }
        public List<Notification> Notifications { get; set; }
    }

    public class TripManagementViewModel
    {
        public List<TripRequest> TripRequests { get; set; }
        public List<TripSchedule> TripSchedules { get; set; }
        public List<Notification> Notifications { get; set; }
    }

    public class TripStudentViewModel
    {
        public TripStudent TripStudent { get; set; }
        public List<Notification> Notifications { get; set; }
    }

    public class TripRequestCreateViewModel
    {
        [Required]
        public string Title { get; set; }
        public string Description { get; set; }

        [Required]
        public DateTime DepartureTime { get; set; } = DateTime.Today.AddHours(9); // today at 9 AM

        [Required]
        public DateTime ReturnTime { get; set; } = DateTime.Today.AddHours(16);   // today at 4 PM

        public string Destination { get; set; }

        // Optional for future use: store coordinates
        public double? DestinationLat { get; set; }
        public double? DestinationLng { get; set; }

        [Required]
        public int MaxStudents { get; set; }
    }

    public class TripScheduleCreateViewModel
    {
        public int TripRequestId { get; set; }
        [Required]
        public DateTime ScheduledDate { get; set; }
        public string Notes { get; set; }
    }

    public class TripStudentMarkViewModel
    {
        public int TripScheduleId { get; set; }
        public int StudentId { get; set; }
        public bool IsPresentBefore { get; set; }
        public bool IsPresentAfter { get; set; }
    }
    public class TripManagementDashboardViewModel
    {
        public List<TripRequest> RecentTripRequests { get; set; }
        public List<TripSchedule> UpcomingTrips { get; set; }
        public List<Notification> RecentNotifications { get; set; }
    }

    public class TripRequestApprovalViewModel
    {
        public TripRequest TripRequest { get; set; }
        public List<Notification> Notifications { get; set; }
    }
    public class TripScheduleConfirmationViewModel
    {
        public TripSchedule TripSchedule { get; set; }
        public List<Notification> Notifications { get; set; }
    }

    public class TripStudentAttendanceViewModel
    {
        public TripSchedule TripSchedule { get; set; }
        public List<TripStudent> TripStudents { get; set; }
        public List<Notification> Notifications { get; set; }
    }





}
