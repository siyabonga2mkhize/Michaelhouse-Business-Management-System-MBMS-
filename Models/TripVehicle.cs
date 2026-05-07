using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models
{
    public class TripVehicle
    {
        public int Id { get; set; }

        public int TripId { get; set; }
        public int VehicleId { get; set; }
        public int DriverId { get; set; }

        public virtual Trip Trip { get; set; }
        public virtual Vehicle Vehicle { get; set; }
        public virtual Driver Driver { get; set; }
    }
}