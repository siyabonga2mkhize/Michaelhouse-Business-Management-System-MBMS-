using System;
using System.Collections.Generic;

namespace Michaelhouse.Models.ViewModels
{
    public class StudentMyProfileViewModel
    {
        // Read-only institutional
        public string FullName { get; set; }
        public string StudentNumber { get; set; }
        public int GradeLevel { get; set; }
        public string HouseName { get; set; }
        public string RoomNumber { get; set; }
        public string BedNumber { get; set; }
        public string Email { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string BoardingStatus { get; set; }

        // Editable (non-sensitive — saved directly)
        public string PreferredName { get; set; }
        public string HomeLanguage { get; set; }
        public string PhoneNumber { get; set; }

        // Legacy free-text (kept for backward compat, read-only now)
        public string Allergies { get; set; }
        public string MedicalConditions { get; set; }
        public string EmergencyMedication { get; set; }
        public string DietaryPreferences { get; set; }
        public string SpecialDietaryNeeds { get; set; }
        public string Sports { get; set; }

        // NEW — structured collections
        public List<StudentDietaryRecord> DietaryRecords { get; set; } = new List<StudentDietaryRecord>();
        public List<TeamMembership> SportMemberships { get; set; } = new List<TeamMembership>();
        public List<DietaryChangeRequest> PendingRequests { get; set; } = new List<DietaryChangeRequest>();
    }
}