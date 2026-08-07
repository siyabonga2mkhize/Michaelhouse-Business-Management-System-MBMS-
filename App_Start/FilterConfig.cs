using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace Michaelhouse
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
