using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class QRScanRecord
    {
        [Key]
        public int QRScanRecordId { get; set; }

        [NotMapped]
        public int ResidenceLogId
        {
            get { return QRScanRecordId; }
            set { QRScanRecordId = value; }
        }

        public int StudentId { get; set; }

        public int? ResidenceId { get; set; }

        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

        [StringLength(40)]
        public string Action { get; set; }

        public int? HouseMasterId { get; set; }

        [MaxLength(200)]
        public string Scanner { get; set; } // device or user performing scan

        public bool Approved { get; set; }

        [MaxLength(1000)]
        public string Reason { get; set; }

        [NotMapped]
        public string Notes
        {
            get { return Reason; }
            set { Reason = value; }
        }

        public bool IsHoliday { get; set; }
        public bool OnTrip { get; set; }
        public bool MedicalRestrictionViolated { get; set; }
        public bool IsArchived { get; set; }

        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [ForeignKey("ResidenceId")]
        public virtual Residence Residence { get; set; }

        [ForeignKey("HouseMasterId")]
        public virtual HouseMaster HouseMaster { get; set; }
    }
}
