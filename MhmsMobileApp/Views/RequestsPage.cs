using Microsoft.Maui.Controls;
using System;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Student — "Requests": the things a student asks for on mobile.
    // Each opens its own screen; approval stays on the website.
    // ============================================================
    public class RequestsPage : ContentPage
    {
        public RequestsPage()
        {
            Title = "Requests";

            var root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
            root.Children.Add(Ui.PageHeader("Requests", "Ask for permission, or report something broken."));

            root.Children.Add(Item("🛠", "Report a Fault", "Something broken in your residence or around school.", () => new ReportFaultPage()));

            Content = new ScrollView { Content = root };
        }

        private View Item(string icon, string title, string description, Func<Page> open)
        {
            var text = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            text.Children.Add(new Label { Text = title, Style = Ui.Style("CardTitle"), FontSize = 17 });
            text.Children.Add(Ui.Text(description, "MutedText"));

            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 14,
                MinimumHeightRequest = 72
            };
            row.Add(new Label { Text = icon, FontSize = 30, VerticalOptions = LayoutOptions.Center }, 0, 0);
            row.Add(text, 1, 0);
            row.Add(new Label { Text = "›", FontSize = 30, TextColor = Ui.Color("Primary"), VerticalOptions = LayoutOptions.Center }, 2, 0);

            var card = Ui.Card(row);
            card.OnTap(async () => await Navigation.PushAsync(open()));
            return card;
        }
    }
}
