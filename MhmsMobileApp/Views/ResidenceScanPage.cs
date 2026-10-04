using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // House Master — residence QR sign in / sign out
    //
    // Same steps and rules as the web page HouseMaster/ScanQRCode:
    //   1. scan the student's QR (or type their student number)
    //   2. see the student, their room and status
    //   3. Sign in (check in) when they're outside, or Sign out (check
    //      out) when they're inside — logged as a QR scan record
    // (POST api/residence-scan/lookup | check-in | check-out)
    // ============================================================
    public class ResidenceScanPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly CameraBarcodeReaderView _camera;
        private readonly Border _cameraFrame;
        private readonly ContentView _resultHost = new ContentView();
        private readonly Entry _manual = new Entry { Placeholder = "Student number or QR code", FontSize = 16 };
        private readonly ScrollView _scroll = new ScrollView();
        private bool _busy;

        public ResidenceScanPage()
        {
            Title = "Scan QR";

            _camera = new CameraBarcodeReaderView
            {
                IsDetecting = false,
                Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true, Multiple = false }
            };
            _camera.BarcodesDetected += OnBarcodesDetected;

            _cameraFrame = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                HeightRequest = 280,
                BackgroundColor = Colors.Black,
                Content = _camera
            };

            var lookup = new Button { Text = "Look up", Style = Ui.Style("SecondaryButton"), WidthRequest = 120 };
            lookup.Clicked += async (s, e) => await LookupAsync(_manual.Text);
            _manual.Completed += async (s, e) => await LookupAsync(_manual.Text);

            var manualRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
            manualRow.Add(Ui.InputBox(_manual), 0, 0);
            manualRow.Add(lookup, 1, 0);

            var root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
            root.Children.Add(Ui.PageHeader("Residence Sign In / Out", "Scan the student's QR code."));
            root.Children.Add(_cameraFrame);
            root.Children.Add(new Label { Text = "Camera can't read it?", Style = Ui.Style("FieldLabel") });
            root.Children.Add(manualRow);
            root.Children.Add(_resultHost);

            _scroll.Content = root;
            Content = _scroll;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var permission = await Permissions.RequestAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted)
            {
                _cameraFrame.IsVisible = false;
                _resultHost.Content = Ui.Banner(BannerKind.Warning, "Camera permission needed",
                    "Allow Michaelhouse to use the camera (Android Settings → Apps → Michaelhouse → Permissions), or type the student number below.");
                return;
            }

            _cameraFrame.IsVisible = true;
            _camera.IsDetecting = !_busy;
        }

        // Free the camera when leaving the tab
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _camera.IsDetecting = false;
        }

        private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
        {
            var value = e.Results?.FirstOrDefault()?.Value;
            if (_busy || string.IsNullOrWhiteSpace(value)) return;

            // The scanner reports on a background thread
            MainThread.BeginInvokeOnMainThread(async () => await LookupAsync(value));
        }

        // ============================================================
        // LOOK UP
        // ============================================================

        private async Task LookupAsync(string? value)
        {
            if (_busy || string.IsNullOrWhiteSpace(value)) return;
            _busy = true;
            _camera.IsDetecting = false;

            _resultHost.Content = new ActivityIndicator { IsRunning = true };

            var result = await _api.LookupResidenceQrAsync(value.Trim());
            ShowResult(result, null);

            _busy = false;
        }

        private void ShowResult(ResidenceScanDto r, string? note)
        {
            if (!r.Ok)
            {
                var stack = new VerticalStackLayout { Spacing = 10 };
                stack.Children.Add(Ui.Banner(BannerKind.Danger, "Not found", r.Error));
                stack.Children.Add(ScanNextButton());
                _resultHost.Content = stack;
                _ = _scroll.ScrollToAsync(_resultHost, ScrollToPosition.Start, true);
                return;
            }

            var card = new VerticalStackLayout { Spacing = 8 };

            if (!string.IsNullOrEmpty(note))
            {
                card.Children.Add(Ui.Banner(BannerKind.Success, null, "✓ " + note));
            }

            card.Children.Add(new Label { Text = r.Name, FontFamily = "PlayfairBold", FontSize = 24, TextColor = Ui.Color("TextPrimary") });
            if (!string.IsNullOrEmpty(r.StudentNumber))
                card.Children.Add(Ui.Text("Student no. " + r.StudentNumber, "MutedText"));

            if (r.HasAssignment)
            {
                card.Children.Add(Ui.Text(string.Join(" · ", new[] { r.Residence, r.Room == null ? null : "Room " + r.Room, r.Bed == null ? null : "Bed " + r.Bed }
                    .Where(x => !string.IsNullOrEmpty(x)))));
            }

            card.Children.Add(StatusChip(r));

            var times = new[]
            {
                r.LastCheckIn == null ? null : "Last signed in: " + r.LastCheckIn,
                r.LastCheckOut == null ? null : "Last signed out: " + r.LastCheckOut
            }.Where(x => x != null);
            var timesText = string.Join("\n", times);
            if (timesText.Length > 0) card.Children.Add(Ui.Text(timesText, "MutedText"));

            if (r.CanCheckIn)
            {
                var signIn = new Button { Text = "Sign In (Check In)", Style = Ui.Style("PrimaryButton") };
                signIn.Clicked += async (s, e) => await ActAsync(signIn, () => _api.CheckInAsync(r.StudentId));
                card.Children.Add(signIn);
            }
            if (r.CanCheckOut)
            {
                var signOut = new Button { Text = "Sign Out (Check Out)", Style = Ui.Style("PrimaryButton") };
                signOut.Clicked += async (s, e) => await ActAsync(signOut, () => _api.CheckOutAsync(r.StudentId));
                card.Children.Add(signOut);
            }
            if (!r.CanCheckIn && !r.CanCheckOut)
            {
                card.Children.Add(Ui.Banner(BannerKind.Info, null,
                    r.HasAssignment
                        ? "Sign in / out isn't available while the student is " + r.StatusLabel.ToLower() + ". Use the website for holiday, leave or suspension changes."
                        : "This student has a valid QR identity but no residence assignment."));
            }

            var wrapper = new VerticalStackLayout { Spacing = 10 };
            wrapper.Children.Add(Ui.Card(card, new Thickness(18)));
            wrapper.Children.Add(ScanNextButton());
            _resultHost.Content = wrapper;
            _ = _scroll.ScrollToAsync(_resultHost, ScrollToPosition.Start, true);
        }

        private static View StatusChip(ResidenceScanDto r)
        {
            switch (r.Status)
            {
                case "Inside": return Ui.Chip(r.StatusLabel, "#D1FAE5", "#065F46", 12);
                case "Outside": return Ui.Chip(r.StatusLabel, "#FEF3C7", "#92400E", 12);
                case "Suspended":
                case "Archived": return Ui.Chip(r.StatusLabel, "#FEE2E2", "#991B1B", 12);
                default: return Ui.Chip(r.StatusLabel, "#DBEAFE", "#1E40AF", 12);
            }
        }

        private Button ScanNextButton()
        {
            var next = new Button { Text = "Scan Next Student", Style = Ui.Style("SecondaryButton") };
            next.Clicked += async (s, e) =>
            {
                _resultHost.Content = null;
                _manual.Text = "";
                await _scroll.ScrollToAsync(0, 0, true);
                _camera.IsDetecting = _cameraFrame.IsVisible;
            };
            return next;
        }

        // ============================================================
        // SIGN IN / SIGN OUT
        // ============================================================

        private async Task ActAsync(Button button, Func<Task<ResidenceScanDto>> action)
        {
            if (_busy) return;
            _busy = true;
            button.IsEnabled = false;
            var label = button.Text;
            button.Text = "Saving...";

            var result = await action();

            if (result.Ok)
            {
                ShowResult(result, result.Message);
            }
            else
            {
                button.IsEnabled = true;
                button.Text = label;
                await DisplayAlert("Not recorded", result.Error, "OK");
            }

            _busy = false;
        }
    }
}
