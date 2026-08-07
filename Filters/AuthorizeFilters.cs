using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Michaelhouse.Filters
{
    /// <summary>
    /// Redirects to login if user is not authenticated.
    /// </summary>
    public class RequireLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }

    /// <summary>
    /// Allows only Parents. Redirects to login if not authenticated,
    /// or returns 403 if wrong role.
    /// </summary>
    public class ParentOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (session.GetString("UserRole") != "Parent")
            {
                filterContext.Result = new StatusCodeResult(403);
            }
        }
    }

    /// <summary>
    /// Allows only Admins. Redirects to login if not authenticated,
    /// or returns 403 if wrong role.
    /// </summary>
    public class AdminOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (session.GetString("UserRole") != "Admin")
            {
                filterContext.Result = new StatusCodeResult(403);
            }
        }
    }

    /// <summary>
    /// Allows only Teachers. Redirects to login if not authenticated,
    /// or returns 403 if wrong role.
    /// </summary>
    public class TeacherOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (session.GetString("UserRole") != "Teacher")
            {
                filterContext.Result = new StatusCodeResult(403);
            }
        }
    }

    /// <summary>
    /// Allows only Students. Redirects to login if not authenticated,
    /// or returns 403 if wrong role.
    /// </summary>
    public class StudentOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (session.GetString("UserRole") != "Student")
            {
                filterContext.Result = new StatusCodeResult(403);
            }
        }
    }

    /// <summary>
    /// Allows only Transport Managers and Admins.
    /// </summary>
    public class TransportManagerOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            var role = session.GetString("UserRole");
            if (role != "TransportManager" && role != "Admin")
            {
                filterContext.Result = new StatusCodeResult(403);
            }
        }
    }

    /// <summary>
    /// Allows only Inventory Managers and Admins.
    /// </summary>
    public class InventoryManagerOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            var role = session.GetString("UserRole");
            if (role != "InventoryManager" && role != "Admin")
            {
                filterContext.Result = new StatusCodeResult(403);
            }
        }
    }

    public class TransportManagerOrAdminOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            if (session.GetInt32("UserId") == null)
            {
                filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }
            var role = session.GetString("UserRole");
            if (role != "Admin" && role != "TransportManager")
            {
                filterContext.Result = new StatusCodeResult(403);
            }
        }

        public class AdminOrTransportManagerOnlyAttribute : ActionFilterAttribute
        {
            public override void OnActionExecuting(ActionExecutingContext filterContext)
            {
                var session = filterContext.HttpContext.Session;
                if (session.GetInt32("UserId") == null)
                {
                    filterContext.Result = new RedirectToActionResult("Login", "Account", null);
                    return;
                }
                var role = session.GetString("UserRole");
                if (role != "Admin" && role != "TransportManager")
                {
                    filterContext.Result = new StatusCodeResult(403);
                }
            }
        }
    }
}
