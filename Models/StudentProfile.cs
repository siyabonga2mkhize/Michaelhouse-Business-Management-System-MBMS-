using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    // Allocation-specific data only. DOB and Grade live on Student already —
    // this table is purely the "extra" information the Smart Allocation
    // Engine needs that Student doesn't capture.
    public class StudentProfile
    {
        [Key]
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public string AcademicStream { get; set; } // STEM / Commerce / Arts / Humanities

        // Grade as a string (e.g. "8", "9", "10") used by allocation rules
        public string Grade { get; set; }

        // Comma-separated — simple and query-friendly for this project's scale
        public string ElectiveSubjects { get; set; }
        public string Sports { get; set; }
        public string ClubsAndSocieties { get; set; }

        public bool AccessibilityRequired { get; set; }
        public string AccessibilityNotes { get; set; }

        public bool MedicalAccommodationRequired { get; set; }
        public string MedicalAccommodationNotes { get; set; }

        public int? PreviousResidenceId { get; set; }
        public int? PreviousRoomId { get; set; }

        // Comma-separated StudentIds of previous roommates who were a good fit
        public string PreviousRoommateIds { get; set; }

        // Helper: calculate age from linked Student DOB
        public int CalculateAge()
        {
            if (Student == null || Student.DOB == default)
                return 0;
            var today = System.DateTime.Today;
            var age = today.Year - Student.DOB.Year;
            if (Student.DOB.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}