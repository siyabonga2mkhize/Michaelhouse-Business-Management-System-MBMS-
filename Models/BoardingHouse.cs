using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Michaelhouse.Models
{
    public class BoardingHouse
    {
        public int BoardingHouseId { get; set; }

        [Required, Display(Name = "House Name")]
        [StringLength(100)]
        public string HouseName { get; set; }

        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Current Occupancy")]
        public int CurrentOccupancy { get; set; }

        [Display(Name = "House Master")]
        public string HouseMaster { get; set; }

        [Display(Name = "Available Spaces")]
        public int AvailableSpaces => Capacity - CurrentOccupancy;

        public virtual ICollection<Student> Students { get; set; }
        public virtual ICollection<Room> Rooms { get; set; }
    }

    public class Room
    {
        public int RoomId { get; set; }

        [Display(Name = "Room Number")]
        public string RoomNumber { get; set; }

        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        [Display(Name = "Floor")]
        public int Floor { get; set; }

        public int BoardingHouseId { get; set; }
        public virtual BoardingHouse BoardingHouse { get; set; }
    }
}