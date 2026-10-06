using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class StudentProfileWizardBase
    {
        public int AppId { get; set; }
        public int StudentId { get; set; }
        public int Step { get; set; }
        public string StudentName { get; set; }
    }

    // ============================================================
    // STEP 1 - BASIC
    // ============================================================

    public class StudentProfileBasicViewModel : StudentProfileWizardBase
    {
        [StringLength(100)]
        [Display(Name = "Preferred Name")]
        public string PreferredName { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(50)]
        public string Gender { get; set; }

        [StringLength(100)]
        [Display(Name = "Home Language")]
        public string HomeLanguage { get; set; }

        [StringLength(100)]
        public string Nationality { get; set; }
    }


    // ============================================================
    // STEP 2 - ACADEMIC
    // ============================================================

    public class StudentProfileAcademicViewModel : StudentProfileWizardBase
    {
        [Required]
        [Display(Name = "Grade/Form")]
        public string Grade { get; set; }

        public string Subjects { get; set; }

        [Display(Name = "Academic Interests")]
        public string AcademicInterests { get; set; }

        [Required]
        [Display(Name = "Learning Style")]
        public string LearningStyle { get; set; }

        [Display(Name = "Academic Stream")]
        public string AcademicStream { get; set; }
    }


    // ============================================================
    // STEP 3 - ACTIVITIES
    // ============================================================

    public class StudentProfileActivitiesViewModel : StudentProfileWizardBase
    {
        [Display(Name = "Sport")]
        public string Sports { get; set; }

        // The sports ticked on the form; saved to StudentProfile.Sports
        // as "Rugby, Athletics"
        public List<string> SportsSelected { get; set; } = new List<string>();

        // Existing database value is still stored in StudentProfile.ClubsAndSocieties
        public string Clubs { get; set; }

        // Used only by the form for multiple checkbox selections.
        public List<string> ClubsSelected { get; set; } = new List<string>();

        [Display(Name = "Leadership Roles")]
        public string LeadershipRoles { get; set; }

        [Display(Name = "Cultural Activities")]
        public string CulturalActivities { get; set; }
    }


    // ============================================================
    // STEP 4 - MEDICAL
    // ============================================================

    public class StudentProfileMedicalViewModel : StudentProfileWizardBase
    {
        public string Allergies { get; set; }

        public string Disabilities { get; set; }

        [Display(Name = "Medical Conditions")]
        public string MedicalConditions { get; set; }

        [Display(Name = "Emergency Medication")]
        public string EmergencyMedication { get; set; }

        [Display(Name = "Mobility Requirements")]
        public string MobilityRequirements { get; set; }

        [Display(Name = "Accessibility Required")]
        public bool AccessibilityRequired { get; set; }

        [Display(Name = "Accessibility Notes")]
        public string AccessibilityNotes { get; set; }

        [Display(Name = "Medical Accommodation Required")]
        public bool MedicalAccommodationRequired { get; set; }

        [Display(Name = "Medical Accommodation Notes")]
        public string MedicalAccommodationNotes { get; set; }
    }


    // ============================================================
    // STEP 5 - PERSONALITY
    // ============================================================

    public class StudentProfilePersonalityViewModel : StudentProfileWizardBase
    {
        [Required]
        [Display(Name = "I prefer")]
        public string EnvironmentPreference { get; set; }

        [Required]
        [Display(Name = "I usually")]
        public string RoutinePreference { get; set; }

        [Required]
        [Display(Name = "I enjoy")]
        public string ActivityPreference { get; set; }

        [Required]
        [Display(Name = "I study best")]
        public string StudyPreference { get; set; }

        [Required]
        [Display(Name = "I describe myself as")]
        public string SocialPreference { get; set; }
    }
}