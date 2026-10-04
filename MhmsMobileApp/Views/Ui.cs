using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace MhmsMobileApp.Views
{
    // Coloured message boxes, same colours as the web app's banners
    public enum BannerKind { Info, Success, Warning, Danger }

    // ============================================================
    // Small helpers so every screen built in code uses the same
    // Michaelhouse look (Resources/Styles/Colors.xaml + Styles.xaml)
    // ============================================================
    public static class Ui
    {
        public static Color Color(string key) => (Color)Application.Current!.Resources[key];

        public static Style Style(string key) => (Style)Application.Current!.Resources[key];

        public static Label Text(string? text, string style = "BodyText")
        {
            return new Label { Text = text ?? "", Style = Style(style) };
        }

        // Playfair heading with the short red bar underneath (web: .heading-accent)
        public static View PageHeader(string title, string? subtitle = null)
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Children.Add(new Label { Text = title, Style = Style("PageTitle") });
            stack.Children.Add(new BoxView { Style = Style("HeadingAccent"), Margin = new Thickness(0, 10, 0, 0) });

            if (!string.IsNullOrEmpty(subtitle))
            {
                stack.Children.Add(new Label
                {
                    Text = subtitle,
                    Style = Style("MutedText"),
                    FontSize = 14,
                    Margin = new Thickness(0, 10, 0, 0)
                });
            }

            return stack;
        }

        public static Border Card(View content, Thickness? padding = null)
        {
            return new Border
            {
                Style = Style("Card"),
                Padding = padding ?? new Thickness(16),
                Content = content
            };
        }

        // Small rounded label, e.g. a status or a tag
        public static Border Chip(string text, string background, string foreground, double fontSize = 11)
        {
            return new Border
            {
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(background),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(10, 4),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = text,
                    FontFamily = "MontserratBold",
                    FontSize = fontSize,
                    TextColor = Microsoft.Maui.Graphics.Color.FromArgb(foreground)
                }
            };
        }

        public static Border Banner(BannerKind kind, string? title, string? body)
        {
            string bg, border, fg;
            switch (kind)
            {
                case BannerKind.Success: bg = "#F0FDF4"; border = "#BBF7D0"; fg = "#166534"; break;
                case BannerKind.Warning: bg = "#FFF7ED"; border = "#FED7AA"; fg = "#9A3412"; break;
                case BannerKind.Danger: bg = "#FEF2F2"; border = "#FECACA"; fg = "#991B1B"; break;
                default: bg = "#EFF6FF"; border = "#BFDBFE"; fg = "#1E40AF"; break;
            }

            var stack = new VerticalStackLayout { Spacing = 4 };

            if (!string.IsNullOrEmpty(title))
            {
                stack.Children.Add(new Label
                {
                    Text = title,
                    FontFamily = "MontserratBold",
                    FontSize = 14,
                    TextColor = Microsoft.Maui.Graphics.Color.FromArgb(fg)
                });
            }

            if (!string.IsNullOrEmpty(body))
            {
                stack.Children.Add(new Label
                {
                    Text = body,
                    FontSize = 13,
                    TextColor = Microsoft.Maui.Graphics.Color.FromArgb(fg)
                });
            }

            return new Border
            {
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(bg),
                Stroke = Microsoft.Maui.Graphics.Color.FromArgb(border),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 12),
                Content = stack
            };
        }

        // Big tappable choice (web: .rv-radio-tile). Call SetTileSelected
        // to show which one is chosen.
        public static Border Tile(string text, string? icon = null)
        {
            var stack = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            if (!string.IsNullOrEmpty(icon))
            {
                stack.Children.Add(new Label { Text = icon, FontSize = 26, HorizontalOptions = LayoutOptions.Center });
            }
            stack.Children.Add(new Label
            {
                Text = text,
                FontFamily = "MontserratBold",
                FontSize = 15,
                TextColor = Color("TextPrimary"),
                HorizontalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center
            });

            var tile = new Border
            {
                Style = Style("Card"),
                Padding = new Thickness(12, 14),
                MinimumHeightRequest = 56,
                Content = stack
            };
            SetTileSelected(tile, false);
            return tile;
        }

        public static void SetTileSelected(Border tile, bool selected)
        {
            tile.Stroke = selected ? Color("Primary") : Color("CardBorder");
            tile.StrokeThickness = selected ? 2 : 1;
            tile.BackgroundColor = selected ? Microsoft.Maui.Graphics.Color.FromArgb("#FEF2F2") : Color("CardBackground");
        }

        // A full-width tick-box row: the whole row can be tapped
        public static Grid CheckRow(string text, bool isChecked, Action<bool> changed, out CheckBox box)
        {
            var check = new CheckBox { IsChecked = isChecked, Color = Color("Primary"), VerticalOptions = LayoutOptions.Center };
            check.CheckedChanged += (s, e) => changed(e.Value);

            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                MinimumHeightRequest = 48,
                ColumnSpacing = 4
            };
            row.Add(check, 0, 0);
            row.Add(new Label { Text = text, FontSize = 15, TextColor = Color("TextPrimary"), VerticalOptions = LayoutOptions.Center }, 1, 0);
            row.OnTap(() => check.IsChecked = !check.IsChecked);

            box = check;
            return row;
        }

        // A text box inside the rounded Michaelhouse input border
        public static Border InputBox(View input)
        {
            return new Border { Style = Style("InputBox"), Content = input };
        }

        // Makes any view respond to a tap
        public static T OnTap<T>(this T view, Action action) where T : View
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => action();
            view.GestureRecognizers.Add(tap);
            return view;
        }
    }
}
