namespace Michaelhouse.Infrastructure
{
    /// <summary>
    /// Static helper providing Server.MapPath-compatible path resolution in ASP.NET Core.
    /// Configure once at application startup via <see cref="Configure"/>.
    /// </summary>
    public static class PathHelper
    {
        private static string _contentRootPath = Directory.GetCurrentDirectory();

        public static void Configure(string contentRootPath)
        {
            _contentRootPath = contentRootPath;
        }

        /// <summary>
        /// Resolves a virtual path (~/...) to an absolute file system path.
        /// </summary>
        public static string MapPath(string virtualPath)
        {
            if (virtualPath.StartsWith("~/"))
            {
                var relative = virtualPath[2..].Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(_contentRootPath, relative);
            }
            return Path.Combine(_contentRootPath, virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
