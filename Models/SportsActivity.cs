using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Michaelhouse.Models.Enums;

namespace Michaelhouse.Models
{
    public class SportsActivity
    {
        [Key]
        public int Id { get; set; }

        public int TeamId { get; set; }
        [ForeignKey("TeamId")] public virtual Team Team { get; set; }

        public int? CoachId { get; set; }
        [ForeignKey("CoachId")] public virtual Coach Coach { get; set; }

        public SportsActivityType ActivityType { get; set; }

        [Required, StringLength(150)] public string Title { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        [StringLength(150)] public string Location { get; set; }
        [StringLength(150)] public string Opponent { get; set; }
        public bool IsAway { get; set; }
        [StringLength(2000)] public string Notes { get; set; }

        public bool MealRequirement { get; set; }
        public bool SnackRequirement { get; set; }
        public bool HydrationRequirement { get; set; }
        [StringLength(500)] public string SpecialTimingNote { get; set; }

        public bool IsLateRequirement { get; set; }
        public bool IsCancelled { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedByUserId { get; set; }
    }

    public class CoachMessage
    {
        [Key]
        public int Id { get; set; }

        public int TeamId { get; set; }
        [ForeignKey("TeamId")] public virtual Team Team { get; set; }

        public int? CoachId { get; set; }
        [ForeignKey("CoachId")] public virtual Coach Coach { get; set; }

        [Required, StringLength(150)] public string Subject { get; set; }
        [Required, StringLength(2000)] public string Body { get; set; }
        public bool IsUrgent { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
    }

    public class CoachRequirement
    {
        [Key]
        public int Id { get; set; }

        public int TeamId { get; set; }
        [ForeignKey("TeamId")] public virtual Team Team { get; set; }

        public int? CoachId { get; set; }
        [ForeignKey("CoachId")] public virtual Coach Coach { get; set; }

        public int? SportsActivityId { get; set; }
        [ForeignKey("SportsActivityId")] public virtual SportsActivity SportsActivity { get; set; }

        public DateTime WeekStartDate { get; set; }

        public DateTime? MealDateTime { get; set; }

        public int? MealSlotId { get; set; }
        [ForeignKey("MealSlotId")] public virtual MealSlot MealSlot { get; set; }

        public MealServiceType ServiceType { get; set; } = MealServiceType.Normal;

        public int PlayerCount { get; set; }

        [Required, StringLength(1000)] public string RequirementText { get; set; }
        [StringLength(2000)] public string CafeteriaResponse { get; set; }

        public CoachRequirementStatus Status { get; set; } = CoachRequirementStatus.Submitted;
        public bool IsLate { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        public int? RespondedByUserId { get; set; }
        public DateTime? RespondedAt { get; set; }
    }
}