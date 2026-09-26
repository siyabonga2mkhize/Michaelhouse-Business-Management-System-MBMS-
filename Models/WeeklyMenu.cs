using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    public class WeeklyMenu
    {
        [Key]
        public int WeeklyMenuID { get; set; }

        [Required, StringLength(30)]
        public string WeekNumber { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        // ─── NEW RELATIONAL STRUCTURE ─────────────────────────────────
        public virtual ICollection<MenuDay> Days { get; set; } = new List<MenuDay>();
        public virtual ICollection<MenuValidationIssue> ValidationIssues { get; set; } = new List<MenuValidationIssue>();

        // ─── WORKFLOW ─────────────────────────────────────────────────
        [StringLength(40)]
        public string Status { get; set; } = "Draft";

        [NotMapped]
        public WeeklyMenuStatus StatusEnum
        {
            get
            {
                WeeklyMenuStatus parsed;
                return Enum.TryParse(Status, true, out parsed) ? parsed : WeeklyMenuStatus.Draft;
            }
            set { Status = value.ToString(); }
        }

        [StringLength(2000)] public string ChefComments { get; set; }
        [StringLength(120)] public string ChefReviewedBy { get; set; }
        public DateTime? ChefReviewedAt { get; set; }

        public int RevisionNumber { get; set; } = 1;
        public int? ParentMenuId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        [StringLength(120)] public string CreatedBy { get; set; }
        public int? CreatedByUserId { get; set; }

        public DateTime? PublishedDate { get; set; }
        [StringLength(120)] public string PublishedBy { get; set; }
        public int? PublishedByUserId { get; set; }

        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        // ─── LEGACY FLAT COLUMNS ─────────────────────────────────────
        public string MondayBreakfast { get; set; }
        public string MondayLunch { get; set; }
        public string MondayDinner { get; set; }
        public string TuesdayBreakfast { get; set; }
        public string TuesdayLunch { get; set; }
        public string TuesdayDinner { get; set; }
        public string WednesdayBreakfast { get; set; }
        public string WednesdayLunch { get; set; }
        public string WednesdayDinner { get; set; }
        public string ThursdayBreakfast { get; set; }
        public string ThursdayLunch { get; set; }
        public string ThursdayDinner { get; set; }
        public string FridayBreakfast { get; set; }
        public string FridayLunch { get; set; }
        public string FridayDinner { get; set; }
        public string SaturdayBreakfast { get; set; }
        public string SaturdayLunch { get; set; }
        public string SaturdayDinner { get; set; }
        public string SundayBreakfast { get; set; }
        public string SundayLunch { get; set; }
        public string SundayDinner { get; set; }
    }
}