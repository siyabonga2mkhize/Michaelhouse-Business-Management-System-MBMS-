using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // House Master — emergency (from Michaelhouse.Mobile)
    //
    //   • residence alerts for the House Master's residences
    //     (GET api/mobile/housemaster/alerts)
    //   • emergency roll call: choose a residence, mark each student
    //     present (housemaster/residences/{id}/rollcall/{studentId}) —
    //     recorded as an "EmergencyRollCall" QR scan record
    // ============================================================
    public class HouseMasterEmergencyPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly RefreshView _refresh = new RefreshView();
        private bool _loading;

        public HouseMasterEmergencyPage()
        {
            Title = "Emergency";

            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _refresh.Content = new ScrollView { Content = _root };
            _refresh.Refreshing += async (s, e) =>
            {
                await LoadAsync();
                _refresh.IsRefreshing = false;
            };
            Content = _refresh;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;

            var alertsTask = _api.GetHouseMasterAlertsAsync();
            var residencesTask = _api.GetHouseMasterResidencesAsync();
            var alerts = await alertsTask;
            var residences = await residencesTask;
            _loading = false;

            _root.Children.Clear();
            _root.Children.Add(Ui.PageHeader("Residence Emergency"));

            // ── Emergency roll call ──
            _root.Children.Add(new Label { Text = "Emergency roll call", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 6, 0, 0) });
            if (!residences.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, null, residences.Error));
            }
            else if (residences.Data == null || residences.Data.Count == 0)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Info, null, "No residence is assigned to you."));
            }
            else
            {
                foreach (var r in residences.Data)
                {
                    var button = new Button { Text = "Roll call · " + r.Name, Style = Ui.Style("PrimaryButton") };
                    button.Clicked += async (s, e) => await Navigation.PushAsync(new RollCallPage(r));
                    _root.Children.Add(button);
                }
            }

            // ── Residence alerts ──
            _root.Children.Add(new Label { Text = "Residence alerts", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 10, 0, 0) });
            if (!alerts.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, null, alerts.Error));
            }
            else if (alerts.Data == null || alerts.Data.Count == 0)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Success, null, "No active residence alerts."));
            }
            else
            {
                foreach (var a in alerts.Data)
                {
                    var stack = new VerticalStackLayout { Spacing = 4 };
                    stack.Children.Add(new Label { Text = a.Title, Style = Ui.Style("CardTitle") });
                    stack.Children.Add(Ui.Text(a.Message));
                    stack.Children.Add(new Label
                    {
                        Text = a.Residence + " · " + a.CreatedAt.ToLocalTime().ToString("ddd dd MMM, HH:mm"),
                        Style = Ui.Style("MutedText"),
                        FontSize = 11
                    });
                    _root.Children.Add(Ui.Card(stack, new Thickness(14)));
                }
            }
        }
    }

    // One residence's emergency roll call: a big "Mark present" per student
    public class RollCallPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly HouseMasterResidenceDto _residence;
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 10 };
        private readonly Label _count = new Label();
        private readonly HashSet<int> _present = new HashSet<int>();
        private int _total;

        public RollCallPage(HouseMasterResidenceDto residence)
        {
            _residence = residence;
            Title = "Roll Call";

            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            Content = new ScrollView { Content = _root };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_total > 0) return;

            var result = await _api.GetRollCallAsync(_residence.Id);

            _root.Children.Clear();
            _root.Children.Add(Ui.PageHeader("Roll Call", _residence.Name));

            if (!result.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, null, result.Error));
                return;
            }

            var students = result.Data ?? new List<RollCallStudentDto>();
            _total = students.Count;
            if (_total == 0)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Info, null, "No active students are listed for this residence."));
                return;
            }

            _count.FontFamily = "MontserratBold";
            _count.FontSize = 15;
            _count.TextColor = Ui.Color("TextPrimary");
            UpdateCount();
            _root.Children.Add(_count);

            foreach (var student in students)
            {
                _root.Children.Add(StudentRow(student));
            }
        }

        private void UpdateCount() => _count.Text = _present.Count + " of " + _total + " marked present";

        private View StudentRow(RollCallStudentDto student)
        {
            var name = new Label { Text = student.Name, Style = Ui.Style("CardTitle"), VerticalOptions = LayoutOptions.Center };
            var button = new Button { Text = "Mark present", Style = Ui.Style("SecondaryButton"), WidthRequest = 150 };
            var state = new Label { FontSize = 12, TextColor = Ui.Color("Danger"), IsVisible = false };

            button.Clicked += async (s, e) =>
            {
                button.IsEnabled = false;
                button.Text = "Saving...";
                state.IsVisible = false;

                var result = await _api.MarkRollCallPresentAsync(_residence.Id, student.StudentId);
                if (result.Ok)
                {
                    _present.Add(student.StudentId);
                    button.Text = "✓ Present";
                    button.BackgroundColor = Ui.Color("SuccessBackground");
                    button.TextColor = Ui.Color("Success");
                    button.BorderColor = Ui.Color("Success");
                    UpdateCount();
                }
                else
                {
                    button.IsEnabled = true;
                    button.Text = "Mark present";
                    state.Text = result.Error;
                    state.IsVisible = true;
                }
            };

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
            grid.Add(name, 0, 0);
            grid.Add(button, 1, 0);

            var stack = new VerticalStackLayout { Spacing = 4 };
            stack.Children.Add(grid);
            stack.Children.Add(state);
            return Ui.Card(stack, new Thickness(14, 10));
        }
    }
}
