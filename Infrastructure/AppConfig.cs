namespace Michaelhouse.Infrastructure
{
    /// <summary>
    /// Static wrapper for IConfiguration to provide ConfigurationManager-compatible access.
    /// Configure once at application startup via <see cref="Configure"/>.
    /// </summary>
    public static class AppConfig
    {
        private static IConfiguration? _config;

        public static void Configure(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Gets a configuration value by key (supports colon-delimited paths like "Email:SmtpHost").
        /// </summary>
        public static string? Get(string key) => _config?[key];

        /// <summary>
        /// Gets a configuration value using the legacy AppSettings["key"] pattern.
        /// Keys that used colons in Web.config (e.g. "Email:SmtpHost") are supported directly.
        /// </summary>
        public static string? AppSettings(string key) => _config?[key];
    }
}
