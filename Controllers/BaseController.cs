using Michaelhouse.Models;
using System.Linq;
using System.Web.Mvc;

namespace Michaelhouse.Controllers
{
    public class BaseController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            // Only run this if user is logged in and is a Student
            if (Session["UserId"] != null && Session["UserRole"]?.ToString() == "Student")
            {
                int userId = (int)Session["UserId"];

                using (var db = new DBContextClass())
                {
                    var activeAlert = db.EmergencyAlerts
                        .Where(a => a.Status == AlertStatus.Active)
                        .OrderByDescending(a => a.AlertTime)
                        .FirstOrDefault();

                    if (activeAlert != null)
                    {
                        var student = db.Students.FirstOrDefault(s => s.UserId == userId);
                        if (student != null)
                        {
                            bool alreadyConfirmed = db.StudentSafetyConfirmations
                                .Any(c => c.AlertId == activeAlert.AlertId && c.StudentId == student.StudentId);

                            // If not confirmed, FORCE redirect to emergency!
                            if (!alreadyConfirmed)
                            {
                                filterContext.Result = RedirectToAction("ConfirmSafety", "Emergency");
                                return; // Stop further processing
                            }
                        }
                    }
                }
            }
        }
    }
}