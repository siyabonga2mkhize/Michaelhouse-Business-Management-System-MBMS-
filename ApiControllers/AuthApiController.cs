using Michaelhouse.Controllers;
using Michaelhouse.Models;
using Michaelhouse.Services;
using Newtonsoft.Json;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Security;

namespace Michaelhouse.ApiControllers
{
    // ============================================================
    // Mobile — Login / Logout
    //
    //   POST /api/auth/login   { email, password }
    //        → same credential check as Account/Login; issues the
    //          same forms-auth cookie and session values, so the
    //          role-protected APIs ([Authorize(Roles=...)]) work
    //   POST /api/auth/logout  → clears cookie and session
    //
    // The web login page is unchanged.
    // ============================================================
    [RoutePrefix("api/auth")]
    public class AuthApiController : Controller
    {
        private readonly DBContextClass _db;

        public AuthApiController()
        {
            _db = new DBContextClass();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // ============================================================
        // POST /api/auth/login
        // Body: { email, password }
        // ============================================================

        [HttpPost]
        [Route("login")]
        public async Task<JsonResult> Login()
        {
            var data = ReadBody<LoginRequestModel>();

            if (data == null || string.IsNullOrWhiteSpace(data.Email) || string.IsNullOrWhiteSpace(data.Password))
            {
                return Json(new { ok = false, error = "Please enter your email and password." });
            }

            var email = data.Email.Trim();
            var hash = AccountController.HashPassword(data.Password);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hash);

            if (user == null)
            {
                return Json(new { ok = false, error = "Invalid email or password." });
            }

            Session["UserId"] = user.UserId;
            Session["UserName"] = user.Name;
            Session["UserRole"] = user.Role;
            FormsAuthentication.SetAuthCookie(user.Email, false);

            int? parentId = null;
            int? studentId = null;

            if (user.Role == "Parent")
            {
                var parent = await _db.Parents.FirstOrDefaultAsync(p => p.UserId == user.UserId);
                if (parent != null)
                {
                    parentId = parent.ParentId;
                    Session["ParentId"] = parent.ParentId;
                }
            }
            else if (user.Role == "Student")
            {
                var student = await _db.Students.FirstOrDefaultAsync(s => s.UserId == user.UserId);
                if (student != null)
                {
                    studentId = student.StudentId;
                    Session["StudentId"] = student.StudentId;
                }
            }
            else if (user.Role == "HouseMaster" || user.Role == "Housemaster")
            {
                // Same lookup as Account/Login; BoardingAccessService uses it
                // to limit a House Master to their own residence
                var houseMaster = await _db.HouseMasters.FirstOrDefaultAsync(h =>
                    h.ContactEmail == user.Email || h.FullName == user.Name);
                if (houseMaster != null) Session["HouseMasterId"] = houseMaster.HouseMasterId;
            }
            else if (user.Role == "MaintenanceWorker")
            {
                // Same as Account/Login
                var staff = await _db.MaintenanceStaff.FirstOrDefaultAsync(s => s.UserId == user.UserId);
                if (staff != null) Session["MaintenanceStaffId"] = staff.Id;
            }

            return Json(ToJson(user, parentId, studentId, await HasUnconfirmedEmergencyAsync(user.Role, studentId)));
        }

        // ============================================================
        // POST /api/auth/logout
        // ============================================================

        [HttpPost]
        [Route("logout")]
        public JsonResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            return Json(new { ok = true });
        }

        // ============================================================
        // HELPERS
        // ============================================================

        // Password hash never leaves the server
        private static object ToJson(AppUser user, int? parentId, int? studentId, bool mustConfirmSafety)
        {
            return new
            {
                ok = true,
                userId = user.UserId,
                name = user.Name,
                email = user.Email,
                role = user.Role,
                parentId,
                studentId,
                mustConfirmSafety,
                // Bearer token for the Web API endpoints (api/mobile/*,
                // MobileApiController), so one login works for both
                token = new MobileApiTokenService().Create(user.UserId, user.Role)
            };
        }

        // Same check as Account/Login: a student who has not yet
        // confirmed they are safe during an active emergency alert
        private async Task<bool> HasUnconfirmedEmergencyAsync(string role, int? studentId)
        {
            if (role != "Student" || !studentId.HasValue) return false;

            var activeAlert = await _db.EmergencyAlerts
                .Where(a => a.Status == AlertStatus.Active)
                .OrderByDescending(a => a.AlertTime)
                .FirstOrDefaultAsync();

            if (activeAlert == null) return false;

            return !await _db.StudentSafetyConfirmations
                .AnyAsync(c => c.AlertId == activeAlert.AlertId && c.StudentId == studentId.Value);
        }

        private T ReadBody<T>() where T : class
        {
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public class LoginRequestModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
