using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.Cafeteria
{
    public enum MenuStatus
    {
        Draft = 1,
        PendingReview = 2,
        Accepted = 3,
        Rejected = 4
    }

    public class MealMenu
    {
        public MealMenu()
        {
            ScheduleItems = new HashSet<MenuScheduleItem>();
            CreatedDate = DateTime.UtcNow;
            MenuStatus = MenuStatus.Draft;
        }

        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(0, 100000)]
        public int StaffMeals { get; set; }

        [StringLength(2000)]
        public string SpecialEventNotes { get; set; }

        [Required]
        public MenuStatus MenuStatus { get; set; }

        [StringLength(2000)]
        public string RejectionReason { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        public int? RegeneratedFromMenuId { get; set; }

        public virtual MealMenu RegeneratedFromMenu { get; set; }

        public virtual ICollection<MenuScheduleItem> ScheduleItems { get; set; }
    }
}