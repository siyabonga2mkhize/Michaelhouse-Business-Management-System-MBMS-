using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using System;

namespace MhmsMobileApp.Views
{
    public partial class LoginPage : ContentPage
    {
        private const string LoginText = "Sign Into Portal";

        private readonly ApiService _api = new ApiService();

        // Once the server has answered, later visits to this screen
        // (e.g. after logging out) don't show the "Connecting" box
        private static bool _serverReached;
        private CancellationTokenSource? _connecting;

        // message: optional note to show, e.g. "Your session has expired"
        public LoginPage(string? message = null)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(message))
            {
                ShowError(message);
            }

            ConnectionBox.IsVisible = !_serverReached;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!_serverReached) _ = WaitForServerAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _connecting?.Cancel();
        }

        // The website may still be starting after F5. Keep checking in
        // the background (the screen stays usable) and say what's happening.
        private async Task WaitForServerAsync()
        {
            _connecting?.Cancel();
            var cts = _connecting = new CancellationTokenSource();
            int attempt = 0;

            while (!cts.IsCancellationRequested)
            {
                attempt++;
                if (await _api.PingAsync(TimeSpan.FromSeconds(90)))
                {
                    _serverReached = true;
                    ConnectionBox.IsVisible = false;
                    return;
                }

                if (cts.IsCancellationRequested) return;

                ConnectionLabel.Text = attempt == 1
                    ? "Can't reach the Michaelhouse server yet. Is the website running? Trying again..."
                    : "Still trying to reach the Michaelhouse server (attempt " + attempt + ")...";

                try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token); }
                catch (TaskCanceledException) { return; }
            }
        }

        private void OnEmailCompleted(object sender, EventArgs e)
        {
            PasswordEntry.Focus();
        }

        // Red border on the box being typed in (web: .form-input-focus)
        private void OnEntryFocused(object sender, FocusEventArgs e)
        {
            BoxFor(sender).Stroke = (Color)Application.Current!.Resources["Primary"];
        }

        private void OnEntryUnfocused(object sender, FocusEventArgs e)
        {
            BoxFor(sender).Stroke = (Color)Application.Current!.Resources["Gray300"];
        }

        private Border BoxFor(object entry) => entry == EmailEntry ? EmailBox : PasswordBox;

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (!LoginButton.IsEnabled) return;

            ErrorBox.IsVisible = false;

            var email = (EmailEntry.Text ?? "").Trim();
            var password = PasswordEntry.Text ?? "";

            if (email.Length == 0 || password.Length == 0)
            {
                ShowError("Please enter your email and password.");
                return;
            }

            SetBusy(true);

            // If the server is slow (still starting), explain after a few
            // seconds instead of looking frozen
            var login = _api.LoginAsync(email, password);
            if (await Task.WhenAny(login, Task.Delay(TimeSpan.FromSeconds(4))) != login)
            {
                SlowLabel.IsVisible = true;
            }
            var user = await login;
            SlowLabel.IsVisible = false;

            if (user.Ok)
            {
                _serverReached = true;
                _connecting?.Cancel();
                ConnectionBox.IsVisible = false;
            }

            if (!user.Ok)
            {
                SetBusy(false);
                ShowError(user.Error);
                return;
            }

            if (UserSession.ScreensFor(user.Role).Count == 0)
            {
                await _api.LogoutAsync();
                SetBusy(false);
                ShowError("Your account (" + user.Role + ") has no screens in the mobile app. Please use the website.");
                return;
            }

            if (user.MustConfirmSafety)
            {
                await DisplayAlert("Emergency alert",
                    "There is an active emergency alert. Please confirm you are safe on the Michaelhouse website.",
                    "OK");
            }

            UserSession.Start(user);
            PasswordEntry.Text = "";
            SetBusy(false);

            App.ShowMainShell();
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorBox.IsVisible = true;
        }

        private void SetBusy(bool busy)
        {
            LoginButton.IsEnabled = !busy;
            LoginButton.Text = busy ? "Signing in..." : LoginText;
            LoadingSpinner.IsVisible = busy;
            LoadingSpinner.IsRunning = busy;
            EmailEntry.IsEnabled = !busy;
            PasswordEntry.IsEnabled = !busy;
        }
    }
}
