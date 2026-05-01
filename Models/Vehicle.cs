using System;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class Vehicle
    {
        public int Id { get; set; }

        [Required]
        public string VehicleNumber { get; set; }   // e.g. plate number
        [Required]
        public string Model { get; set; }
        [Required]
        public string Type { get; set; }             // Bus / Van / Car
        [Required]
        public int Capacity { get; set; }

        public bool IsActive { get; set; }           // ACTIVE vs ARCHIVED

        public DateTime DateAdded { get; set; }
    }
}