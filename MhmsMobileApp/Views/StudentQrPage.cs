using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.IO;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Student — "My QR": the student's residence QR identity, shown
    // to the House Master to sign in / sign out
    // (GET api/mobile/students/{id}/qr — the web's StudentQRCodeService)
    // ============================================================
    public class StudentQrPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private bool _loaded;

        public StudentQrPage()
        {
            Title = "My QR";
            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            Content = new ScrollView { Content = _root };

            Responsive.Adapt(this, _root);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_loaded) return;

            var user = UserSession.CurrentUser;
            _root.Children.Clear();
            _root.Children.Add(Ui.PageHeader("My Residence QR", user?.Name, "Residence"));

            if (user?.StudentId == null)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, null, "No student profile is linked to this account."));
                return;
            }

            var spinner = new ActivityIndicator { IsRunning = true };
            _root.Children.Add(spinner);

            var result = await _api.GetStudentQrAsync(user.StudentId.Value);
            _root.Children.Remove(spinner);

            if (!result.Ok || result.Data == null || string.IsNullOrEmpty(result.Data.ImageBase64))
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "QR code not available", result.Error ?? "Please try again later."));
                return;
            }

            // Decode the picture off the screen's thread
            var bytes = await Task.Run(() => Convert.FromBase64String(result.Data.ImageBase64));

            var image = new Image
            {
                Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Center
            };
            Responsive.Square(image, this, 0.75, 200, 420);

            var card = Ui.Card(image, new Thickness(20));
            card.BackgroundColor = Microsoft.Maui.Graphics.Colors.White;
            _root.Children.Add(card);

            _root.Children.Add(Ui.Banner(BannerKind.Info, "Signing in or out of residence",
                "Show this code to your House Master. They scan it to sign you in when you arrive and out when you leave."));
            _root.Children.Add(Ui.Text("Turn your screen brightness up if it doesn't scan.", "MutedText"));

            _loaded = true;
        }
    }
}
