using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Michaelhouse.Models
{
    /// <summary>
    /// Represents a House Master who manages one or more residences.
    /// </summary>
    public class HouseMaster
    {
        [Key]
        public int HouseMasterId { get; set; }

        [Required]
        [StringLength(200)]
        public string FullName { get; set; }

        [NotMapped]
        public string Name
        {
            get { return FullName; }
            set { FullName = value; }
        }

        [StringLength(100)]
        public string ContactEmail { get; set; }

        [NotMapped]
        public string Email
        {
            get { return ContactEmail; }
            set { ContactEmail = value; }
        }

        [StringLength(20)]
        public string ContactPhone { get; set; }

        [NotMapped]
        public string Phone
        {
            get { return ContactPhone; }
            set { ContactPhone = value; }
        }

        public int? ResidenceId { get; set; }
        public bool IsArchived { get; set; }

        /// <summary>
        /// Residences managed by this house master
        /// </summary>
        public virtual ICollection<Residence> Residences { get; set; }
    }
}
