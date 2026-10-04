using Microsoft.Maui.Controls;

namespace MhmsMobileApp.Views
{
    // Placeholder for screens that are not built yet
    public class ComingSoonPage : ContentPage
    {
        public ComingSoonPage(string title)
        {
            Title = title;

            Content = new VerticalStackLayout
            {
                Padding = 32,
                Spacing = 12,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Image
                    {
                        Source = "mh_crest.png",
                        HeightRequest = 90,
                        WidthRequest = 90,
                        Opacity = 0.35,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = title,
                        Style = (Style)Application.Current!.Resources["PageTitle"],
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "Coming soon",
                        Style = (Style)Application.Current!.Resources["FieldLabel"],
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            };
        }
    }
}
