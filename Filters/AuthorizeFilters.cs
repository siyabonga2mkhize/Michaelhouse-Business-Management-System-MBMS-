using System.Web.Mvc;

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
            if (session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
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

            if (session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            if (session["UserRole"]?.ToString() != "Parent")
            {
                filterContext.Result = new HttpUnauthorizedResult();
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

            if (session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            if (session["UserRole"]?.ToString() != "Admin")
            {
                filterContext.Result = new HttpUnauthorizedResult();
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

            if (session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            if (session["UserRole"]?.ToString() != "Teacher")
            {
                filterContext.Result = new HttpUnauthorizedResult();
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

            if (session["UserId"] == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            if (session["UserRole"]?.ToString() != "Student")
            {
                filterContext.Result = new HttpUnauthorizedResult();
            }
        }
    }
}
