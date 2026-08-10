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
                .Where(r => !r.IsArchived &&
                            !r.NeedsMaintenance &&
                            r.OccupiedBeds < r.Capacity &&
                            !r.IsFull &&
                            !r.Residence.IsArchived)
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

        public List<(Bed Bed, Room Room, Residence Residence, List<Student> Occupants)> GetAvailableBedsWithOccupants()
        {
            var beds = db.Beds
                .Include(b => b.Room.Residence)
                .Where(b => !b.IsOccupied &&
                            (b.Status == null || b.Status == "Available") &&
                            !b.Room.IsArchived &&
                            !b.Room.NeedsMaintenance &&
                            b.Room.OccupiedBeds < b.Room.Capacity &&
                            !b.Room.IsFull &&
                            !b.Room.Residence.IsArchived &&
                            b.Room.Residence.OccupiedBeds < b.Room.Residence.Capacity)
                .OrderByDescending(b => b.Room.Residence.Capacity - b.Room.Residence.OccupiedBeds)
                .ThenByDescending(b => b.Room.Capacity - b.Room.OccupiedBeds)
                .ThenBy(b => b.Room.Residence.Name)
                .ThenBy(b => b.Room.RoomNumber)
                .ThenBy(b => b.BedNumber)
                .ToList();

            var results = new List<(Bed, Room, Residence, List<Student>)>();

            foreach (var bed in beds)
            {
                var occupants = db.ResidenceAssignments
                    .Include(a => a.Student)
                    .Where(a => a.RoomId == bed.RoomId && a.IsActive)
                    .Select(a => a.Student)
                    .ToList();

                results.Add((bed, bed.Room, bed.Room.Residence, occupants));
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
