using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

    namespace Michaelhouse.Models.ViewModels
    {
        public class DriverTripVM
        {
            public int TripId { get; set; }
            public string Destination { get; set; }

            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            public string VehicleNumber { get; set; }
            public int Capacity { get; set; }

            public List<string> Students { get; set; }
        }
    }
