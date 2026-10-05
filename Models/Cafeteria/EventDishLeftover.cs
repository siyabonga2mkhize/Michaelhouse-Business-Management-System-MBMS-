using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC19 step 10 — what the kitchen recorded as left over, per
    // dish, after a feast. These records are one of the two inputs
    // to the Menu Fatigue Score (the other is meal collections),
    // and they drive the waste optimisation in step 7.
    //
    // IsDemo marks sample rows created by the Feast Plan "demo
    // data" action so they can be removed again.
    // ============================================================
    public class EventDishLeftover
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("FeastPlan")]
        public int? FeastPlanId { get; set; }

        public virtual EventFeastPlan FeastPlan { get; set; }

        [Required]
        [ForeignKey("MenuItem")]
        public int MenuItemId { get; set; }

        public virtual MenuItem MenuItem { get; set; }

        [Required]
        [StringLength(200)]
        public string DishName { get; set; }

        // The day the dish was served
        public DateTime EventDate { get; set; }

        [Range(0, 1000000)]
        public int PreparedPortions { get; set; }

        [Range(0, 1000000)]
        public int LeftoverPortions { get; set; }

        public int RecordedByUserId { get; set; }

        public DateTime RecordedAt { get; set; }

        public bool IsDemo { get; set; }

        [NotMapped]
        public decimal LeftoverFraction
        {
            get
            {
                if (PreparedPortions <= 0) return 0m;
                return Math.Min(1m, (decimal)LeftoverPortions / PreparedPortions);
            }
        }

        public EventDishLeftover()
        {
            RecordedAt = DateTime.UtcNow;
        }
    }
}
