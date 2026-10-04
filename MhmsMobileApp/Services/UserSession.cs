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
        ResidenceScan         // house master: QR sign in / sign out
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
                return new[] { AppScreen.MealPlan, AppScreen.Events, AppScreen.Safety, AppScreen.MyQr };

            if (Is(role, "HouseMaster") || Is(role, "Housemaster"))
                return new[] { AppScreen.Events, AppScreen.HouseMasterEmergency, AppScreen.ResidenceScan };

            if (Is(role, "Parent") || StaffRoles.Any(r => Is(role, r)))
                return new[] { AppScreen.Events };

            if (Is(role, "Chef"))
                return new[] { AppScreen.MealCollection, AppScreen.KitchenPlan };

            if (Is(role, "CafeteriaManager") || Is(role, "Admin"))
                return new[] { AppScreen.MealCollection, AppScreen.RecordDelivery, AppScreen.KitchenPlan, AppScreen.FeastPlan };

            return Array.Empty<AppScreen>();
        }

        private static bool Is(string role, string name) =>
            string.Equals(role, name, StringComparison.OrdinalIgnoreCase);
    }
}
