using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MIEMS.Models
{
	public class Vehicle
	{
        public int VehicleId { get; set; }

        [Required, Display(Name = "Registration Number")]
        public string RegistrationNumber { get; set; }

        [Display(Name = "Vehicle Type")]
        public string VehicleType { get; set; } // Bus, Minibus, Car

        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Driver Name")]
        public string DriverName { get; set; }

        [Display(Name = "Driver License")]
        public string DriverLicense { get; set; }

        [Display(Name = "Route")]
        public string Route { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Active, Maintenance, Inactive
    }
}