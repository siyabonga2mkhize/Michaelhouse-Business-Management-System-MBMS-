using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class CampusRule
    {
        [Key]
        public int CampusRuleId { get; set; }

        [Required]
        [StringLength(100)]
        public string RuleName { get; set; } // e.g., "Evening Prep", "House Curfew"

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public bool IsActive { get; set; } = true;

        // Indicates if violating this rule auto-blocks submission or just flags it for Housemaster review
        public bool IsStrictBlock { get; set; } = false;
    }
}