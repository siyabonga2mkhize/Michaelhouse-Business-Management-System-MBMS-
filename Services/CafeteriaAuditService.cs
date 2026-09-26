using System;
using System.Web;
using Michaelhouse.Models;

namespace Michaelhouse.Services
{
    /// <summary>
    /// Writes records to the CafeteriaAuditLog table.
    /// Called from controllers after state-changing operations.
    /// </summary>
    public class CafeteriaAuditService
    {
        private readonly DBContextClass _db;

        public CafeteriaAuditService(DBContextClass db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public CafeteriaAuditService() : this(new DBContextClass()) { }

        // ─────────────────────────────────────────────────────────────
        // Main entry point
        // ─────────────────────────────────────────────────────────────

        public void Log(string action, string entityName = null, int? entityId = null,
                        string previousValue = null, string newValue = null,
                        string reason = null)
        {
            int? userId = null;
            string userName = "System";
            string ip = null;

            try
            {
                var ctx = HttpContext.Current;
                if (ctx != null)
                {
                    if (ctx.Session != null && ctx.Session["UserId"] != null)
                    {
                        int parsed;
                        if (int.TryParse(ctx.Session["UserId"].ToString(), out parsed))
                            userId = parsed;
                    }
                    if (ctx.Session != null && ctx.Session["UserName"] != null)
                        userName = ctx.Session["UserName"].ToString();
                    if (ctx.Request != null && ctx.Request.UserHostAddress != null)
                        ip = ctx.Request.UserHostAddress;
                }
            }
            catch { /* audit never throws */ }

            var log = new CafeteriaAuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = Trim(action, 120),
                EntityName = Trim(entityName, 120),
                EntityId = entityId,
                PreviousValue = Trim(previousValue, 2000),
                NewValue = Trim(newValue, 2000),
                Reason = Trim(reason, 500),
                Timestamp = DateTime.Now,
                IPAddress = Trim(ip, 60)
            };

            try
            {
                _db.CafeteriaAuditLogs.Add(log);
                _db.SaveChanges();
            }
            catch
            {
                // Logging must never break the workflow.
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Named helpers — used from controllers for consistency
        // ─────────────────────────────────────────────────────────────

        public void LogMenuPublished(int weeklyMenuId, string weekNumber)
            => Log("MenuPublished", "WeeklyMenu", weeklyMenuId, null, weekNumber);

        public void LogMenuRevised(int weeklyMenuId, int revisionNumber)
            => Log("MenuRevised", "WeeklyMenu", weeklyMenuId, null, "Revision " + revisionNumber);

        public void LogMealServed(int mealServiceId, int studentId, int menuMealId, bool isOverride, string overrideReason)
            => Log(isOverride ? "MealServedOverride" : "MealServed",
                   "MealService", mealServiceId,
                   null, "StudentId=" + studentId + ", MenuMealId=" + menuMealId,
                   overrideReason);

        public void LogDietaryApproved(int dietaryRecordId, int studentId, string recordType, string description)
            => Log("DietaryApproved", "StudentDietaryRecord", dietaryRecordId,
                   null, recordType + ": " + description, "StudentId=" + studentId);

        public void LogDietaryChangeRequest(int requestId, int studentId, string requestType)
            => Log("DietaryChangeRequested", "DietaryChangeRequest", requestId,
                   null, requestType, "StudentId=" + studentId);

        public void LogStockAdjustment(int inventoryItemId, decimal oldValue, decimal newValue, string reason)
            => Log("StockAdjusted", "InventoryItem", inventoryItemId,
                   oldValue.ToString("0.##"), newValue.ToString("0.##"), reason);

        public void LogCoachRequirement(int requirementId, int teamId, string action)
            => Log(action, "CoachRequirement", requirementId, null, "TeamId=" + teamId);

        public void LogTeamAssignmentChanged(int membershipId, int studentId, int teamId, string oldStatus, string newStatus)
            => Log("TeamAssignmentChanged", "TeamMembership", membershipId,
                   oldStatus, newStatus, "StudentId=" + studentId + ", TeamId=" + teamId);

        // ─────────────────────────────────────────────────────────────
        // Utility
        // ─────────────────────────────────────────────────────────────

        private static string Trim(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }
    }
}