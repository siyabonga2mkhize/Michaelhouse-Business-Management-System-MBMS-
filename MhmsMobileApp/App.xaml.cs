using MhmsMobileApp.Services;
using MhmsMobileApp.Views;

namespace MhmsMobileApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // The web app has no dark mode; keep the same look on every phone
            UserAppTheme = AppTheme.Light;

            ApiService.SessionExpired += (s, e) =>
            {
                if (!UserSession.IsLoggedIn) return;
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowLogin("Your session has expired. Please log in again."));
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Always start at the login screen
            return new Window(new LoginPage());
        }

        // After a successful login: tabs for the user's role
        // openTab: route of a tab to open first, e.g. "SafetyPage" when a
        // student still has to confirm they're safe
        public static void ShowMainShell(string? openTab = null)
        {
            var shell = new AppShell();
            SetRootPage(shell);

            if (!string.IsNullOrEmpty(openTab))
            {
                shell.Dispatcher.Dispatch(async () =>
                {
                    try { await shell.GoToAsync("//" + openTab); }
                    catch (Exception) { /* the role has no such tab */ }
                });
            }
        }

        // Log out (or session expired): back to the login screen
        public static void ShowLogin(string? message = null)
        {
            UserSession.Clear();
            SetRootPage(new LoginPage(message));
        }

        private static void SetRootPage(Page page)
        {
            var window = Current?.Windows.FirstOrDefault();
            if (window != null) window.Page = page;
        }
    }
}
