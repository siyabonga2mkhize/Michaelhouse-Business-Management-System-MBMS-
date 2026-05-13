using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Services
{
    public static class NotificationHelper
    {
        public static void Send(DBContextClass db, int userId, string message, string entityType, int entityId)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = message,
                RelatedEntityType = entityType,
                RelatedEntityId = entityId,
                CreatedAt = DateTime.Now,
                IsRead = false
            });
            db.SaveChanges();
        }

        public static void NotifyTeacher(DBContextClass db, int teacherId, string message)
        {
            var teacher = db.Teachers.Find(teacherId);
            if (teacher?.UserId != null)
                Send(db, teacher.UserId.Value, message, "Trip", 0);
        }

        public static void NotifyTransportManagers(DBContextClass db, string message)
        {
            var managers = db.Users.Where(u => u.Role == "TransportManager").ToList();
            foreach (var m in managers)
                Send(db, m.UserId, message, "Trip", 0);
        }

        public static void NotifyDriver(DBContextClass db, int driverId, string message)
        {
            var driver = db.Drivers.Find(driverId);
            if (driver?.UserId != null)
                Send(db, driver.UserId.Value, message, "Trip", 0);
        }

        public static void NotifyParents(DBContextClass db, List<int> studentIds, string message)
        {
            var parentIds = db.Students
                .Where(s => studentIds.Contains(s.StudentId))
                .Select(s => s.ParentId)
                .Distinct()
                .ToList();
            var users = db.Parents
                .Where(p => parentIds.Contains(p.ParentId) && p.UserId != null)
                .Select(p => p.UserId.Value)
                .ToList();
            foreach (var uid in users)
                Send(db, uid, message, "Trip", 0);
        }
    }
}