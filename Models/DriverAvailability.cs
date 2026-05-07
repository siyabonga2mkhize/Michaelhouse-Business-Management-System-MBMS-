using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class DriverAvailability
    {
        public int Id { get; set; }

        public int DriverId { get; set; }
        public virtual Driver Driver { get; set; }
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
        [Required]
        public string Reason { get; set; }

        public DateTime? DateCreated { get; set; }
    }
}