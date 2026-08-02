using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class ResidenceMovement
    {
        [Key]
        public int ResidenceMovementId { get; set; }

        public int StudentId { get; set; }

        public int? FromResidenceId { get; set; }
        public int? FromRoomId { get; set; }

        public int? ToResidenceId { get; set; }
        public int? ToRoomId { get; set; }

        public int? PerformedByUserId { get; set; }
        public string PerformedByName { get; set; }

        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string Reason { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("FromResidenceId")]
        public virtual Residence FromResidence { get; set; }

        [ForeignKey("ToResidenceId")]
        public virtual Residence ToResidence { get; set; }

        [ForeignKey("FromRoomId")]
        public virtual Room FromRoom { get; set; }

        [ForeignKey("ToRoomId")]
        public virtual Room ToRoom { get; set; }
    }
}
