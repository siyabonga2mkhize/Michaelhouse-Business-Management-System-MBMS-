using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Emergency safety (Student) — from Michaelhouse.Mobile
    //
    //   • the active emergency alert: what's happening, the assembly
    //     point and the safe zone (GET api/mobile/student/emergency)
    //   • "I'm Safe" sends the student's GPS position; the server checks
    //     it against the safe zone and records the confirmation
    //     (POST api/mobile/student/emergency/confirm)
    //   • the student's emergency alerts (GET api/mobile/student/alerts,
    //     only the EmergencyAlert ones — event invitations belong in
    //     My Events); tap one to mark it read
    // ============================================================
    public class SafetyPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly RefreshView _refresh = new RefreshView();
        private readonly ContentView _emergencyHost = new ContentView();
        private readonly VerticalStackLayout _alerts = new VerticalStackLayout { Spacing = 10 };
        private bool _loading;
        private bool _confirming;

        public SafetyPage()
        {
            Title = "Safety";

            _root.Children.Add(Ui.PageHeader("Emergency Safety"));
            _root.Children.Add(_emergencyHost);
            _root.Children.Add(new Label { Text = "Emergency alerts", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 8, 0, 0) });
            _root.Children.Add(_alerts);

            _emergencyHost.Content = new ActivityIndicator { IsRunning = true };

            _refresh.Content = new ScrollView { Content = _root };
            _refresh.Refreshing += async (s, e) =>
            {
                await LoadAsync();
                _refresh.IsRefreshing = false;
            };
            Content = _refresh;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;

            var emergencyTask = _api.GetActiveEmergencyAsync();
            var alertsTask = _api.GetStudentAlertsAsync();
            var emergency = await emergencyTask;
            var alerts = await alertsTask;

            _loading = false;

            ShowEmergency(emergency);
            ShowAlerts(alerts);
        }

        // ============================================================
        // ACTIVE EMERGENCY + "I'M SAFE"
        // ============================================================

        private void ShowEmergency(MobileResult<EmergencyAlertDto> result, string? note = null, bool noteGood = false)
        {
            if (!result.Ok)
            {
                _emergencyHost.Content = Ui.Banner(BannerKind.Warning, "Can't check for emergencies", result.Error);
                return;
            }

            var alert = result.Data;
            if (alert == null)
            {
                _emergencyHost.Content = Ui.Banner(BannerKind.Success, "✓ All clear", "There is no active emergency alert.");
                return;
            }

            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(new Label
            {
                Text = "⚠ EMERGENCY",
                FontFamily = "MontserratBold",
                FontSize = 14,
                CharacterSpacing = 2,
                TextColor = Colors.White
            });
            stack.Children.Add(new Label { Text = alert.Message, FontFamily = "PlayfairBold", FontSize = 22, TextColor = Colors.White });
            stack.Children.Add(new Label
            {
                Text = "Go to the assembly point: " + alert.AssemblyPoint + "\nSafe zone: within " + alert.RadiusMeters.ToString("0") + " m",
                FontSize = 15,
                TextColor = Colors.White
            });

            var card = new Border
            {
                BackgroundColor = Ui.Color("Primary"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(18),
                Content = stack
            };

            var confirm = new Button
            {
                Text = "I'm Safe — Confirm with GPS",
                Style = Ui.Style("PrimaryButton"),
                BackgroundColor = Color.FromArgb("#065F46")
            };
            confirm.Clicked += async (s, e) => await ConfirmSafeAsync(confirm, result);

            var host = new VerticalStackLayout { Spacing = 12 };
            host.Children.Add(card);
            if (!string.IsNullOrEmpty(note))
            {
                host.Children.Add(Ui.Banner(noteGood ? BannerKind.Success : BannerKind.Danger, null, note));
            }
            host.Children.Add(confirm);
            host.Children.Add(Ui.Text("Your location is only used to check you're inside the safe zone.", "MutedText"));

            _emergencyHost.Content = host;
        }

        private async Task ConfirmSafeAsync(Button confirm, MobileResult<EmergencyAlertDto> alert)
        {
            if (_confirming) return;
            _confirming = true;
            confirm.IsEnabled = false;
            confirm.Text = "Getting your location...";

            try
            {
                var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (permission != PermissionStatus.Granted)
                {
                    ShowEmergency(alert, "Location permission is needed to confirm you are safe. Allow it in Android Settings → Apps → Michaelhouse → Permissions.");
                    return;
                }

                Location? location;
                try
                {
                    location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(20)))
                               ?? await Geolocation.Default.GetLastKnownLocationAsync();
                }
                catch (FeatureNotEnabledException)
                {
                    ShowEmergency(alert, "Location is turned off. Turn on Location in the phone's quick settings, then try again.");
                    return;
                }

                if (location == null)
                {
                    ShowEmergency(alert, "Your location isn't available right now. Move outside or near a window, then try again.");
                    return;
                }

                confirm.Text = "Confirming...";
                var result = await _api.ConfirmSafetyAsync(location.Latitude, location.Longitude);

                if (!result.Ok || result.Data == null)
                {
                    ShowEmergency(alert, result.Error ?? "Your safety confirmation could not be sent. Please try again.");
                    return;
                }

                var r = result.Data;
                var text = r.Message + (r.WithinGeofence ? "" : " (" + r.Distance.ToString("0") + " m from the assembly point)");
                ShowEmergency(alert, text, r.WithinGeofence);
            }
            catch (Exception ex)
            {
                ShowEmergency(alert, "Your location couldn't be read: " + ex.Message);
            }
            finally
            {
                _confirming = false;
            }
        }

        // ============================================================
        // ALERTS
        // ============================================================

        private void ShowAlerts(MobileResult<System.Collections.Generic.List<StudentAlertDto>> result)
        {
            _alerts.Children.Clear();

            if (!result.Ok)
            {
                _alerts.Children.Add(Ui.Banner(BannerKind.Warning, null, result.Error));
                return;
            }

            // Only emergency alerts here (invitations are in My Events)
            var alerts = (result.Data ?? new System.Collections.Generic.List<StudentAlertDto>())
                .Where(a => a.IsEmergency)
                .ToList();
            if (alerts.Count == 0)
            {
                _alerts.Children.Add(Ui.Text("You have no emergency alerts.", "MutedText"));
                return;
            }

            foreach (var a in alerts)
            {
                _alerts.Children.Add(AlertCard(a));
            }
        }

        private View AlertCard(StudentAlertDto alert)
        {
            var dot = new BoxView
            {
                WidthRequest = 10,
                HeightRequest = 10,
                CornerRadius = 5,
                Color = Ui.Color("Primary"),
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 6, 0, 0),
                IsVisible = !alert.IsRead
            };

            var text = new VerticalStackLayout { Spacing = 4 };
            text.Children.Add(new Label
            {
                Text = alert.Message,
                FontSize = 15,
                FontFamily = alert.IsRead ? "Montserrat" : "MontserratSemiBold",
                TextColor = Ui.Color("TextPrimary")
            });
            text.Children.Add(new Label
            {
                Text = alert.CreatedAt.ToLocalTime().ToString("ddd dd MMM yyyy, HH:mm") + (string.IsNullOrEmpty(alert.Type) ? "" : " · " + alert.Type),
                Style = Ui.Style("MutedText"),
                FontSize = 11
            });

            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 };
            row.Add(dot, 0, 0);
            row.Add(text, 1, 0);

            var card = Ui.Card(row, new Thickness(14));
            if (!alert.IsRead)
            {
                card.OnTap(async () =>
                {
                    if (alert.IsRead) return;
                    var result = await _api.MarkStudentAlertReadAsync(alert.Id);
                    if (result.Ok)
                    {
                        alert.IsRead = true;
                        dot.IsVisible = false;
                        ((Label)text.Children[0]).FontFamily = "Montserrat";
                    }
                });
            }
            return card;
        }
    }
}
