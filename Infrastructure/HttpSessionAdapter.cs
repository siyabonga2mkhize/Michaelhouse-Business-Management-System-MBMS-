namespace Michaelhouse.Infrastructure
{
    /// <summary>
    /// Wraps ISession to provide an object-indexer interface compatible with the legacy
    /// System.Web.SessionStateBase indexer.  Integers are stored via SetInt32/GetInt32;
    /// all other values are stored as strings via SetString/GetString.
    /// </summary>
    public sealed class HttpSessionAdapter
    {
        private readonly ISession _session;

        public HttpSessionAdapter(ISession session)
        {
            _session = session;
        }

        public object? this[string key]
        {
            get
            {
                // Try int storage first
                var intVal = _session.GetInt32(key);
                if (intVal.HasValue) return intVal.Value;

                // Fall back to string storage
                return _session.GetString(key);
            }
            set
            {
                if (value == null)
                {
                    _session.Remove(key);
                    return;
                }

                if (value is int intValue)
                {
                    _session.SetInt32(key, intValue);
                }
                else
                {
                    _session.SetString(key, value.ToString()!);
                }
            }
        }

        /// <summary>Clears all session entries.</summary>
        public void Clear() => _session.Clear();

        /// <summary>No-op — ASP.NET Core session does not require explicit abandon.</summary>
        public void Abandon() { }
    }
}
