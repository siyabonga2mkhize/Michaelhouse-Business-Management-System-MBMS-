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

        // Food allergies. Comma-separated. Written as free text by the
        // parent application wizard and as a normalised list by the
        // student's dietary profile (see DietaryProfileService).
        public string Allergies { get; set; }
        public string Disabilities { get; set; }
        public string MedicalConditions { get; set; }
        public string EmergencyMedication { get; set; }
        public string MobilityRequirements { get; set; }

        public bool AccessibilityRequired { get; set; }
        public string AccessibilityNotes { get; set; }

        public bool MedicalAccommodationRequired { get; set; }
        public string MedicalAccommodationNotes { get; set; }

        // ── Dietary profile (cafeteria) ──────────────────────────
        // Food allergies live in Allergies above. These fields hold
        // the rest of what affects what the student can safely eat.
        // Codes are defined in DietaryProfileService.

        // "None", "Vegetarian", "Vegan", "Halal" or "Other"
        [StringLength(30)]
        public string DietaryPreference { get; set; }

        [StringLength(200)]
        public string DietaryPreferenceOther { get; set; }

        // Comma-separated codes, e.g. "LactoseIntolerance,Coeliac"
        [StringLength(500)]
        public string MedicalDietaryRestrictions { get; set; }

        [StringLength(200)]
        public string MedicalDietaryRestrictionOther { get; set; }

        [StringLength(1000)]
        public string DietaryNotes { get; set; }

        public DateTime? DietaryProfileUpdatedAt { get; set; }

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
        [StringLength(200)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(500)]
        public string ProfilePhotoUrl { get; set; }

        /// <summary>
        /// General active/inactive flag for the student (e.g. withdrawn, on extended leave).
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// One of: "Inside Residence", "Outside Residence", "On Holiday", "Weekend Leave",
        /// "Suspended", or "Awaiting Allocation" (used for waiting-list demo students).
        /// </summary>
        [StringLength(30)]
        public string BoardingStatus { get; set; }

        public DateTime? LastCheckIn { get; set; }
        public DateTime? LastCheckOut { get; set; }

        public DateTime? HolidayDepartureDate { get; set; }
        public DateTime? HolidayExpectedReturnDate { get; set; }

        [StringLength(500)]
        public string SuspensionReason { get; set; }
        public DateTime? SuspensionDate { get; set; }
        public DateTime? SuspensionEndDate { get; set; }
    }
}
