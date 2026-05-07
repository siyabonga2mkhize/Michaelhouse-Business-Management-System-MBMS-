using Michaelhouse.Models;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class NotificationController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // This partial view is called by the layout to render the notification dropdown
        public ActionResult GetNotifications()
        {
            int userId = (int)(Session["UserId"] ?? 0);
            var notifications = db.Notifications
                                  .Where(n => n.UserId == userId && !n.IsRead)
                                  .OrderByDescending(n => n.CreatedAt)
                                  .Take(10)
                                  .ToList();
            ViewBag.UnreadCount = notifications.Count;
            return PartialView("_Notifications", notifications);
        }

        // Mark a single notification as read (AJAX call)
        [HttpPost]
        public JsonResult MarkAsRead(int id)
        {
            var notif = db.Notifications.Find(id);
            if (notif != null)
            {
                notif.IsRead = true;
                db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // Full notification history page
        public ActionResult Index()
        {
            int userId = (int)(Session["UserId"] ?? 0);
            var all = db.Notifications
                        .Where(n => n.UserId == userId)
                        .OrderByDescending(n => n.CreatedAt)
                        .ToList();
            return View(all);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}