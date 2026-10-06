using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace MhmsMobileApp.Views
{
    // Coloured message boxes, same colours as the web app's notes
    public enum BannerKind { Info, Success, Warning, Danger }

    // Badge colours, as the web's status badges
    // (inv-badge muted / info / low / ok / bad; red-50 + #C21E2E)
    public enum Tone { Muted, Info, Low, Ok, Bad, Brand, Dark }

    // ============================================================
    // Small helpers so every screen built in code uses the same
    // Michaelhouse look as the web app's redesign
    // (Resources/Styles/Colors.xaml + Styles.xaml):
    // square cards, red-bar eyebrow + Playfair title, black section
    // bars, square uppercase badges, mono figures.
    //
    // Layout that adapts to the screen: see Responsive (page margins
    // and maximum width) and AdaptiveGrid (1–3 columns).
    // ============================================================
    public static class Ui
    {
        public static Color Color(string key) => (Color)Application.Current!.Resources[key];

        public static Style Style(string key) => (Style)Application.Current!.Resources[key];

        public static Label Text(string? text, string style = "BodyText")
        {
            return new Label { Text = text ?? "", Style = Style(style) };
        }

        // Small mono text (codes, dates, units); caps = the web's
        // "font-mono uppercase tracking-widest" meta lines
        public static Label Mono(string? text, bool caps = false)
        {
            return new Label { Text = text ?? "", Style = Style(caps ? "MonoCaps" : "MonoText") };
        }

        // Web page header: red bar + small grey capitals, a large
        // Playfair title, then a small uppercase mono subtitle and a
        // thin rule underneath
        public static View PageHeader(string title, string? subtitle = null, string? eyebrow = null)
        {
            var stack = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(0, 0, 0, 4) };

            var eyebrowRow = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 0, 0, 10) };
            eyebrowRow.Children.Add(new BoxView { Style = Style("HeadingAccent") });
            if (!string.IsNullOrEmpty(eyebrow))
            {
                eyebrowRow.Children.Add(new Label { Text = eyebrow, Style = Style("Eyebrow") });
            }
            stack.Children.Add(eyebrowRow);

            stack.Children.Add(new Label { Text = title, Style = Style("PageTitle") });

            if (!string.IsNullOrEmpty(subtitle))
            {
                stack.Children.Add(new Label
                {
                    Text = subtitle,
                    Style = Style("PageSubtitle"),
                    Margin = new Thickness(0, 10, 0, 0)
                });
            }

            stack.Children.Add(new BoxView
            {
                Color = Color("CardBorder"),
                BackgroundColor = Color("CardBorder"),
                HeightRequest = 1,
                Margin = new Thickness(0, 16, 0, 0)
            });

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

        // Card with the web's black header bar: "TITLE ······ meta"
        public static Border Section(string title, View content, string? meta = null, Thickness? padding = null)
        {
            var bar = new Grid
            {
                BackgroundColor = Color("SectionBar"),
                Padding = new Thickness(16, 12),
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 10
            };
            bar.Add(new Label { Text = title, Style = Style("SectionBarText"), LineBreakMode = LineBreakMode.TailTruncation }, 0, 0);

            if (!string.IsNullOrEmpty(meta))
            {
                bar.Add(new Label
                {
                    Text = meta,
                    Style = Style("MonoCaps"),
                    TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#B3FFFFFF"),
                    VerticalOptions = LayoutOptions.Center
                }, 1, 0);
            }

            var body = new ContentView { Padding = padding ?? new Thickness(16), Content = content };

            return new Border
            {
                Style = Style("Card"),
                Padding = 0,
                Content = new VerticalStackLayout { Spacing = 0, Children = { bar, body } }
            };
        }

        // Stat tile (web: label in tiny capitals, big mono figure)
        public static Border Stat(string label, string value, string? note = null, string? valueColor = null)
        {
            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(new Label { Text = label, Style = Style("FieldLabel") });

            var figure = new Label { Text = value, Style = Style("StatValue") };
            if (valueColor != null) figure.TextColor = Microsoft.Maui.Graphics.Color.FromArgb(valueColor);
            stack.Children.Add(figure);

            if (!string.IsNullOrEmpty(note)) stack.Children.Add(Mono(note, caps: true));

            return Card(stack, new Thickness(16, 14));
        }

        // Square uppercase badge with a thin border (web: text-[9px]
        // font-bold uppercase tracking-widest px-2 py-0.5 border)
        public static Border Chip(string text, string background, string foreground, double fontSize = 10)
        {
            var fg = Microsoft.Maui.Graphics.Color.FromArgb(foreground);
            return new Border
            {
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(background),
                Stroke = fg.WithAlpha(0.45f),
                StrokeThickness = 1,
                StrokeShape = new Rectangle(),
                Padding = new Thickness(8, 3),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = text,
                    FontFamily = "MontserratBold",
                    FontSize = fontSize,
                    CharacterSpacing = 1.2,
                    TextTransform = TextTransform.Uppercase,
                    TextColor = fg
                }
            };
        }

        public static Border Badge(string text, Tone tone)
        {
            switch (tone)
            {
                case Tone.Info: return Chip(text, "#EFF6FF", "#1E40AF");
                case Tone.Low: return Chip(text, "#FEFCE8", "#A16207");
                case Tone.Ok: return Chip(text, "#ECFDF5", "#047857");
                case Tone.Bad: return Chip(text, "#FEF2F2", "#991B1B");
                case Tone.Brand: return Chip(text, "#FEF2F2", "#C21E2E");
                case Tone.Dark: return Chip(text, "#1A1A1A", "#FFFFFF");
                default: return Chip(text, "#F3F4F6", "#4B5563");
            }
        }

        // Order status as on the web (_OrderStatusBadge)
        public static Border OrderStatus(string status)
        {
            switch (status)
            {
                case "Pending": return Badge("Pending (sent)", Tone.Info);
                case "Confirmed": return Badge("Confirmed", Tone.Info);
                case "PartiallyFulfilled": return Badge("Partially fulfilled", Tone.Low);
                case "Fulfilled": return Badge("Fulfilled", Tone.Ok);
                case "Cancelled": return Badge("Cancelled", Tone.Bad);
                default: return Badge(status, Tone.Muted);
            }
        }

        // Kitchen station (Kitchen Plan, Feast Plan)
        public static Border StationBadge(string? station)
        {
            switch (station)
            {
                case "Grill": return Badge(station, Tone.Bad);
                case "Stove": return Badge(station, Tone.Info);
                case "Oven": return Badge(station, Tone.Low);
                case "Cold Prep": return Badge(station, Tone.Ok);
                default: return Badge(string.IsNullOrEmpty(station) ? "Line" : station, Tone.Muted);
            }
        }

        // "Name ········ amount" row with an optional shortfall badge
        // (ingredient lists on the Kitchen / Feast Plan)
        public static View AmountRow(string name, string amount, bool ok = true, string? shortText = null)
        {
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 10,
                Padding = new Thickness(0, 8)
            };
            row.Add(Text(name), 0, 0);

            var right = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.End };
            right.Children.Add(new Label
            {
                Text = amount,
                FontFamily = "MonoSemiBold",
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.End,
                TextColor = ok ? Color("TextPrimary") : Color("Primary")
            });
            if (!string.IsNullOrEmpty(shortText)) right.Children.Add(Badge(shortText, Tone.Brand));
            row.Add(right, 1, 0);

            return new VerticalStackLayout { Spacing = 0, Children = { row, Divider() } };
        }

        // Note with a coloured bar on the left (web: bg-*-50 border-l-4 p-4)
        public static Border Banner(BannerKind kind, string? title, string? body)
        {
            string bg, bar, fg;
            switch (kind)
            {
                case BannerKind.Success: bg = "#F0FDF4"; bar = "#16A34A"; fg = "#166534"; break;
                case BannerKind.Warning: bg = "#FFFBEB"; bar = "#EAB308"; fg = "#854D0E"; break;
                case BannerKind.Danger: bg = "#FEF2F2"; bar = "#C21E2E"; fg = "#991B1B"; break;
                default: bg = "#F9FAFB"; bar = "#1A1A1A"; fg = "#374151"; break;
            }

            var stack = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(14, 12), VerticalOptions = LayoutOptions.Center };

            if (!string.IsNullOrEmpty(title))
            {
                stack.Children.Add(new Label
                {
                    Text = title,
                    FontFamily = "MontserratBold",
                    FontSize = 11,
                    CharacterSpacing = 1.5,
                    TextTransform = TextTransform.Uppercase,
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

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(new GridLength(4)), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 0
            };
            grid.Add(new BoxView { Color = Microsoft.Maui.Graphics.Color.FromArgb(bar), BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(bar) }, 0, 0);
            grid.Add(stack, 1, 0);

            return new Border
            {
                BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb(bg),
                StrokeThickness = 0,
                StrokeShape = new Rectangle(),
                Padding = 0,
                Content = grid
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
                FontSize = 12,
                CharacterSpacing = 1.5,
                TextTransform = TextTransform.Uppercase,
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
            row.Add(new Label { Text = text, Style = Style("BodyText"), TextColor = Color("TextPrimary"), VerticalOptions = LayoutOptions.Center }, 1, 0);
            row.OnTap(() => check.IsChecked = !check.IsChecked);

            box = check;
            return row;
        }

        // A text box inside the square Michaelhouse input border
        public static Border InputBox(View input)
        {
            return new Border { Style = Style("InputBox"), Content = input };
        }

        // A thin divider between rows (web: border-b border-gray-50)
        public static BoxView Divider()
        {
            return new BoxView { Color = Color("Divider"), BackgroundColor = Color("Divider"), HeightRequest = 1 };
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

    // ============================================================
    // Fitting the screen
    //
    // Responsive.Adapt(page, root): the page's content keeps a
    // comfortable maximum width, centred on tablets / landscape, with
    // side margins that grow with the screen (16 → 24 → 32).
    // Responsive.Square(view, page, …): a QR code / camera preview
    // sized as a share of the screen width, within limits.
    // ============================================================
    public static class Responsive
    {
        public const double MaxContentWidth = 1040;

        public static double Gutter(double width)
        {
            return width < 600 ? 16 : width < 900 ? 24 : 32;
        }

        public static bool IsWide(double width)
        {
            return width >= 700;
        }

        public static void Adapt(Page page, Layout root, double top = 20, double bottom = 28)
        {
            void Apply()
            {
                double width = page.Width;
                if (width <= 0) return;

                double side = Math.Max(Gutter(width), (width - MaxContentWidth) / 2);
                var padding = new Thickness(side, top, side, bottom);
                if (root.Padding != padding) root.Padding = padding;

                if (root is VerticalStackLayout stack)
                {
                    double spacing = IsWide(width) ? 18 : 14;
                    if (stack.Spacing != spacing) stack.Spacing = spacing;
                }
            }

            page.SizeChanged += (s, e) => Apply();
            Apply();
        }

        // Screen width in device-independent units (at the moment of the call)
        public static double ScreenWidth
        {
            get
            {
                var info = DeviceDisplay.Current.MainDisplayInfo;
                return info.Density > 0 ? info.Width / info.Density : info.Width;
            }
        }

        // A size from the screen width, between min and max — for views
        // built without a page to measure (e.g. photo previews)
        public static double Size(double fraction, double min, double max)
        {
            double width = ScreenWidth;
            double usable = Math.Min(width, MaxContentWidth) - 2 * Gutter(width);
            return Math.Max(min, Math.Min(max, usable * fraction));
        }

        // fraction of the page width, between min and max
        public static void Square(View view, Page page, double fraction, double min, double max, double? aspect = null)
        {
            void Apply()
            {
                double width = page.Width;
                if (width <= 0) return;

                double usable = Math.Min(width, MaxContentWidth) - 2 * Gutter(width);
                double size = Math.Max(min, Math.Min(max, usable * fraction));
                view.WidthRequest = aspect.HasValue ? -1 : size;
                view.HeightRequest = aspect.HasValue ? size / aspect.Value : size;
            }

            // Views like photo previews come and go: stop listening once removed
            EventHandler handler = (s, e) => Apply();
            page.SizeChanged += handler;
            view.Unloaded += (s, e) => page.SizeChanged -= handler;
            Apply();
        }
    }

    // ============================================================
    // Cards in 1, 2 or 3 columns, depending on the width available:
    // as many columns as fit at MinItemWidth (up to MaxColumns).
    // Phone portrait → 1, landscape / tablet → 2–3. Items in a row
    // share the row's height so cards line up like the web's grids.
    // ============================================================
    public class AdaptiveGrid : Layout
    {
        public static readonly BindableProperty MinItemWidthProperty =
            BindableProperty.Create(nameof(MinItemWidth), typeof(double), typeof(AdaptiveGrid), 320d, propertyChanged: Changed);

        public static readonly BindableProperty MaxColumnsProperty =
            BindableProperty.Create(nameof(MaxColumns), typeof(int), typeof(AdaptiveGrid), 3, propertyChanged: Changed);

        public static readonly BindableProperty ColumnSpacingProperty =
            BindableProperty.Create(nameof(ColumnSpacing), typeof(double), typeof(AdaptiveGrid), 12d, propertyChanged: Changed);

        public static readonly BindableProperty RowSpacingProperty =
            BindableProperty.Create(nameof(RowSpacing), typeof(double), typeof(AdaptiveGrid), 12d, propertyChanged: Changed);

        public double MinItemWidth { get => (double)GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }
        public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
        public double ColumnSpacing { get => (double)GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }
        public double RowSpacing { get => (double)GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

        public AdaptiveGrid() { }

        public AdaptiveGrid(double minItemWidth, int maxColumns = 3)
        {
            MinItemWidth = minItemWidth;
            MaxColumns = maxColumns;
        }

        public int ColumnsFor(double width)
        {
            if (double.IsInfinity(width) || width <= 0) return 1;
            int fit = (int)Math.Floor((width + ColumnSpacing) / (MinItemWidth + ColumnSpacing));
            return Math.Max(1, Math.Min(Math.Max(1, MaxColumns), fit));
        }

        protected override ILayoutManager CreateLayoutManager() => new AdaptiveGridManager(this);

        private static void Changed(BindableObject b, object o, object n) => ((AdaptiveGrid)b).InvalidateMeasure();
    }

    internal class AdaptiveGridManager : LayoutManager
    {
        private readonly AdaptiveGrid _grid;
        private readonly List<double> _rowHeights = new List<double>();
        private int _columns = 1;
        private double _columnWidth;

        public AdaptiveGridManager(AdaptiveGrid grid) : base(grid)
        {
            _grid = grid;
        }

        private List<IView> Visible()
        {
            return _grid.Where(c => c.Visibility != Visibility.Collapsed).ToList();
        }

        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = _grid.Padding;
            double available = widthConstraint - padding.HorizontalThickness;
            if (double.IsInfinity(available)) available = _grid.MinItemWidth;

            _columns = _grid.ColumnsFor(available);
            _columnWidth = Math.Max(0, (available - (_columns - 1) * _grid.ColumnSpacing) / _columns);
            _rowHeights.Clear();

            var children = Visible();
            for (int i = 0; i < children.Count; i += _columns)
            {
                double rowHeight = 0;
                for (int j = i; j < Math.Min(i + _columns, children.Count); j++)
                {
                    var size = children[j].Measure(_columnWidth, double.PositiveInfinity);
                    rowHeight = Math.Max(rowHeight, size.Height);
                }
                _rowHeights.Add(rowHeight);
            }

            double height = _rowHeights.Sum() + Math.Max(0, _rowHeights.Count - 1) * _grid.RowSpacing + padding.VerticalThickness;
            double width = double.IsInfinity(widthConstraint) ? available + padding.HorizontalThickness : widthConstraint;
            return new Size(width, height);
        }

        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = _grid.Padding;
            var children = Visible();

            // Arrange can come with a different width than Measure saw
            double available = bounds.Width - padding.HorizontalThickness;
            if (_grid.ColumnsFor(available) != _columns || _rowHeights.Count * _columns < children.Count)
            {
                Measure(bounds.Width, bounds.Height);
            }

            double y = bounds.Y + padding.Top;
            for (int row = 0; row * _columns < children.Count; row++)
            {
                double rowHeight = row < _rowHeights.Count ? _rowHeights[row] : 0;
                for (int col = 0; col < _columns; col++)
                {
                    int index = row * _columns + col;
                    if (index >= children.Count) break;

                    double x = bounds.X + padding.Left + col * (_columnWidth + _grid.ColumnSpacing);
                    children[index].Arrange(new Rect(x, y, _columnWidth, rowHeight));
                }
                y += rowHeight + _grid.RowSpacing;
            }

            return bounds.Size;
        }
    }
}
