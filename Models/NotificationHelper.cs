using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Michaelhouse.Services
{
    public static class NotificationHelper
    {
        /// <summary>
        /// Core method to create a notification for a user.
        /// </summary>
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

        // ═══════════════════════════════════════════════════════════════════
        // CAFETERIA NOTIFICATIONS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Notify all students when a new weekly menu is published.
        /// </summary>
        public static void NotifyStudentsMenuPublished(DBContextClass db, WeeklyMenu menu)
        {
            var students = db.Students.Where(s => s.IsActive && s.UserId != null).ToList();

            string message = $"📖 Weekly Menu {menu.WeekNumber} has been published for " +
                             $"{menu.StartDate:dd MMM} – {menu.EndDate:dd MMM}. " +
                             $"Please create or update your meal plan.";

            foreach (var s in students)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = s.UserId.Value,
                    Message = message,
                    RelatedEntityType = "WeeklyMenu",
                    RelatedEntityId = menu.WeeklyMenuID,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            db.SaveChanges();
        }

        /// <summary>
        /// Notify the Chef when a menu is ready for their review.
        /// </summary>
        public static void NotifyChefMenuReview(DBContextClass db, WeeklyMenu menu, string mealCoordinatorName)
        {
            var chefs = db.Users.Where(u => u.Role == "Chef").ToList();

            string message = $"🍳 New draft menu {menu.WeekNumber} " +
                             $"({menu.StartDate:dd MMM} – {menu.EndDate:dd MMM}) submitted by " +
                             $"{mealCoordinatorName}. Please review and approve.";

            foreach (var c in chefs)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = c.UserId,
                    Message = message,
                    RelatedEntityType = "WeeklyMenu",
                    RelatedEntityId = menu.WeeklyMenuID,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            db.SaveChanges();
        }

        /// <summary>
        /// Notify the Meal Coordinator when Chef approves/changes/rejects the menu.
        /// </summary>
        public static void NotifyMealCoordinatorChefDecision(DBContextClass db, WeeklyMenu menu,
            string decision, string chefComments)
        {
            var coords = db.Users.Where(u => u.Role == "MealCoordinator").ToList();

            string prefix = decision == "Approved" ? "✅ Chef approved menu"
                          : decision == "ChangesRequested" ? "✏️ Chef requested changes to"
                          : "❌ Chef rejected menu";

            string message = $"{prefix} {menu.WeekNumber} ({menu.StartDate:dd MMM} – {menu.EndDate:dd MMM}).";
            if (!string.IsNullOrEmpty(chefComments))
                message += " Chef notes: " + chefComments;

            foreach (var c in coords)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = c.UserId,
                    Message = message,
                    RelatedEntityType = "WeeklyMenu",
                    RelatedEntityId = menu.WeeklyMenuID,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            db.SaveChanges();
        }

        /// <summary>
        /// Notify all players of a sport when their coach submits new requirements.
        /// </summary>
        public static void NotifyTeamPlayersCoachRequirements(DBContextClass db,
            string sport, string coachName, string recommendationText)
        {
            if (string.IsNullOrEmpty(sport)) return;

            var studentIds = db.StudentProfiles
                .Where(p => p.Sports != null && p.Sports.Contains(sport))
                .Select(p => p.StudentId)
                .ToList();

            if (!studentIds.Any()) return;

            var students = db.Students
                .Where(s => studentIds.Contains(s.StudentId) && s.UserId != null)
                .ToList();

            string shortRec = recommendationText.Length > 80
                ? recommendationText.Substring(0, 80) + "..."
                : recommendationText;

            string message = $"🏉 Coach {coachName} ({sport}) submitted new nutrition requirements: " +
                             $"\"{shortRec}\" — Check your meal plan for recommendations.";

            foreach (var s in students)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = s.UserId.Value,
                    Message = message,
                    RelatedEntityType = "CoachRecommendation",
                    RelatedEntityId = 0,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            db.SaveChanges();
        }

        /// <summary>
        /// Send a reminder to students who haven't selected their meal yet.
        /// </summary>
        public static void RemindStudentsToSelectMeal(DBContextClass db, string mealType,
            string cutoffTime)
        {
            var today = DateTime.Now.Date;

            var selectedIds = db.Selections
                .Where(s => s.Date == today && s.MealType == mealType)
                .Select(s => s.StudentID)
                .ToList();

            var studentsToNotify = db.Students
                .Where(s => s.IsActive && s.UserId != null && !selectedIds.Contains(s.StudentId))
                .ToList();

            string message = $"⏰ Reminder: {mealType} selection window closes at {cutoffTime}. " +
                             $"If you don't select a meal, your weekly plan default will be used.";

            foreach (var s in studentsToNotify)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = s.UserId.Value,
                    Message = message,
                    RelatedEntityType = "MealSelection",
                    RelatedEntityId = 0,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }
            db.SaveChanges();
        }

        public static async Task SendEmergencyAlertToAllStudentsAsync(EmergencyAlert alert, Student student)
        {
            await Task.Run(() =>
            {
                Console.WriteLine($"🚨 EMERGENCY ALERT to student {student.StudentId}: {alert.AlertMessage}");
            });
        }
    }
}