using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Report Asset Failure (any user)
    //
    // Same form as the web page Reporter/ReportFault: the broken item
    // (from the asset list, its QR sticker, or typed in), what the
    // problem is, how urgent, a description and an optional photo.
    // The server creates the job card and assigns a worker
    // (POST api/fault-reports). "My reports" lists earlier ones.
    // ============================================================
    public class ReportFaultPage : ContentPage
    {
        private const int PhotoSize = 1200;

        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly ScrollView _scroll = new ScrollView();
        private readonly ContentView _messageHost = new ContentView();

        private FaultFormDto? _form;
        private bool _manual;
        private AssetDto? _asset;
        private string? _priority;
        private byte[]? _photo;
        private bool _busy;

        // Form controls
        private Picker _assetPicker = new Picker();
        private readonly Label _assetChosen = new Label();
        private readonly VerticalStackLayout _listSection = new VerticalStackLayout { Spacing = 8 };
        private readonly VerticalStackLayout _manualSection = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        private readonly Entry _manualName = new Entry { Placeholder = "e.g. Broken window, leaking pipe", MaxLength = 200, FontSize = 16 };
        private readonly Entry _manualLocation = new Entry { Placeholder = "e.g. Founders House, Room 12", MaxLength = 200, FontSize = 16 };
        private readonly Picker _manualCategory = new Picker { Title = "-- Select Category --", FontSize = 16 };
        private readonly Entry _title = new Entry { Placeholder = "e.g. Burst pipe in bathroom", MaxLength = 200, FontSize = 16 };
        private readonly Editor _description = new Editor { Placeholder = "Describe exactly what you found...", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 90, MaxLength = 2000 };
        private readonly Dictionary<string, Border> _priorityTiles = new Dictionary<string, Border>();
        private readonly Label _priorityHelp = new Label();
        private readonly ContentView _photoHost = new ContentView();
        private readonly Button _submit = new Button { Text = "Submit Fault Report", Style = Ui.Style("PrimaryButton") };
        private readonly VerticalStackLayout _myReports = new VerticalStackLayout { Spacing = 10 };

        public ReportFaultPage()
        {
            Title = "Report Fault";
            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _scroll.Content = _root;
            Content = _scroll;

            Responsive.Adapt(this, _root);
            _submit.Clicked += async (s, e) => await SubmitAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_form == null) await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var form = await _api.GetFaultFormAsync();
            _root.Children.Clear();
            _root.Children.Add(Ui.PageHeader("Report a Fault", "Something broken? Tell maintenance and they'll fix it.", "Maintenance"));

            if (!form.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Can't load the form", form.Error));
                return;
            }

            _form = form;
            Build(form);
        }

        // ============================================================
        // FORM
        // ============================================================

        private void Build(FaultFormDto form)
        {
            _root.Children.Add(_messageHost);

            // ── 1. What is broken? ──
            var what = new VerticalStackLayout { Spacing = 8 };

            _assetPicker = new Picker { Title = "-- Select the broken asset --", FontSize = 16 };
            foreach (var a in form.Assets) _assetPicker.Items.Add(a.Label);
            _assetPicker.SelectedIndexChanged += (s, e) =>
            {
                if (_assetPicker.SelectedIndex < 0) return;
                SetAsset(form.Assets[_assetPicker.SelectedIndex]);
            };

            var scan = new Button { Text = "📷 Scan the asset's QR sticker", Style = Ui.Style("SecondaryButton") };
            scan.Clicked += async (s, e) => await ScanAssetAsync();

            _assetChosen.FontSize = 13;
            _assetChosen.TextColor = Ui.Color("Success");
            _assetChosen.IsVisible = false;

            _listSection.Children.Add(Ui.InputBox(_assetPicker));
            _listSection.Children.Add(scan);
            _listSection.Children.Add(_assetChosen);

            foreach (var c in form.Categories) _manualCategory.Items.Add(c);
            _manualSection.Children.Add(Label("Item name *"));
            _manualSection.Children.Add(Ui.InputBox(_manualName));
            _manualSection.Children.Add(Label("Where is it? *"));
            _manualSection.Children.Add(Ui.InputBox(_manualLocation));
            _manualSection.Children.Add(Label("Category"));
            _manualSection.Children.Add(Ui.InputBox(_manualCategory));

            what.Children.Add(_listSection);
            what.Children.Add(_manualSection);
            what.Children.Add(Ui.CheckRow("It's not on the list — I'll type it in", false, on =>
            {
                _manual = on;
                _listSection.IsVisible = !on;
                _manualSection.IsVisible = on;
            }, out _));

            _root.Children.Add(Section("What is broken? *", what));

            // ── 2. The problem ──
            var problem = new VerticalStackLayout { Spacing = 8 };
            problem.Children.Add(Label("What is the problem? *"));
            problem.Children.Add(Ui.InputBox(_title));
            problem.Children.Add(Label("Description *"));
            problem.Children.Add(Ui.InputBox(_description));
            _root.Children.Add(Section("The problem", problem));

            // ── 3. How urgent? (Emergency / High / Medium / Low) ──
            var tiles = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                ColumnSpacing = 10,
                RowSpacing = 10
            };
            for (int i = 0; i < form.Priorities.Count; i++)
            {
                var p = form.Priorities[i];
                var tile = Ui.Tile(p);
                tile.OnTap(() => SetPriority(p));
                _priorityTiles[p] = tile;
                tiles.Add(tile, i % 2, i / 2);
            }
            _priorityHelp.Style = Ui.Style("MutedText");
            var urgent = new VerticalStackLayout { Spacing = 8 };
            urgent.Children.Add(tiles);
            urgent.Children.Add(_priorityHelp);
            _root.Children.Add(Section("How urgent? *", urgent));

            // ── 4. Photo ──
            ShowPhoto();
            _root.Children.Add(Section("Photo (optional)", _photoHost));

            _root.Children.Add(_submit);

            // ── My reports ──
            _root.Children.Add(new Label { Text = "My reports", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 10, 0, 0) });
            _root.Children.Add(_myReports);
            ShowMyReports(form.MyReports);
        }

        private static Label Label(string text) =>
            new Label { Text = text, Style = Ui.Style("FieldLabel"), TextColor = Ui.Color("TextPrimary") };

        private static Border Section(string title, View content)
        {
            // Card with the web's black header bar
            return Ui.Section(title, content);
        }

        private void SetAsset(AssetDto asset)
        {
            _asset = asset;
            _assetChosen.Text = "✓ " + asset.Label;
            _assetChosen.IsVisible = true;
        }

        private async Task ScanAssetAsync()
        {
            var code = await ScanCodePage.ScanAsync(Navigation, "Point the camera at the asset's QR sticker");
            if (string.IsNullOrWhiteSpace(code)) return;

            var asset = await _api.GetAssetByQrAsync(code);
            if (!asset.Ok)
            {
                ShowMessage(BannerKind.Danger, asset.Error ?? "That QR code isn't an asset.");
                return;
            }

            SetAsset(asset);
            var index = _form?.Assets.FindIndex(a => a.Id == asset.Id) ?? -1;
            if (index >= 0) _assetPicker.SelectedIndex = index;
        }

        // Same guide as the web form
        private void SetPriority(string priority)
        {
            _priority = priority;
            foreach (var kv in _priorityTiles) Ui.SetTileSelected(kv.Value, kv.Key == priority);
            switch (priority)
            {
                case "Emergency": _priorityHelp.Text = "EMERGENCY — Fix within 1 HOUR. SMS alert will be sent."; break;
                case "High": _priorityHelp.Text = "HIGH — Must be fixed by end of today."; break;
                case "Medium": _priorityHelp.Text = "MEDIUM — Will be scheduled within 3 days."; break;
                default: _priorityHelp.Text = "LOW — Will be completed within this week."; break;
            }
        }

        private void ShowPhoto()
        {
            var stack = new VerticalStackLayout { Spacing = 10 };
            if (_photo != null)
            {
                var bytes = _photo;
                stack.Children.Add(new Border
                {
                    StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
                    HeightRequest = Responsive.Size(0.55, 180, 360),
                    Content = new Image { Source = ImageSource.FromStream(() => new MemoryStream(bytes)), Aspect = Aspect.AspectFill }
                });
                var remove = new Button { Text = "Remove photo", Style = Ui.Style("SecondaryButton") };
                remove.Clicked += (s, e) => { _photo = null; ShowPhoto(); };
                stack.Children.Add(remove);
            }
            else
            {
                var take = new Button { Text = "Take a Photo", Style = Ui.Style("SecondaryButton"), IsEnabled = PhotoHelper.CanTakePhoto };
                take.Clicked += async (s, e) => await TakePhotoAsync();
                stack.Children.Add(take);
            }
            _photoHost.Content = stack;
        }

        private async Task TakePhotoAsync()
        {
            try
            {
                var photo = await PhotoHelper.TakePhotoAsync(PhotoSize);
                if (photo == null) return;
                _photo = photo;
                ShowPhoto();
            }
            catch (PermissionException)
            {
                ShowMessage(BannerKind.Warning, "Allow Michaelhouse to use the camera (Android Settings → Apps → Michaelhouse → Permissions).");
            }
            catch (Exception ex)
            {
                ShowMessage(BannerKind.Warning, "Camera problem: " + ex.Message);
            }
        }

        // ============================================================
        // SUBMIT
        // ============================================================

        private async Task SubmitAsync()
        {
            if (_busy) return;

            // Same required fields as the web form (the server checks again)
            var missing = new List<string>();
            if (!_manual && _asset == null) missing.Add("choose the broken item (or tick \"not on the list\")");
            if (_manual && (string.IsNullOrWhiteSpace(_manualName.Text) || string.IsNullOrWhiteSpace(_manualLocation.Text)))
                missing.Add("type what the item is and where it is");
            if (string.IsNullOrWhiteSpace(_title.Text)) missing.Add("say what the problem is");
            if (string.IsNullOrWhiteSpace(_description.Text)) missing.Add("describe the problem");
            if (_priority == null) missing.Add("choose how urgent it is");
            if (missing.Count > 0)
            {
                ShowMessage(BannerKind.Danger, "Please " + string.Join(", ", missing) + ".");
                return;
            }

            _busy = true;
            _submit.IsEnabled = false;
            _submit.Text = "Sending...";

            var photo = _photo;
            var request = new FaultReportRequestDto
            {
                AssetId = _manual ? null : _asset?.Id,
                ManualAssetName = _manual ? _manualName.Text?.Trim() : null,
                ManualAssetLocation = _manual ? _manualLocation.Text?.Trim() : null,
                ManualCategory = _manual && _manualCategory.SelectedIndex >= 0 ? _manualCategory.Items[_manualCategory.SelectedIndex] : null,
                Title = _title.Text!.Trim(),
                Description = _description.Text!.Trim(),
                Priority = _priority!,
                PhotoBase64 = photo == null ? null : await Task.Run(() => Convert.ToBase64String(photo))
            };

            var result = await _api.SubmitFaultReportAsync(request);

            _busy = false;
            _submit.IsEnabled = true;
            _submit.Text = "Submit Fault Report";

            if (!result.Ok)
            {
                ShowMessage(BannerKind.Danger, result.Error ?? "The report wasn't sent.");
                return;
            }

            await DisplayAlert("Fault reported", result.Message, "OK");

            // Fresh form, with the new report in "My reports"
            _form = null;
            _asset = null;
            _priority = null;
            _photo = null;
            _manual = false;
            _root.Children.Clear();
            _root.Children.Add(new ActivityIndicator { IsRunning = true });
            await LoadAsync();
            await _scroll.ScrollToAsync(0, 0, false);
        }

        private async void ShowMessage(BannerKind kind, string message)
        {
            _messageHost.Content = Ui.Banner(kind, null, message);
            await _scroll.ScrollToAsync(0, 0, true);
        }

        private void ShowMyReports(List<FaultReportSummaryDto> reports)
        {
            _myReports.Children.Clear();
            if (reports.Count == 0)
            {
                _myReports.Children.Add(Ui.Text("You haven't reported any faults yet.", "MutedText"));
                return;
            }

            foreach (var r in reports)
            {
                var stack = new VerticalStackLayout { Spacing = 4 };
                var top = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
                top.Add(new Label { Text = r.Title, Style = Ui.Style("CardTitle") }, 0, 0);
                top.Add(StatusChip(r.Status), 1, 0);
                stack.Children.Add(top);
                stack.Children.Add(Ui.Text(r.JobReference + (string.IsNullOrEmpty(r.Asset) ? "" : " · " + r.Asset) + " · " + r.Priority, "MutedText"));
                stack.Children.Add(new Label
                {
                    Text = r.Reported + (r.AssignedTo == null ? "" : " · Assigned to " + r.AssignedTo),
                    Style = Ui.Style("MutedText"),
                    FontSize = 11
                });
                _myReports.Children.Add(Ui.Card(stack, new Thickness(14)));
            }
        }

        private static View StatusChip(string status)
        {
            switch (status)
            {
                case "Completed": return Ui.Chip(status, "#D1FAE5", "#065F46");
                case "Assigned":
                case "In Progress": return Ui.Chip(status, "#DBEAFE", "#1E40AF");
                default: return Ui.Chip(status, "#FEF3C7", "#92400E");
            }
        }
    }
}
