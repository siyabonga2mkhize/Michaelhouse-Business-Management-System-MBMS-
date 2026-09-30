using System;
using System.Web.Mvc;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Health-check endpoint.
    // The mobile app calls GET /api/ping to verify the API is up
    // and reachable. Returns a small JSON blob with a timestamp.
    // ============================================================
    [RoutePrefix("api/ping")]
    public class PingApiController : Controller
    {
        [HttpGet]
        [Route("")]
        public JsonResult Index()
        {
            return Json(new
            {
                ok = true,
                message = "Michaelhouse API is running",
                serverTime = DateTime.UtcNow.ToString("o"),
                version = "1.0"
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [Route("echo/{value}")]
        public JsonResult Echo(string value)
        {
            return Json(new
            {
                ok = true,
                echo = value,
                length = value != null ? value.Length : 0
            }, JsonRequestBehavior.AllowGet);
        }
    }
}