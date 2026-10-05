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
    // Scan one QR code and return it (e.g. an asset's QR sticker).
    //   var code = await ScanCodePage.ScanAsync(Navigation, "Scan the asset's QR");
    // Null when the person goes back without scanning.
    // ============================================================
    public class ScanCodePage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _result = new TaskCompletionSource<string?>();
        private readonly CameraBarcodeReaderView _camera;
        private bool _done;

        public static async Task<string?> ScanAsync(INavigation navigation, string title)
        {
            var permission = await Permissions.RequestAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted) return null;

            var page = new ScanCodePage(title);
            await navigation.PushAsync(page);
            return await page._result.Task;
        }

        private ScanCodePage(string title)
        {
            Title = "Scan QR";

            _camera = new CameraBarcodeReaderView
            {
                IsDetecting = true,
                Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true, Multiple = false }
            };
            _camera.BarcodesDetected += (s, e) =>
            {
                var value = e.Results?.FirstOrDefault()?.Value;
                if (_done || string.IsNullOrWhiteSpace(value)) return;
                _done = true;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _camera.IsDetecting = false;
                    _result.TrySetResult(value);
                    await Navigation.PopAsync();
                });
            };

            var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
            grid.Add(new Label
            {
                Text = title,
                FontFamily = "MontserratBold",
                FontSize = 16,
                TextColor = Colors.White,
                BackgroundColor = Ui.Color("Primary"),
                Padding = new Thickness(16, 12),
                HorizontalTextAlignment = TextAlignment.Center
            }, 0, 0);
            grid.Add(_camera, 0, 1);
            Content = grid;
        }

        // Back without scanning
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _camera.IsDetecting = false;
            _result.TrySetResult(null);
        }
    }
}
