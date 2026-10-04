using MhmsMobileApp.Services;
using MhmsMobileApp.Views;

namespace MhmsMobileApp
{
    public partial class AppShell : Shell
    {
        private const string LogoutText = "Log out";

        public AppShell()
        {
            InitializeComponent();

            foreach (var screen in UserSession.ScreensFor(UserSession.CurrentUser?.Role))
            {
                MainTabs.Items.Add(CreateTab(screen));
            }

            Navigated += OnNavigated;
        }

        private static ShellContent CreateTab(AppScreen screen)
        {
            switch (screen)
            {
                case AppScreen.MealPlan:
                    return Tab("My Meal Plan", "MealPlanPage", () => new MealPlanPage());
                case AppScreen.Events:
                    return Tab("My Events", "RsvpPage", () => new RsvpPage());
                case AppScreen.MealCollection:
                    return Tab("Meal Collection", "MealCollectionPage", () => new MealCollectionPage());
                case AppScreen.RecordDelivery:
                    return Tab("Record Delivery", "RecordDeliveryPage", () => new RecordDeliveryPage());
                case AppScreen.KitchenPlan:
                    return Tab("Kitchen Plan", "ProductionPage", () => new ProductionPage());
                case AppScreen.FeastPlan:
                    return Tab("Feast Plan", "FeastPlanPage", () => new FeastPlanPage());
                case AppScreen.Safety:
                    return Tab("Safety", "SafetyPage", () => new SafetyPage());
                case AppScreen.HouseMasterEmergency:
                    return Tab("Emergency", "HouseMasterEmergencyPage", () => new HouseMasterEmergencyPage());
                case AppScreen.MyQr:
                    return Tab("My QR", "StudentQrPage", () => new StudentQrPage());
                case AppScreen.ResidenceScan:
                    return Tab("Scan QR", "ResidenceScanPage", () => new ResidenceScanPage());
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen));
            }
        }

        private static ShellContent Tab(string title, string route, Func<Page> createPage)
        {
            return new ShellContent
            {
                Title = title,
                Route = route,
                ContentTemplate = new DataTemplate(createPage)
            };
        }

        // Put a "Log out" button in the top bar of every page we show
        private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            var page = CurrentPage;
            if (page == null || page.ToolbarItems.Any(t => t.Text == LogoutText)) return;

            page.ToolbarItems.Add(new ToolbarItem
            {
                Text = LogoutText,
                Order = ToolbarItemOrder.Primary,
                Command = new Command(async () => await ConfirmLogoutAsync())
            });
        }

        private async Task ConfirmLogoutAsync()
        {
            var name = UserSession.CurrentUser?.Name ?? "";
            bool yes = await DisplayAlert("Log out", "Log out " + name + "?", "Log out", "Cancel");
            if (!yes) return;

            // Back to the login screen straight away; the server is told
            // in the background (no waiting on the network here)
            var logout = new ApiService().LogoutAsync();
            App.ShowLogin();
            await logout;
        }
    }
}
