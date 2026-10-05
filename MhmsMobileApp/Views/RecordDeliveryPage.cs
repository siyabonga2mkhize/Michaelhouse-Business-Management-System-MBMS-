using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.Linq;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Record Stock Delivery — orders awaiting delivery
    // (Cafeteria Manager / Admin; web: IngredientDelivery/Record)
    // Tap Receive to record a delivery for that order
    // (ReceiveDeliveryPage).
    // ============================================================
    public class RecordDeliveryPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly RefreshView _refresh = new RefreshView();
        private bool _loading;

        public RecordDeliveryPage()
        {
            Title = "Record Delivery";

            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _refresh.Content = new ScrollView { Content = _root };
            _refresh.Refreshing += async (s, e) =>
            {
                await LoadAsync();
                _refresh.IsRefreshing = false;
            };
            Content = _refresh;
        }

        // Reload every time, so a recorded delivery drops off the list
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;

            var result = await _api.GetOpenOrdersAsync();
            _loading = false;

            _root.Children.Clear();
            _root.Children.Add(Ui.PageHeader("Record Delivery", "Choose the purchase order the delivery is for."));

            if (!result.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Orders not available", result.Error));
                return;
            }

            _root.Children.Add(new Label { Text = "Orders awaiting delivery", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 6, 0, 0) });

            if (result.Orders.Count == 0)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Info, null, "No orders are waiting for a delivery."));
            }

            foreach (var order in result.Orders)
            {
                _root.Children.Add(OrderCard(order));
            }

            _root.Children.Add(Ui.Text("A delivery that has no purchase order is recorded on the Michaelhouse website.", "MutedText"));
        }

        private View OrderCard(OpenOrderDto order)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };

            var title = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            title.Add(new Label { Text = order.PoNumber, FontFamily = "PlayfairBold", FontSize = 20, TextColor = Ui.Color("TextPrimary") }, 0, 0);
            title.Add(Ui.Chip(StatusLabel(order.Status), "#FEF3C7", "#92400E"), 1, 0);
            stack.Children.Add(title);

            stack.Children.Add(new Label { Text = order.Supplier, Style = Ui.Style("CardTitle"), FontSize = 15 });

            if (!string.IsNullOrEmpty(order.RequestedDelivery))
            {
                stack.Children.Add(Ui.Text("Requested delivery: " + FormatDate(order.RequestedDelivery), "MutedText"));
            }

            // Same summary as the web page: what's still outstanding
            var outstanding = order.Lines.Where(l => l.Outstanding > 0m)
                .Select(l => l.Name + " " + l.Outstanding.ToString("0.###", CultureInfo.CurrentCulture) + " " + l.Unit);
            stack.Children.Add(new Label { Text = string.Join(", ", outstanding), FontSize = 13, TextColor = Ui.Color("Gray600") });

            var receive = new Button { Text = "Receive", Style = Ui.Style("PrimaryButton"), Margin = new Thickness(0, 4, 0, 0) };
            receive.Clicked += async (s, e) => await Navigation.PushAsync(new ReceiveDeliveryPage(order.Id));
            stack.Children.Add(receive);

            return Ui.Card(stack);
        }

        private static string StatusLabel(string status)
        {
            switch (status)
            {
                case "Pending": return "Sent";
                case "PartiallyFulfilled": return "Partly received";
                default: return status;
            }
        }

        private static string FormatDate(string isoDate)
        {
            DateTime d;
            return DateTime.TryParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                ? d.ToString("ddd dd MMM yyyy")
                : isoDate;
        }
    }
}
