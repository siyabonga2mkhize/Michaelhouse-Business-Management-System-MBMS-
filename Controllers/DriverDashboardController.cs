using Michaelhouse.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class DriverDashboardController : Controller
    {
        private DBContextClass db = new DBContextClass();

        public ActionResult Index()
        {
            // 🔐 SESSION CHECK FIRST
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // 🔐 ROLE CHECK (VERY IMPORTANT)
            if (Session["UserRole"]?.ToString() != "Driver")
            {
                return new HttpStatusCodeResult(403); // Forbidden
            }

            int userId = (int)Session["UserId"];

            // ✅ FIX: use Drivers table, NOT DriverApplications
            var driver = db.Drivers.FirstOrDefault(d => d.UserId == userId);

            if (driver == null)
            {
                return Content("Driver profile not found. Please contact admin.");
            }

            return View(driver);
        }
    }
}