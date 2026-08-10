using System;
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

        public string AcademicStream { get; set; }
    }

    public class StudentProfileActivitiesViewModel : StudentProfileWizardBase
    {
        public string Sports { get; set; }
        public string Clubs { get; set; }

        [Display(Name = "Leadership Roles")]
        public string LeadershipRoles { get; set; }

        [Display(Name = "Cultural Activities")]
        public string CulturalActivities { get; set; }
    }

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
