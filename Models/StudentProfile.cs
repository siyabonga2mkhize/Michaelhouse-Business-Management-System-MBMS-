using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class StudentProfile
    {
        [Key]
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public string AcademicStream { get; set; }
        public string Grade { get; set; }

        [StringLength(100)]
        public string PreferredName { get; set; }

        [StringLength(50)]
        public string Gender { get; set; }

        [StringLength(100)]
        public string HomeLanguage { get; set; }

        [StringLength(100)]
        public string Nationality { get; set; }

        [StringLength(100)]
        public string LearningStyle { get; set; }

        public string AcademicInterests { get; set; }
        public string ElectiveSubjects { get; set; }
        public string Sports { get; set; }
        public string ClubsAndSocieties { get; set; }
        public string LeadershipRoles { get; set; }
        public string CulturalActivities { get; set; }

        public string Allergies { get; set; }
        public string Disabilities { get; set; }
        public string MedicalConditions { get; set; }
        public string EmergencyMedication { get; set; }
        public string MobilityRequirements { get; set; }

        public bool AccessibilityRequired { get; set; }
        public string AccessibilityNotes { get; set; }

        public bool MedicalAccommodationRequired { get; set; }
        public string MedicalAccommodationNotes { get; set; }

        public int? PreviousResidenceId { get; set; }
        public int? PreviousRoomId { get; set; }
        public string PreviousRoommateIds { get; set; }

        [StringLength(50)]
        public string EnvironmentPreference { get; set; }

        [StringLength(50)]
        public string RoutinePreference { get; set; }

        [StringLength(50)]
        public string ActivityPreference { get; set; }

        [StringLength(50)]
        public string StudyPreference { get; set; }

        [StringLength(50)]
        public string SocialPreference { get; set; }

        [StringLength(100)]
        public string StudyStyle { get; set; }

        public int SocialScore { get; set; }
        public int LeadershipScore { get; set; }
        public int ActivityScore { get; set; }

        [StringLength(100)]
        public string MorningRoutine { get; set; }

        public string CompatibilityTraits { get; set; }

        public bool IsProfileComplete { get; set; }
        public int CompletedStep { get; set; }
        public DateTime? ProfileCompletedAt { get; set; }
        public DateTime? BoardingProfileGeneratedAt { get; set; }

        public int CalculateAge()
        {
            if (Student == null || Student.DOB == default(DateTime))
                return 0;

            var today = DateTime.Today;
            var age = today.Year - Student.DOB.Year;
            if (Student.DOB.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}
