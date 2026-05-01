using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class Trip
    {
        public int Id { get; set; }
        [Required]
        public string Destination { get; set; }
        [Required]
        public DateTime TripDate { get; set; }

        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }

        public string Status { get; set; } // Scheduled / Completed / Cancelled
    }
}