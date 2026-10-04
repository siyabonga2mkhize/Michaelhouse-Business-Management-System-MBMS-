using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Request Permission to Leave Residence (UC27, Student)
    //
    // Same form and rules as the web page LeaveRequest/Create. The
    // request goes to the parent, then the house master of the
    // student's residence; they decide on the website.
    // (api/leave-requests)
    // ============================================================
    public class LeaveRequestPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly ScrollView _scroll = new ScrollView();
        private readonly ContentView _messageHost = new ContentView();

        private readonly Entry _destination = new Entry { Placeholder = "e.g. Home, Durban", MaxLength = 300, FontSize = 16 };
        private readonly DatePicker _departDate = new DatePicker { Format = "ddd dd MMM yyyy", FontSize = 16 };
        private readonly TimePicker _departTime = new TimePicker { Format = "HH:mm", FontSize = 16 };
        private readonly DatePicker _returnDate = new DatePicker { Format = "ddd dd MMM yyyy", FontSize = 16 };
        private readonly TimePicker _returnTime = new TimePicker { Format = "HH:mm", FontSize = 16 };
        private readonly Editor _reason = new Editor { Placeholder = "Why do you need to leave?", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 80, MaxLength = 500 };
        private readonly ContentView _conflictHost = new ContentView();
        private readonly Button _submit = new Button { Text = "Submit Leave Request", Style = Ui.Style("PrimaryButton") };
        private CheckBox? _acknowledge;
        private bool _hasConflict;
        private bool _loaded;
        private bool _busy;
        private CancellationTokenSource? _conflictCheck;

        public LeaveRequestPage()
        {
            Title = "Request Leave";
            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _scroll.Content = _root;
            Content = _scroll;

            // Same starting values as the web form: leave in 1 hour, back in 4
            var depart = DateTime.Now.AddHours(1);
            var back = DateTime.Now.AddHours(4);
            _departDate.Date = depart.Date; _departTime.Time = new TimeSpan(depart.Hour, depart.Minute, 0);
            _returnDate.Date = back.Date; _returnTime.Time = new TimeSpan(back.Hour, back.Minute, 0);
            _departDate.MinimumDate = DateTime.Today;
            _returnDate.MinimumDate = DateTime.Today;

            foreach (var v in new View[] { _departDate, _departTime, _returnDate, _returnTime })
            {
                if (v is DatePicker dp) dp.DateSelected += (s, e) => DatesChanged();
                if (v is TimePicker tp) tp.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TimePicker.Time)) DatesChanged(); };
            }

            _submit.Clicked += async (s, e) => await SubmitAsync();
        }

        private DateTime Departure => _departDate.Date.Date + _departTime.Time;
        private DateTime Return => _returnDate.Date.Date + _returnTime.Time;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_loaded) return;
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var form = await _api.GetLeaveFormAsync();
            _root.Children.Clear();
            _root.Children.Add(Ui.PageHeader("Request Leave", "Ask permission to leave the residence."));

            if (!form.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Can't load the form", form.Error));
                return;
            }

            _loaded = true;
            _root.Children.Add(_messageHost);

            if (!form.CanRequest)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, null, form.CannotRequestReason));
            }
            else
            {
                _root.Children.Add(Ui.Banner(BannerKind.Info, "Who approves",
                    "First your parent/guardian, then your house master: " + form.HouseMaster + " (" + form.Residence + ")."));

                var fields = new VerticalStackLayout { Spacing = 8 };
                fields.Children.Add(Label("Destination *"));
                fields.Children.Add(Ui.InputBox(_destination));
                fields.Children.Add(Label("Departure date & time *"));
                fields.Children.Add(DateTimeRow(_departDate, _departTime));
                fields.Children.Add(Label("Expected return date & time *"));
                fields.Children.Add(DateTimeRow(_returnDate, _returnTime));
                fields.Children.Add(Label("Reason for leaving *"));
                fields.Children.Add(Ui.InputBox(_reason));
                fields.Children.Add(_conflictHost);
                _root.Children.Add(Ui.Card(fields));
                _root.Children.Add(_submit);
                DatesChanged();
            }

            _root.Children.Add(new Label { Text = "My leave requests", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 10, 0, 0) });
            if (form.MyRequests.Count == 0)
            {
                _root.Children.Add(Ui.Text("You have no leave requests yet.", "MutedText"));
            }
            foreach (var r in form.MyRequests)
            {
                _root.Children.Add(RequestCard(r));
            }
        }

        private static Label Label(string text) =>
            new Label { Text = text, FontFamily = "MontserratBold", FontSize = 13, TextColor = Ui.Color("TextPrimary"), Margin = new Thickness(0, 6, 0, 0) };

        private static View DateTimeRow(DatePicker date, TimePicker time)
        {
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(110)) }, ColumnSpacing = 10 };
            grid.Add(Ui.InputBox(date), 0, 0);
            grid.Add(Ui.InputBox(time), 1, 0);
            return grid;
        }

        // ============================================================
        // SCHOOL CALENDAR CHECK (web: CheckDateConflicts) — advisory;
        // the server checks again when the request is sent
        // ============================================================

        private async void DatesChanged()
        {
            _conflictCheck?.Cancel();
            var cts = _conflictCheck = new CancellationTokenSource();
            try { await Task.Delay(400, cts.Token); } catch (TaskCanceledException) { return; }

            if (Return <= Departure)
            {
                _hasConflict = false;
                _conflictHost.Content = null;
                return;
            }

            var result = await _api.CheckLeaveConflictsAsync(Departure, Return);
            if (cts.IsCancellationRequested || !result.Ok) return;
            ShowConflicts(result);
        }

        private void ShowConflicts(LeaveConflictsDto result)
        {
            _hasConflict = result.HasConflict;
            if (!result.HasConflict)
            {
                _conflictHost.Content = null;
                _acknowledge = null;
                return;
            }

            var stack = new VerticalStackLayout { Spacing = 8 };
            var list = string.Join("\n", result.Conflicts.Select(c => "• " + c.Title + " (" + c.Start + (c.End != c.Start ? " – " + c.End : "") + (string.IsNullOrEmpty(c.Category) ? "" : ", " + c.Category) + ")"));
            stack.Children.Add(Ui.Banner(BannerKind.Warning, "Your dates overlap a school event", (result.Message ?? "") + (list.Length > 0 ? "\n" + list : "")));
            stack.Children.Add(Ui.CheckRow("I understand and still want to submit", false, on => { }, out var box));
            _acknowledge = box;
            _conflictHost.Content = stack;
        }

        // ============================================================
        // SUBMIT
        // ============================================================

        private async Task SubmitAsync()
        {
            if (_busy) return;

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(_destination.Text)) missing.Add("the destination");
            if (string.IsNullOrWhiteSpace(_reason.Text)) missing.Add("the reason");
            if (missing.Count > 0)
            {
                ShowMessage(BannerKind.Danger, "Please enter " + string.Join(" and ", missing) + ".");
                return;
            }
            if (Departure <= DateTime.Now)
            {
                ShowMessage(BannerKind.Danger, "Departure date and time must be in the future.");
                return;
            }
            if (Return <= Departure)
            {
                ShowMessage(BannerKind.Danger, "Expected return must be after the departure date and time.");
                return;
            }
            if (_hasConflict && _acknowledge?.IsChecked != true)
            {
                ShowMessage(BannerKind.Danger, "Your dates overlap a school event. Tick \"I understand\" if you still wish to submit.");
                return;
            }

            _busy = true;
            _submit.IsEnabled = false;
            _submit.Text = "Sending...";

            var result = await _api.SubmitLeaveRequestAsync(new LeaveRequestDto
            {
                Destination = _destination.Text!.Trim(),
                DepartureDateTime = Departure,
                ExpectedReturnDateTime = Return,
                Reason = _reason.Text!.Trim(),
                AcknowledgeConflict = _acknowledge?.IsChecked == true
            });

            _busy = false;
            _submit.IsEnabled = true;
            _submit.Text = "Submit Leave Request";

            if (!result.Ok)
            {
                var text = result.Errors.Count > 0 ? "• " + string.Join("\n• ", result.Errors) : result.Error;
                ShowMessage(BannerKind.Danger, text ?? "Your request wasn't sent.");
                return;
            }

            await DisplayAlert("Leave request sent", result.Message, "OK");
            _destination.Text = "";
            _reason.Text = "";
            _loaded = false;
            await LoadAsync();
            await _scroll.ScrollToAsync(0, 0, false);
        }

        private async void ShowMessage(BannerKind kind, string message)
        {
            _messageHost.Content = Ui.Banner(kind, null, message);
            await _scroll.ScrollToAsync(0, 0, true);
        }

        private static View RequestCard(LeaveSummaryDto r)
        {
            var stack = new VerticalStackLayout { Spacing = 4 };
            var top = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            top.Add(new Label { Text = r.Destination, Style = Ui.Style("CardTitle") }, 0, 0);
            top.Add(StatusChip(r), 1, 0);
            stack.Children.Add(top);
            stack.Children.Add(Ui.Text(r.Departure + "  →  " + r.ExpectedReturn, "MutedText"));
            stack.Children.Add(Ui.Text(r.Reason));
            if (!string.IsNullOrWhiteSpace(r.ParentComments)) stack.Children.Add(Ui.Text("Parent: " + r.ParentComments, "MutedText"));
            if (!string.IsNullOrWhiteSpace(r.HouseMasterComments)) stack.Children.Add(Ui.Text("House master: " + r.HouseMasterComments, "MutedText"));
            if (r.HasCalendarConflict) stack.Children.Add(Ui.Chip("Overlaps a school event", "#FFF7ED", "#9A3412", 10));
            return Ui.Card(stack, new Thickness(14));
        }

        private static View StatusChip(LeaveSummaryDto r)
        {
            switch (r.Status)
            {
                case "Approved": return Ui.Chip(r.StatusLabel, "#D1FAE5", "#065F46");
                case "Rejected": return Ui.Chip(r.StatusLabel, "#FEE2E2", "#991B1B");
                default: return Ui.Chip(r.StatusLabel, "#FEF3C7", "#92400E");
            }
        }
    }
}
