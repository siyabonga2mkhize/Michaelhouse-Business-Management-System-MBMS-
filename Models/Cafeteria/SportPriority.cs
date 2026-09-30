using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC12 rework — Weekly priority flags set by the Coach.
    //
    // Example: "Cricket has finals this week — prioritise nutrition".
    // The generator applies a stronger tilt for that sport all week.
    // ============================================================

    public class SportPriority
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Sport { get; set; }

        [Required]
        public DateTime WeekStartDate { get; set; }

        [Required]
        public DateTime WeekEndDate { get; set; }

        /// <summary>
        /// 1 = Normal, 2 = High priority.
        /// </summary>
        [Range(1, 2)]
        public int PriorityLevel { get; set; }

        [StringLength(300)]
        public string Reason { get; set; }

        public int SetByUserId { get; set; }

        public DateTime SetAt { get; set; }

        public SportPriority()
        {
            PriorityLevel = 2;
            SetAt = DateTime.UtcNow;
        }
    }
}