using System;
using System.Text;
using System.Web.Security;

namespace Michaelhouse.Services
{
    // Short-lived protected bearer tokens for mobile clients; MVC sessions remain unchanged.
    public class MobileApiTokenService
    {
        private const string Purpose = "Michaelhouse.MobileApi.v1";
        public string Create(int userId, string role)
        {
            var value = userId + "|" + (role ?? "") + "|" + DateTime.UtcNow.AddHours(12).Ticks;
            return Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(value), Purpose)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
        public bool TryRead(string token, out int userId, out string role)
        {
            userId = 0; role = null;
            try
            {
                var value = token.Replace('-', '+').Replace('_', '/');
                value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
                var fields = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(value), Purpose)).Split('|');
                long expiry;
                if (fields.Length != 3 || !int.TryParse(fields[0], out userId) || !long.TryParse(fields[2], out expiry) || DateTime.UtcNow.Ticks > expiry) return false;
                role = fields[1]; return userId > 0;
            }
            catch { return false; }
        }
    }
}
