using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    public class StudentQRCode
    {
        [Key]
        public int QRCodeId { get; set; }

        [NotMapped]
        public int QRIdentityId
        {
            get { return QRCodeId; }
            set { QRCodeId = value; }
        }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public virtual Student Student { get; set; }

        [Required]
        [MaxLength(512)]
        public string QRCodeValue { get; set; }

        [NotMapped]
        public string QRCode
        {
            get { return QRCodeValue; }
            set { QRCodeValue = value; }
        }

        // PNG bytes of the generated QR image
        public byte[] QRImage { get; set; }

        public DateTime DateGenerated { get; set; }

        [NotMapped]
        public DateTime GeneratedDate
        {
            get { return DateGenerated; }
            set { DateGenerated = value; }
        }

        // Only one active code per student at a time.
        // Old codes are kept (not deleted) for audit history.
        public bool IsActive { get; set; }

        // Links a regenerated code back to the one it replaced
        public int? RegeneratedFromId { get; set; }
    }
}
