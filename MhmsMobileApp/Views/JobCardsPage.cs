using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Maintenance Worker — my job cards (web: Worker/MyJobs)
    // Open jobs first (emergencies, then by due date), then the last
    // few completed. Tap a job to complete it (CompleteJobPage).
    // ============================================================
    public class JobCardsPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly RefreshView _refresh = new RefreshView();
        private bool _loading;

        public JobCardsPage()
        {
            Title = "Job Cards";
            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _refresh.Content = new ScrollView { Content = _root };
            _refresh.Refreshing += async (s, e) =>
            {
                await LoadAsync();
                _refresh.IsRefreshing = false;
            };
            Content = _refresh;
        }

        // Reload every time, so a completed job moves to "Completed"
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;
            var result = await _api.GetWorkerJobsAsync();
            _loading = false;

            _root.Children.Clear();

            if (!result.Ok)
            {
                _root.Children.Add(Ui.PageHeader("My Job Cards"));
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Jobs not available", result.Error));
                return;
            }

            _root.Children.Add(Ui.PageHeader("My Job Cards", result.Worker + (string.IsNullOrEmpty(result.Skill) ? "" : " · " + result.Skill)));

            _root.Children.Add(new Label { Text = "Open jobs", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 6, 0, 0) });
            if (result.OpenJobs.Count == 0)
                _root.Children.Add(Ui.Banner(BannerKind.Success, null, "No open jobs. Well done!"));
            foreach (var job in result.OpenJobs)
                _root.Children.Add(JobCard(job, true));

            if (result.CompletedJobs.Count > 0)
            {
                _root.Children.Add(new Label { Text = "Recently completed", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 10, 0, 0) });
                foreach (var job in result.CompletedJobs)
                    _root.Children.Add(JobCard(job, false));
            }
        }

        private View JobCard(JobSummaryDto job, bool open)
        {
            var stack = new VerticalStackLayout { Spacing = 6 };

            var top = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            top.Add(new Label { Text = job.Title, Style = Ui.Style("CardTitle") }, 0, 0);
            top.Add(PriorityChip(job.Priority), 1, 0);
            stack.Children.Add(top);

            stack.Children.Add(Ui.Text(job.JobReference + (string.IsNullOrEmpty(job.Asset) ? "" : " · " + job.Asset), "MutedText"));

            if (open)
            {
                var due = new Label
                {
                    Text = (job.Overdue ? "⚠ Overdue — was due " : "Due ") + job.Due,
                    FontSize = 13,
                    FontFamily = job.Overdue ? "MontserratBold" : "Montserrat",
                    TextColor = job.Overdue ? Ui.Color("Danger") : Ui.Color("Gray600")
                };
                if (job.Due != null) stack.Children.Add(due);

                var button = new Button { Text = "Open & Complete", Style = Ui.Style("PrimaryButton") };
                button.Clicked += async (s, e) => await Navigation.PushAsync(new CompleteJobPage(job.Id));
                stack.Children.Add(button);
            }
            else
            {
                stack.Children.Add(Ui.Text("Completed " + job.Completed + " · " + job.FinalCondition
                    + (job.TotalCost.HasValue ? " · R" + job.TotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture) : ""), "MutedText"));
            }

            return Ui.Card(stack);
        }

        public static View PriorityChip(string priority)
        {
            switch (priority)
            {
                case "Emergency": return Ui.Chip(priority, "#C21E2E", "#FFFFFF");
                case "High": return Ui.Chip(priority, "#FEF3C7", "#92400E");
                case "Medium": return Ui.Chip(priority, "#DBEAFE", "#1E40AF");
                default: return Ui.Chip(priority, "#D1FAE5", "#065F46");
            }
        }
    }

    // ============================================================
    // Complete one job (web: Worker/JobDetail + CompleteJob)
    // ============================================================
    public class CompleteJobPage : ContentPage
    {
        private const int PhotoSize = 1200;

        private readonly ApiService _api = new ApiService();
        private readonly int _jobId;
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly ScrollView _scroll = new ScrollView();
        private readonly ContentView _messageHost = new ContentView();
        private readonly ContentView _photoHost = new ContentView();
        private readonly VerticalStackLayout _partsList = new VerticalStackLayout { Spacing = 8 };
        private readonly Editor _notes = new Editor { Placeholder = "What did you do?", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 80, MaxLength = 2000 };
        private readonly Entry _labour = new Entry { Text = "0", Keyboard = Keyboard.Numeric, FontSize = 16 };
        private readonly Label _costLabel = new Label { FontFamily = "MontserratBold", FontSize = 15 };
        private readonly Button _complete = new Button { Text = "Complete Job", Style = Ui.Style("PrimaryButton") };
        private readonly Dictionary<string, Border> _conditionTiles = new Dictionary<string, Border>();
        private readonly List<(InventoryItemDto Item, Entry Qty)> _parts = new List<(InventoryItemDto, Entry)>();

        private JobDetailDto? _detail;
        private string? _condition;
        private byte[]? _photo;
        private bool _busy;

        public CompleteJobPage(int jobId)
        {
            _jobId = jobId;
            Title = "Complete Job";
            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _scroll.Content = _root;
            Content = _scroll;

            _labour.TextChanged += (s, e) => UpdateCost();
            _complete.Clicked += async (s, e) => await CompleteAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_detail == null) await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var detail = await _api.GetWorkerJobAsync(_jobId);
            _root.Children.Clear();

            if (!detail.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Job not available", detail.Error));
                return;
            }

            _detail = detail;
            var job = detail.Job;

            _root.Children.Add(Ui.PageHeader(job.Title, job.JobReference));
            _root.Children.Add(_messageHost);

            // ── The fault ──
            var fault = new VerticalStackLayout { Spacing = 6 };
            var chips = new HorizontalStackLayout { Spacing = 8 };
            chips.Children.Add(JobCardsPage.PriorityChip(job.Priority));
            if (job.Overdue) chips.Children.Add(Ui.Chip("Overdue", "#FEE2E2", "#991B1B"));
            fault.Children.Add(chips);
            if (!string.IsNullOrEmpty(job.Asset)) fault.Children.Add(new Label { Text = job.Asset, Style = Ui.Style("CardTitle") });
            if (!string.IsNullOrEmpty(detail.Location)) fault.Children.Add(Ui.Text("📍 " + detail.Location, "MutedText"));
            if (!string.IsNullOrEmpty(detail.Description)) fault.Children.Add(Ui.Text(detail.Description));
            if (job.Due != null) fault.Children.Add(Ui.Text("Due " + job.Due, "MutedText"));

            if (!string.IsNullOrEmpty(detail.PhotoBeforeBase64))
            {
                var bytes = await Task.Run(() => Convert.FromBase64String(detail.PhotoBeforeBase64));
                fault.Children.Add(new Label { Text = "Before repair", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 6, 0, 0) });
                fault.Children.Add(PhotoView(bytes));
            }
            _root.Children.Add(Section("The fault", fault));

            // ── 1. Repair photo (required) ──
            ShowPhoto();
            _root.Children.Add(Section("Repair photo proof *", _photoHost));

            // ── 2. Final condition ──
            var tiles = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
            for (int i = 0; i < detail.FinalConditions.Count; i++)
            {
                tiles.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var c = detail.FinalConditions[i];
                var tile = Ui.Tile(c);
                tile.OnTap(() =>
                {
                    _condition = c;
                    foreach (var kv in _conditionTiles) Ui.SetTileSelected(kv.Value, kv.Key == c);
                });
                _conditionTiles[c] = tile;
                tiles.Add(tile, i, 0);
            }
            _root.Children.Add(Section("Final condition *", tiles));

            // ── 3. Notes ──
            _root.Children.Add(Section("Completion notes", Ui.InputBox(_notes)));

            // ── 4. Parts used + labour ──
            var partsStack = new VerticalStackLayout { Spacing = 10 };
            partsStack.Children.Add(_partsList);
            var addPart = new Button { Text = "+ Add a part from stock", Style = Ui.Style("SecondaryButton"), IsEnabled = detail.Inventory.Count > 0 };
            addPart.Clicked += async (s, e) => await AddPartAsync();
            partsStack.Children.Add(addPart);
            partsStack.Children.Add(new Label { Text = "Labour cost (R)", FontFamily = "MontserratBold", FontSize = 13, TextColor = Ui.Color("TextPrimary"), Margin = new Thickness(0, 6, 0, 0) });
            partsStack.Children.Add(Ui.InputBox(_labour));
            _costLabel.TextColor = Ui.Color("TextPrimary");
            partsStack.Children.Add(_costLabel);
            UpdateCost();
            _root.Children.Add(Section("Parts & cost", partsStack));

            _root.Children.Add(_complete);
        }

        private static Border Section(string title, View content)
        {
            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(new Label { Text = title, Style = Ui.Style("FieldLabel") });
            stack.Children.Add(content);
            return Ui.Card(stack);
        }

        private static View PhotoView(byte[] bytes)
        {
            return new Border
            {
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                HeightRequest = 200,
                Content = new Image { Source = ImageSource.FromStream(() => new MemoryStream(bytes)), Aspect = Aspect.AspectFill }
            };
        }

        private void ShowPhoto()
        {
            var stack = new VerticalStackLayout { Spacing = 10 };
            if (_photo != null)
            {
                stack.Children.Add(PhotoView(_photo));
                var retake = new Button { Text = "Retake photo", Style = Ui.Style("SecondaryButton") };
                retake.Clicked += async (s, e) => await TakePhotoAsync();
                stack.Children.Add(retake);
            }
            else
            {
                var take = new Button { Text = "📷 Take Repair Photo", Style = Ui.Style("SecondaryButton"), IsEnabled = PhotoHelper.CanTakePhoto };
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
            catch (Microsoft.Maui.ApplicationModel.PermissionException)
            {
                ShowMessage(BannerKind.Warning, "Allow Michaelhouse to use the camera (Android Settings → Apps → Michaelhouse → Permissions).");
            }
            catch (Exception ex)
            {
                ShowMessage(BannerKind.Warning, "Camera problem: " + ex.Message);
            }
        }

        // ── Parts ──
        private async Task AddPartAsync()
        {
            if (_detail == null) return;
            var labels = _detail.Inventory
                .Select(i => i.Name + " (" + i.Stock + " " + i.Unit + " · R" + i.UnitCost.ToString("0.00", CultureInfo.InvariantCulture) + ")")
                .ToArray();
            var choice = await DisplayActionSheet("Choose a part", "Cancel", null, labels);
            int index = Array.IndexOf(labels, choice);
            if (index < 0) return;

            var item = _detail.Inventory[index];
            var qty = new Entry { Text = "1", Keyboard = Keyboard.Numeric, FontSize = 16, WidthRequest = 70, HorizontalTextAlignment = TextAlignment.Center };
            qty.TextChanged += (s, e) => UpdateCost();
            var entry = (item, qty);
            _parts.Add(entry);

            var remove = new Button { Text = "✕", Style = Ui.Style("SecondaryButton"), WidthRequest = 52, Padding = 0 };
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8
            };
            row.Add(new Label
            {
                Text = item.Name + "\nR" + item.UnitCost.ToString("0.00", CultureInfo.InvariantCulture) + " each · " + item.Stock + " in stock",
                FontSize = 13,
                TextColor = Ui.Color("TextPrimary"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            row.Add(Ui.InputBox(qty), 1, 0);
            row.Add(remove, 2, 0);
            remove.Clicked += (s, e) =>
            {
                _parts.Remove(entry);
                _partsList.Children.Remove(row);
                UpdateCost();
            };

            _partsList.Children.Add(row);
            UpdateCost();
        }

        private static int Quantity(Entry entry) =>
            int.TryParse((entry.Text ?? "").Trim(), out var q) ? q : 0;

        private static decimal Money(string? text) =>
            decimal.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

        // Same sums as the web form: parts = quantity × unit cost; total = parts + labour
        private void UpdateCost()
        {
            decimal parts = _parts.Sum(p => Quantity(p.Qty) * p.Item.UnitCost);
            decimal labour = Money(_labour.Text);
            _costLabel.Text = "Parts R" + parts.ToString("0.00", CultureInfo.InvariantCulture)
                              + " + labour R" + labour.ToString("0.00", CultureInfo.InvariantCulture)
                              + " = total R" + (parts + labour).ToString("0.00", CultureInfo.InvariantCulture);
        }

        // ============================================================
        // COMPLETE
        // ============================================================

        private async Task CompleteAsync()
        {
            if (_busy || _detail == null) return;

            var problems = new List<string>();
            if (_photo == null) problems.Add("take a photo of the repair");
            if (_condition == null) problems.Add("choose the final condition");
            foreach (var p in _parts)
            {
                int q = Quantity(p.Qty);
                if (q <= 0) problems.Add("enter how many " + p.Item.Name + " you used");
                else if (q > p.Item.Stock) problems.Add("only " + p.Item.Stock + " " + p.Item.Unit + " of " + p.Item.Name + " are in stock");
            }
            if (Money(_labour.Text) < 0) problems.Add("labour cost can't be negative");
            if (problems.Count > 0)
            {
                ShowMessage(BannerKind.Danger, "Please " + string.Join("; ", problems) + ".");
                return;
            }

            bool yes = await DisplayAlert("Complete " + _detail.Job.JobReference + "?",
                "The job is marked completed, the parts are taken from stock and you'll be set to Available.", "Complete", "Cancel");
            if (!yes) return;

            _busy = true;
            _complete.IsEnabled = false;
            _complete.Text = "Saving...";

            var photo = _photo!;
            var result = await _api.CompleteJobAsync(_jobId, new CompleteJobRequestDto
            {
                CompletionNotes = _notes.Text,
                FinalCondition = _condition!,
                PhotoAfterBase64 = await Task.Run(() => Convert.ToBase64String(photo)),
                Parts = _parts.Select(p => new PartUsedDto { InventoryId = p.Item.Id, Quantity = Quantity(p.Qty) }).ToList(),
                LabourCost = Money(_labour.Text)
            });

            _busy = false;
            _complete.IsEnabled = true;
            _complete.Text = "Complete Job";

            if (!result.Ok)
            {
                var text = result.Errors.Count > 1 ? "• " + string.Join("\n• ", result.Errors) : result.Error;
                ShowMessage(BannerKind.Danger, text ?? "The job wasn't completed.");
                return;
            }

            await DisplayAlert("Job completed", result.Message, "OK");
            await Navigation.PopAsync();
        }

        private async void ShowMessage(BannerKind kind, string message)
        {
            _messageHost.Content = Ui.Banner(kind, null, message);
            await _scroll.ScrollToAsync(0, 0, true);
        }
    }
}
