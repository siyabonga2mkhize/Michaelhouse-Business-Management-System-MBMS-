using Michaelhouse.Models;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Policy;

namespace Michaelhouse.Services
{
    public class ResidenceAvailabilityService
    {
        private DBContextClass db;

        public ResidenceAvailabilityService()
        {
            db = new DBContextClass();
        }

        public ResidenceAvailabilityService(DBContextClass context)
        {
            db = context ?? new DBContextClass();
        }

        // All rooms with at least one free bed, across all residences,
        // with their occupants loaded (scoring needs occupant profiles).
        public List<(Room Room, Residence Residence, List<Student> Occupants)> GetAvailableRoomsWithOccupants()
        {
            var rooms = db.Rooms
                .Include(r => r.Residence)
                .Where(r => r.OccupiedBeds < r.Capacity && !r.IsFull)
                .ToList();

            var results = new List<(Room, Residence, List<Student>)>();

            foreach (var room in rooms)
            {
                var occupants = db.ResidenceAssignments
                    .Include(a => a.Student)
                    .Where(a => a.RoomId == room.RoomId && a.IsActive)
                    .Select(a => a.Student)
                    .ToList();

                results.Add((room, room.Residence, occupants));
            }

            return results;
        }

        public Bed GetFirstAvailableBed(int roomId)
        {
            return db.Beds
                .Where(b => b.RoomId == roomId &&
                            !b.IsOccupied &&
                            (b.Status == null || b.Status == "Available"))
                .OrderBy(b => b.BedNumber)
                .FirstOrDefault();
        }
    }
}
