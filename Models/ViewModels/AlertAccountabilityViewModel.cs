using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    public class AlertAccountabilityViewModel
    {
        public EmergencyAlert Alert { get; set; }

        public int TotalStudents { get; set; }
        public int SafeCount { get; set; }
        public int OutsideCount { get; set; }
        public int PendingCount { get; set; }

        public List<StudentSafetyConfirmationViewModel> Confirmations { get; set; }

        public AlertAccountabilityViewModel()
        {
            Confirmations = new List<StudentSafetyConfirmationViewModel>();
        }
    }

    public class StudentSafetyConfirmationViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public SafetyStatus Status { get; set; }
        public double DistanceFromAssemblyPointMeters { get; set; }
        public DateTime ConfirmationTime { get; set; }
        public bool WithinGeofence { get; set; }

        public double StudentLatitude { get; set; }
        public double StudentLongitude { get; set; }

    }
}