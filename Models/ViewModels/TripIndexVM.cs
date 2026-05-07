using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Michaelhouse.Models.ViewModels
{
    public class TripIndexVM
        {
            public int Id { get; set; }
            public string Destination { get; set; }
            public int PassengerCount { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string DriverName { get; set; }
            public string Vehicles { get; set; }
            public string Status { get; set; }
    }
    

}