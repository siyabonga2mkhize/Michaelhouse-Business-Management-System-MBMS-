using Michaelhouse.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse.Controllers
{
    /// <summary>
    /// Base controller that provides legacy Session compatibility and common helpers.
    /// All application controllers should inherit from this class instead of Controller.
    /// </summary>
    public abstract class BaseController : Controller
    {
        private HttpSessionAdapter? _session;

        /// <summary>
        /// Provides an object-indexer Session API compatible with the legacy System.Web session.
        /// </summary>
        protected new HttpSessionAdapter Session =>
            _session ??= new HttpSessionAdapter(HttpContext.Session);

        /// <summary>
        /// Maps a virtual path (~/...) to an absolute path on disk.
        /// Replaces the legacy MapPath().
        /// </summary>
        protected string MapPath(string virtualPath) =>
            PathHelper.MapPath(virtualPath);
    }
}
