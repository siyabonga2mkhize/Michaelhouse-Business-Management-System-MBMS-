using Michaelhouse.Models;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class ScanController : Controller
    {
        private DBContextClass db = new DBContextClass();

        // GET: /Scan/Index (shows the scanner)
        // GET: /Scan/Index/MH-ASSET-0001 (redirects to report fault)
        public ActionResult Index(string id)
        {
            // If no asset code, show the scanner view
            if (string.IsNullOrEmpty(id))
            {
                return View("Scanner");
            }

            // Look up the asset
            var asset = db.Assets.FirstOrDefault(a => a.QrCode == id);
            if (asset == null)
                return HttpNotFound();

            // Load job history
            var jobHistory = db.JobCards
                .Include(j => j.AssignedTo)
                .Where(j => j.AssetId == asset.Id)
                .OrderByDescending(j => j.DateCreated)
                .Take(10)
                .ToList();

            ViewBag.JobHistory = jobHistory;
            ViewBag.IsLoggedIn = Session["UserId"] != null;
            ViewBag.UserRole = Session["UserRole"]?.ToString() ?? "";

            // Always show the asset details view – the action buttons inside it
            // will redirect to the appropriate controller/action based on role.
            return View("Index", asset);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}