using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Services
{
    public class BoardingAccessService
    {
        public bool IsAdmin(Controller controller)
        {
            var role = controller.Session["UserRole"] as string;
            return role != null && role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public int? GetHouseMasterId(Controller controller)
        {
            var value = controller.Session["HouseMasterId"];
            if (value == null) return null;

            if (value is int id) return id;
            if (int.TryParse(value.ToString(), out id)) return id;
            return null;
        }

        public List<int> GetAccessibleResidenceIds(Controller controller, DBContextClass db)
        {
            if (IsAdmin(controller))
                return db.Residences.Select(r => r.ResidenceId).ToList();

            var houseMasterId = GetHouseMasterId(controller);
            if (!houseMasterId.HasValue)
                return new List<int>();

            return db.Residences
                .Where(r => !r.IsArchived &&
                            (r.HouseMasterId == houseMasterId.Value ||
                             db.HouseMasters.Any(h => h.HouseMasterId == houseMasterId.Value &&
                                                      h.ResidenceId == r.ResidenceId &&
                                                      !h.IsArchived)))
                .Select(r => r.ResidenceId)
                .Distinct()
                .ToList();
        }

        public bool CanAccessResidence(Controller controller, DBContextClass db, int residenceId)
        {
            return IsAdmin(controller) || GetAccessibleResidenceIds(controller, db).Contains(residenceId);
        }

        public bool CanAccessRoom(Controller controller, DBContextClass db, int roomId)
        {
            if (IsAdmin(controller)) return true;

            var residenceId = db.Rooms
                .Where(r => r.RoomId == roomId)
                .Select(r => (int?)r.ResidenceId)
                .FirstOrDefault();

            return residenceId.HasValue && CanAccessResidence(controller, db, residenceId.Value);
        }

        public bool CanAccessBed(Controller controller, DBContextClass db, int bedId)
        {
            if (IsAdmin(controller)) return true;

            var residenceId = db.Beds
                .Where(b => b.BedId == bedId)
                .Select(b => (int?)b.Room.ResidenceId)
                .FirstOrDefault();

            return residenceId.HasValue && CanAccessResidence(controller, db, residenceId.Value);
        }

        public bool CanAccessStudentCurrentResidence(Controller controller, DBContextClass db, int studentId)
        {
            if (IsAdmin(controller)) return true;

            var residenceId = db.ResidenceAssignments
                .Where(a => a.StudentId == studentId && a.IsActive && !a.IsArchived)
                .OrderByDescending(a => a.MoveInDate)
                .Select(a => (int?)a.ResidenceId)
                .FirstOrDefault();

            return residenceId.HasValue && CanAccessResidence(controller, db, residenceId.Value);
        }

        public IQueryable<ResidenceAssignment> ScopeAssignments(Controller controller, DBContextClass db, IQueryable<ResidenceAssignment> query)
        {
            if (IsAdmin(controller)) return query;
            var residenceIds = GetAccessibleResidenceIds(controller, db);
            return query.Where(a => residenceIds.Contains(a.ResidenceId));
        }
    }
}
