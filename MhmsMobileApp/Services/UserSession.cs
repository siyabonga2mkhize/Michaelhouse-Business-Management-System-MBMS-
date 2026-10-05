using MhmsMobileApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MhmsMobileApp.Services
{
    // The screens the mobile app offers
    public enum AppScreen
    {
        MealPlan,
        Events,
        MealCollection,
        RecordDelivery,
        KitchenPlan,
        FeastPlan,
        Safety,               // student: emergency alert + "I'm safe" + alerts
        HouseMasterEmergency, // house master: residence alerts + roll call
        MyQr,                 // student: residence QR identity
        ResidenceScan,        // house master: QR sign in / sign out
        Requests,             // student: report a fault, leave, visitor
        ReportFault,          // everyone else: report an asset failure
        JobCards              // maintenance worker: complete job cards
    }

    // ============================================================
    // Who is logged in, and which screens their role may see.
    // The server still enforces roles on every API call — this
    // only decides which tabs to show.
    // ============================================================
    public static class UserSession
    {
        public static CurrentUserDto? CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        // The login cookie is kept by ApiService; the Web API token too
        public static void Start(CurrentUserDto user)
        {
            CurrentUser = user;
            ApiService.SetToken(user.Token);
        }

        public static void Clear()
        {
            CurrentUser = null;
            ApiService.SetToken(null);
        }

        // Same list as EventRsvpService.StaffRoles on the web app,
        // minus the cafeteria roles, which have their own screens below
        private static readonly string[] StaffRoles =
        {
            "Teacher", "HouseMaster", "Housemaster", "Dietitian", "Coach",
            "InventoryManager", "TransportManager",
            "MaintenanceManager", "MaintenanceWorker", "Driver"
        };

        public static IReadOnlyList<AppScreen> ScreensFor(string? role)
        {
            role = (role ?? "").Trim();

            if (Is(role, "Student"))
                return new[] { AppScreen.MealPlan, AppScreen.Events, AppScreen.Safety, AppScreen.MyQr, AppScreen.Requests };

            if (Is(role, "HouseMaster") || Is(role, "Housemaster"))
                return new[] { AppScreen.Events, AppScreen.HouseMasterEmergency, AppScreen.ResidenceScan, AppScreen.ReportFault };

            if (Is(role, "MaintenanceWorker"))
                return new[] { AppScreen.JobCards, AppScreen.Events, AppScreen.ReportFault };

            if (Is(role, "Parent") || StaffRoles.Any(r => Is(role, r)))
                return new[] { AppScreen.Events, AppScreen.ReportFault };

            if (Is(role, "Chef"))
                return new[] { AppScreen.MealCollection, AppScreen.KitchenPlan, AppScreen.ReportFault };

            if (Is(role, "CafeteriaManager") || Is(role, "Admin"))
                return new[] { AppScreen.MealCollection, AppScreen.RecordDelivery, AppScreen.KitchenPlan, AppScreen.FeastPlan, AppScreen.ReportFault };

            // Any other logged-in user can still report a fault
            if (role.Length > 0)
                return new[] { AppScreen.ReportFault };

            return Array.Empty<AppScreen>();
        }

        private static bool Is(string role, string name) =>
            string.Equals(role, name, StringComparison.OrdinalIgnoreCase);
    }
}
