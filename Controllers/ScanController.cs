using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using Michaelhouse.Models;

namespace Michaelhouse.Controllers
{
    /// <summary>
    /// Public-facing controller that handles QR code scans.
    /// No login required to VIEW an asset — but the quick action buttons
    /// change depending on whether someone is logged in and what role they have.
    /// This is what a phone camera opens when it scans an asset's QR code.
    /// </summary>
    public class ScanController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // GET: /Scan/Index/MH-ASSET-0001
        // The "id" here is actually the asset's QrCode string, not a numeric Id.
        public ActionResult Index(string id)
        {
            if (string.IsNullOrEmpty(id))
                return HttpNotFound();

            var asset = db.Assets.FirstOrDefault(a => a.QrCode == id);
            if (asset == null)
                return HttpNotFound();

            // Recent job/fault history for this asset
            var jobHistory = db.JobCards
                .Include(j => j.AssignedTo)
                .Where(j => j.AssetId == asset.Id)
                .OrderByDescending(j => j.DateCreated)
                .Take(10)
                .ToList();

            ViewBag.JobHistory = jobHistory;

            // Who is looking at this? Determines which buttons to show.
            ViewBag.IsLoggedIn = Session["UserId"] != null;
            ViewBag.UserRole = Session["UserRole"] != null
                ? Session["UserRole"].ToString() : "";

            return View(asset);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}