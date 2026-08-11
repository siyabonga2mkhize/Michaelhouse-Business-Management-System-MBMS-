using System;
using System.Linq;
using System.Web.Mvc;
using Michaelhouse.Models;

namespace Michaelhouse.Controllers
{
    [Authorize(Roles = "Admin,HouseMaster,Housemaster")]
    public class VisitorAnalyticsController : Controller
    {
        private readonly DBContextClass db = new DBContextClass();

        // ============================================================
        // 📊 ANALYTICS DASHBOARD
        // ============================================================
        public ActionResult Index(string period = "monthly", int? year = null, int? month = null, int? day = null)
        {
            IQueryable<VisitorScanLog> query = db.VisitorScanLogs.Include("Request");

            DateTime startDate, endDate;
            if (period == "daily" && day.HasValue && month.HasValue && year.HasValue)
            {
                startDate = new DateTime(year.Value, month.Value, day.Value);
                endDate = startDate.AddDays(1);
            }
            else if (period == "weekly" && day.HasValue && month.HasValue && year.HasValue)
            {
                var d = new DateTime(year.Value, month.Value, day.Value);
                startDate = d.AddDays(-(int)d.DayOfWeek);
                endDate = startDate.AddDays(7);
            }
            else if (period == "yearly" && year.HasValue)
            {
                startDate = new DateTime(year.Value, 1, 1);
                endDate = startDate.AddYears(1);
            }
            else
            {
                if (!year.HasValue) year = DateTime.Now.Year;
                if (!month.HasValue) month = DateTime.Now.Month;
                startDate = new DateTime(year.Value, month.Value, 1);
                endDate = startDate.AddMonths(1);
            }

            query = query.Where(l => l.ScannedAt >= startDate && l.ScannedAt < endDate);
            var logs = query.OrderByDescending(l => l.ScannedAt).ToList();

            ViewBag.Total = logs.Count;
            ViewBag.Granted = logs.Count(l => l.Status == "Granted");
            ViewBag.Denied = logs.Count(l => l.Status == "Denied");
            ViewBag.Period = period;
            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.Day = day;

            return View(logs);
        }

        // ============================================================
        // 👥 ACTIVE VISITORS (fixed .Date issue)
        // ============================================================
        public ActionResult ActiveVisitors()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var activeLogs = db.VisitorScanLogs
                .Include("Request")
                .Where(l => l.IsEntry == true && l.Status == "Granted" && l.ScannedAt >= today && l.ScannedAt < tomorrow)
                .ToList();

            var active = activeLogs
                .Where(entry => !db.VisitorScanLogs.Any(exit =>
                    exit.RequestId == entry.RequestId &&
                    exit.IsEntry == false &&
                    exit.Status == "Exit" &&
                    exit.ScannedAt >= today &&
                    exit.ScannedAt < tomorrow))
                .OrderBy(l => l.ScannedAt)
                .ToList();

            return View(active);
        }

        // ============================================================
        // 🚪 MANUAL CHECKOUT
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ManualCheckout(int requestId)
        {
            var request = db.VisitorAccessRequests.Find(requestId);
            if (request == null) return HttpNotFound();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var alreadyExited = db.VisitorScanLogs.Any(l =>
                l.RequestId == requestId &&
                l.IsEntry == false &&
                l.Status == "Exit" &&
                l.ScannedAt >= today &&
                l.ScannedAt < tomorrow);

            if (alreadyExited)
            {
                TempData["Error"] = "This visitor has already been checked out today.";
                return RedirectToAction("ActiveVisitors");
            }

            var log = new VisitorScanLog
            {
                RequestId = requestId,
                ScannedAt = DateTime.Now,
                ScannerUserId = (int?)Session["UserId"],
                ScannerName = Session["UserName"]?.ToString() ?? "Manual Checkout",
                Status = "Exit",
                Reason = "Manual checkout from dashboard",
                Location = "Admin Dashboard",
                IsEntry = false
            };
            db.VisitorScanLogs.Add(log);
            db.SaveChanges();

            TempData["Success"] = $"{request.VisitorFullName} has been checked out.";
            return RedirectToAction("ActiveVisitors");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}