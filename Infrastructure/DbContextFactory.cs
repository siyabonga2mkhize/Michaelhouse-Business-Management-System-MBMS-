using Michaelhouse.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Michaelhouse.Infrastructure
{
    /// <summary>
    /// Provides a parameterless-constructor compatible way to create DBContextClass instances.
    /// Configure once at application startup via <see cref="Configure"/>.
    /// </summary>
    public static class DbContextFactory
    {
        private static DbContextOptions<Models.DBContextClass>? _options;

        public static void Configure(DbContextOptions<Models.DBContextClass> options)
        {
            _options = options;
        }

        public static Models.DBContextClass Create()
        {
            if (_options == null)
                throw new InvalidOperationException("DbContextFactory has not been configured. Call DbContextFactory.Configure() at startup.");
            return new Models.DBContextClass(_options);
        }
    }
}
