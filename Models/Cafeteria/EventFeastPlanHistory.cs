using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models.Cafeteria
{
    // ============================================================
    // UC19 — Audit trail of a feast plan: generated, edited,
    // submitted, approved / rejected / changes requested, sent.
    // ============================================================
    public class EventFeastPlanHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("FeastPlan")]
        public int FeastPlanId { get; set; }
        public virtual EventFeastPlan FeastPlan { get; set; }

        public int Revision { get; set; }

        // FeastPlanService.Action* constants
        [Required]
        [StringLength(30)]
        public string Action { get; set; }

        public int UserId { get; set; }

        [StringLength(200)]
        public string UserName { get; set; }

        [StringLength(50)]
        public string UserRole { get; set; }

        [StringLength(1000)]
        public string Comments { get; set; }

        public DateTime At { get; set; }

        public EventFeastPlanHistory()
        {
            At = DateTime.UtcNow;
        }
    }
}
