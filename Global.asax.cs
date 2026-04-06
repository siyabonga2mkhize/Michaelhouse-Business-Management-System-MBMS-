using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;


namespace Michaelhouse
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            StripeConfiguration.ApiKey = "sk_test_51TGkmpCX1ORzTt6NEmDOc0b3JZfikEKfkgwHx2ulmuep6cuNGTAFlY3WE65A5bUy8BQ2A0HShSCBqze2A4jFGkJ300YdaTPxkN";
        }
    }
}
