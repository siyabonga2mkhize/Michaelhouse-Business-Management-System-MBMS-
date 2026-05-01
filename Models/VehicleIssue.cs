using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class VehicleIssue
    {
        public int Id { get; set; }

        public int DriverId { get; set; }
        public int VehicleId { get; set; }

        public string Description { get; set; }

        public DateTime DateReported { get; set; }

        public string Status { get; set; } // Open / Resolved
    }
}