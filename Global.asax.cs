using Stripe;
using System;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Michaelhouse
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // ==============================================================
            // STEP 15 - BLOCK 1: ADDED AT THE VERY TOP
            // ==============================================================
            System.Data.Entity.Database.SetInitializer(
                new System.Data.Entity.MigrateDatabaseToLatestVersion<
                    Michaelhouse.Models.DBContextClass,
                    Michaelhouse.Migrations.Configuration>());
            // ==============================================================

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);

            // ─── WEB API ROUTES (NEW - for mobile app) ─────────────────
            // This line registers the /api/ routes.
            // Without it, the mobile app cannot connect.
            GlobalConfiguration.Configure(WebApiConfig.Register);
            // ─────────────────────────────────────────────────────────

            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            try
            {
                var stripeKey = System.Configuration.ConfigurationManager.AppSettings["StripeApiKey"];
                if (!string.IsNullOrWhiteSpace(stripeKey))
                {
                    StripeConfiguration.ApiKey = stripeKey;
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning("StripeApiKey not found in AppSettings; Stripe calls will fail until configured.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Stripe initialization failed: " + ex);
            }

            // 🌱 SEED THE DATABASE ONCE WHEN THE APP STARTS
            //SeedDrivers();
            //SeedMichaelhouseSystem();

            // ==============================================================
            // STEP 15 - BLOCK 2: ADDED AT THE VERY BOTTOM
            // ==============================================================
            try
            {
                using (var db = new Michaelhouse.Models.DBContextClass())
                {
                    Michaelhouse.Data.CafeteriaSeeder.Seed(db);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CafeteriaSeeder failed: " + ex.Message);
            }
            // ==============================================================
        }
    }
}