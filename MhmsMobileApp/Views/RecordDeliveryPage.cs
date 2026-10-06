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

            Responsive.Adapt(this, _root);
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
            _root.Children.Add(Ui.PageHeader("Record Delivery", "Choose the purchase order the delivery is for.", "Logistics & Supply"));

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

            // One card per order; 2–3 per row on wide screens
            var grid = new AdaptiveGrid(320);
            foreach (var order in result.Orders)
            {
                grid.Children.Add(OrderCard(order));
            }
            _root.Children.Add(grid);

            _root.Children.Add(Ui.Text("A delivery that has no purchase order is recorded on the Michaelhouse website.", "MutedText"));
        }

        private View OrderCard(OpenOrderDto order)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };

            // Web order list: PO in mono, supplier in serif capitals, status badge
            var title = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            title.Add(new Label { Text = order.PoNumber, FontFamily = "MonoSemiBold", FontSize = 14, TextColor = Ui.Color("TextPrimary"), VerticalOptions = LayoutOptions.Center }, 0, 0);
            title.Add(Ui.OrderStatus(order.Status), 1, 0);
            stack.Children.Add(title);

            stack.Children.Add(new Label { Text = order.Supplier, Style = Ui.Style("CardTitle"), CharacterSpacing = 1, TextTransform = TextTransform.Uppercase });

            if (!string.IsNullOrEmpty(order.RequestedDelivery))
            {
                stack.Children.Add(Ui.Mono("Requested delivery · " + FormatDate(order.RequestedDelivery), caps: true));
            }

            if (!string.IsNullOrEmpty(order.TestInvoiceNumber))
            {
                stack.Children.Add(Ui.Mono("Test invoice · " + order.TestInvoiceNumber, caps: true));
            }

            stack.Children.Add(Ui.Divider());

            // Same summary as the web page: what's still outstanding
            foreach (var line in order.Lines.Where(l => l.Outstanding > 0m))
            {
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
                row.Add(Ui.Text(line.Name), 0, 0);
                row.Add(new Label { Text = line.Outstanding.ToString("0.###", CultureInfo.CurrentCulture) + " " + line.Unit, FontFamily = "MonoSemiBold", FontSize = 13, TextColor = Ui.Color("TextPrimary"), VerticalOptions = LayoutOptions.Center }, 1, 0);
                stack.Children.Add(row);
            }

            var receive = new Button { Text = "Receive", Style = Ui.Style("PrimaryButton"), Margin = new Thickness(0, 6, 0, 0) };
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
