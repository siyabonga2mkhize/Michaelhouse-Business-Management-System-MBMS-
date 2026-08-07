using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
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

            try
            {
                var stripeKey = AppConfig.AppSettings("StripeApiKey");
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
                // Do not rethrow — allow the app to start so you can inspect logs and fix assemblies
            }
        }
        private void SeedDrivers()
        {
            using (var db = DbContextFactory.Create())
            {
                // Prevent duplicate seeding
                if (db.Drivers.Any())
                    return;

                var drivers = new List<Driver>
        {
            new Driver
            {
                FullName = "Sibusiso Mthembu",
                IDNumber = "9001015009087",
                PhoneNumber = "0721111111",
                Email = "sibusiso@michaelhouse.com",
                LicenceNumber = "LIC1001",
                LicenceExpiryDate = DateTime.Now.AddYears(3),
                HasPDP = true,
                IsActive = true,
                DateCreated = DateTime.Now,
                PasswordHash = "123456"
            },
            new Driver
            {
                FullName = "Thabo Khumalo",
                IDNumber = "9202025009088",
                PhoneNumber = "0722222222",
                Email = "thabo@michaelhouse.com",
                LicenceNumber = "LIC1002",
                LicenceExpiryDate = DateTime.Now.AddYears(2),
                HasPDP = true,
                IsActive = true,
                DateCreated = DateTime.Now,
                PasswordHash = "123456"
            },
            new Driver
            {
                FullName = "Mandla Dlamini",
                IDNumber = "9303035009089",
                PhoneNumber = "0723333333",
                Email = "mandla@michaelhouse.com",
                LicenceNumber = "LIC1003",
                LicenceExpiryDate = DateTime.Now.AddYears(4),
                HasPDP = true,
                IsActive = true,
                DateCreated = DateTime.Now,
                PasswordHash = "123456"
            },
            new Driver
            {
                FullName = "Sipho Zulu",
                IDNumber = "9404045009090",
                PhoneNumber = "0724444444",
                Email = "sipho@michaelhouse.com",
                LicenceNumber = "LIC1004",
                LicenceExpiryDate = DateTime.Now.AddYears(1),
                HasPDP = true,
                IsActive = true,
                DateCreated = DateTime.Now,
                PasswordHash = "123456"
            }
        };

                db.Drivers.AddRange(drivers);
                db.SaveChanges();
            }
        }

    }
}
