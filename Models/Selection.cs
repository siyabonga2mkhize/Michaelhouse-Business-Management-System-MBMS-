using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// This stores a student's DAILY meal selection.
    /// This is different from the weekly meal plan.
    /// The weekly plan is the default.
    /// The daily selection is what they actually want today.
    /// 
    /// The QR code is generated from this selection.
    /// The kitchen dashboard is updated from this selection.
    /// </summary>
    public class Selection
    {
        [Key]
        public int SelectionID { get; set; }

        [Required]
        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public virtual Student Student { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Meal Selected")]
        public string Meal { get; set; }

        [Required]
        [Display(Name = "Date")]
        public DateTime Date { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Meal Type")]
        public string MealType { get; set; }
        // MealType can be: Breakfast, Lunch, Dinner

        [Display(Name = "Selected On")]
        public DateTime SelectedOn { get; set; } = DateTime.Now;

        [Display(Name = "QR Code Value")]
        [MaxLength(500)]
        public string QRCodeValue { get; set; }

        [Display(Name = "Is Collected")]
        public bool IsCollected { get; set; } = false;

        [Display(Name = "Collected On")]
        public DateTime? CollectedOn { get; set; }

        [Display(Name = "Collected By")]
        public string CollectedBy { get; set; }
        // Name of the staff member who served the meal

        [Display(Name = "Verification Method")]
        public string VerificationMethod { get; set; }
        // VerificationMethod can be: QR Code, Facial Recognition
    }
}