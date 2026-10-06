using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Request Visitor Access (Student)
    //
    // The student asks for someone to visit them. Same checks as the
    // web's visitor form (closed weekends, campus rules, safeguarding
    // of access zones); it always waits for the house master, who
    // approves it on the website — then the visitor is emailed their
    // gate pass. (api/visitor-requests)
    // ============================================================
    public class VisitorRequestPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly ScrollView _scroll = new ScrollView();
        private readonly ContentView _messageHost = new ContentView();

        private readonly Entry _name = new Entry { Placeholder = "Visitor's full name", MaxLength = 100, FontSize = 16 };
        private readonly Entry _phone = new Entry { Placeholder = "e.g. 0821234567", MaxLength = 15, Keyboard = Keyboard.Telephone, FontSize = 16 };
        private readonly Entry _email = new Entry { Placeholder = "The gate pass is emailed here", MaxLength = 100, Keyboard = Keyboard.Email, FontSize = 16 };
        private readonly Entry _idNumber = new Entry { Placeholder = "Optional", MaxLength = 13, FontSize = 16 };
        private readonly Entry _relationship = new Entry { Placeholder = "e.g. Mother, Uncle, Family friend", MaxLength = 50, FontSize = 16 };
        private readonly DatePicker _date = new DatePicker { Format = "ddd dd MMM yyyy", FontSize = 16, MinimumDate = DateTime.Today };
        private readonly TimePicker _start = new TimePicker { Format = "HH:mm", FontSize = 16 };
        private readonly TimePicker _end = new TimePicker { Format = "HH:mm", FontSize = 16 };
        private readonly Picker _zone = new Picker { Title = "-- Choose the area --", FontSize = 16 };
        private readonly Editor _purpose = new Editor { Placeholder = "Why are they visiting?", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70, MaxLength = 250 };
        private readonly Label _zoneNote = new Label { IsVisible = false };
        private readonly Button _submit = new Button { Text = "Submit Visitor Request", Style = Ui.Style("PrimaryButton") };

        private VisitorFormDto? _form;
        private bool _isParent;
        private bool _busy;

        public VisitorRequestPage()
        {
            Title = "Request Visitor";
            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _scroll.Content = _root;
            Content = _scroll;

            Responsive.Adapt(this, _root);

            _zone.SelectedIndexChanged += (s, e) => UpdateZoneNote();
            _submit.Clicked += async (s, e) => await SubmitAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_form == null) await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var form = await _api.GetVisitorFormAsync();
            _root.Children.Clear();
            _root.Children.Add(Ui.PageHeader("Request a Visitor", "Ask for someone to visit you at school.", "Requests"));

            if (!form.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Can't load the form", form.Error));
                return;
            }

            _form = form;

            // Same defaults as the web form: tomorrow, 14:00–16:00
            DateTime date;
            _date.Date = DateTime.TryParseExact(form.DefaultDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? date : DateTime.Today.AddDays(1);
            _start.Time = TimeSpan.TryParse(form.DefaultStart, out var s) ? s : new TimeSpan(14, 0, 0);
            _end.Time = TimeSpan.TryParse(form.DefaultEnd, out var e) ? e : new TimeSpan(16, 0, 0);

            _zone.Items.Clear();
            foreach (var z in form.Zones) _zone.Items.Add(z.Label);
            if (form.Zones.Count > 0) _zone.SelectedIndex = 0;

            _root.Children.Add(_messageHost);
            _root.Children.Add(Ui.Banner(BannerKind.Info, "How it works",
                "Your house master reviews every request on the website. Once approved, your visitor is emailed a gate pass to show at the gate."));

            // ── The visitor ──
            var visitor = new VerticalStackLayout { Spacing = 8 };
            visitor.Children.Add(Label("Full name *"));
            visitor.Children.Add(Ui.InputBox(_name));
            visitor.Children.Add(Label("Phone number *"));
            visitor.Children.Add(Ui.InputBox(_phone));
            visitor.Children.Add(Label("Email address *"));
            visitor.Children.Add(Ui.InputBox(_email));
            visitor.Children.Add(Label("ID or passport number"));
            visitor.Children.Add(Ui.InputBox(_idNumber));
            visitor.Children.Add(Label("Relationship to you *"));
            visitor.Children.Add(Ui.InputBox(_relationship));
            visitor.Children.Add(Ui.CheckRow("This visitor is my parent or guardian", false, on => { _isParent = on; UpdateZoneNote(); }, out _));
            _root.Children.Add(Section("The visitor", visitor));

            // ── The visit ──
            var visit = new VerticalStackLayout { Spacing = 8 };
            visit.Children.Add(Label("Visit date *"));
            visit.Children.Add(Ui.InputBox(_date));
            var times = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 };
            var startStack = new VerticalStackLayout { Spacing = 8 };
            startStack.Children.Add(Label("From *"));
            startStack.Children.Add(Ui.InputBox(_start));
            var endStack = new VerticalStackLayout { Spacing = 8 };
            endStack.Children.Add(Label("Until *"));
            endStack.Children.Add(Ui.InputBox(_end));
            times.Add(startStack, 0, 0);
            times.Add(endStack, 1, 0);
            visit.Children.Add(times);
            visit.Children.Add(Label("Where will you meet? *"));
            visit.Children.Add(Ui.InputBox(_zone));
            _zoneNote.FontSize = 12;
            _zoneNote.TextColor = Ui.Color("Warning");
            visit.Children.Add(_zoneNote);
            visit.Children.Add(Label("Purpose of the visit *"));
            visit.Children.Add(Ui.InputBox(_purpose));
            _root.Children.Add(Section("The visit · " + form.BoardingHouse, visit));

            _root.Children.Add(_submit);

            // ── My visits ──
            _root.Children.Add(new Label { Text = "My visitor requests", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 10, 0, 0) });
            if (form.MyVisits.Count == 0) _root.Children.Add(Ui.Text("You have no visitor requests yet.", "MutedText"));
            var visits = new AdaptiveGrid(320);
            foreach (var v in form.MyVisits) visits.Children.Add(VisitCard(v));
            _root.Children.Add(visits);
        }

        private static Label Label(string text) =>
            new Label { Text = text, Style = Ui.Style("FieldLabel"), TextColor = Ui.Color("TextPrimary") };

        private static Border Section(string title, View content)
        {
            // Card with the web's black header bar
            return Ui.Section(title, content);
        }

        // Same safeguarding rule the server applies
        private void UpdateZoneNote()
        {
            var zone = SelectedZone();
            bool moved = zone == "HouseCommonRoom" && !_isParent;
            _zoneNote.Text = moved ? "Only parents/guardians may meet in the House Common Room — this visit will be moved to the Public Campus Grounds." : "";
            _zoneNote.IsVisible = moved;
        }

        private string? SelectedZone() =>
            _form != null && _zone.SelectedIndex >= 0 ? _form.Zones[_zone.SelectedIndex].Value : null;

        // ============================================================
        // SUBMIT
        // ============================================================

        private async Task SubmitAsync()
        {
            if (_busy || _form == null) return;

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(_name.Text)) missing.Add("their full name");
            if (string.IsNullOrWhiteSpace(_phone.Text)) missing.Add("their phone number");
            if (string.IsNullOrWhiteSpace(_email.Text)) missing.Add("their email address");
            if (string.IsNullOrWhiteSpace(_relationship.Text)) missing.Add("how they're related to you");
            if (string.IsNullOrWhiteSpace(_purpose.Text)) missing.Add("the purpose of the visit");
            if (SelectedZone() == null) missing.Add("where you'll meet");
            if (missing.Count > 0)
            {
                ShowMessage(BannerKind.Danger, "Please enter " + string.Join(", ", missing) + ".");
                return;
            }
            if (_start.Time >= _end.Time)
            {
                ShowMessage(BannerKind.Danger, "The visit must start before it ends.");
                return;
            }

            _busy = true;
            _submit.IsEnabled = false;
            _submit.Text = "Sending...";

            var result = await _api.SubmitVisitorRequestAsync(new VisitorRequestDto
            {
                VisitorFullName = _name.Text!.Trim(),
                VisitorPhone = _phone.Text!.Trim(),
                VisitorEmail = _email.Text!.Trim(),
                VisitorIdOrPassport = string.IsNullOrWhiteSpace(_idNumber.Text) ? null : _idNumber.Text.Trim(),
                RelationshipToStudent = _relationship.Text!.Trim(),
                IsParentOrGuardian = _isParent,
                VisitDate = _date.Date.Date,
                StartTime = _start.Time.ToString(@"hh\:mm"),
                EndTime = _end.Time.ToString(@"hh\:mm"),
                RequestedZone = SelectedZone()!,
                PurposeOfVisit = _purpose.Text!.Trim()
            });

            _busy = false;
            _submit.IsEnabled = true;
            _submit.Text = "Submit Visitor Request";

            if (!result.Ok)
            {
                var text = result.Errors.Count > 1 ? "• " + string.Join("\n• ", result.Errors) : result.Error;
                ShowMessage(BannerKind.Danger, text ?? "Your request wasn't sent.");
                return;
            }

            await DisplayAlert("Visitor request sent", result.Message, "OK");

            foreach (var entry in new[] { _name, _phone, _email, _idNumber, _relationship }) entry.Text = "";
            _purpose.Text = "";
            _form = null;
            await LoadAsync();
            await _scroll.ScrollToAsync(0, 0, false);
        }

        private async void ShowMessage(BannerKind kind, string message)
        {
            _messageHost.Content = Ui.Banner(kind, null, message);
            await _scroll.ScrollToAsync(0, 0, true);
        }

        private static View VisitCard(VisitSummaryDto v)
        {
            var stack = new VerticalStackLayout { Spacing = 4 };
            var top = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            top.Add(new Label { Text = v.Visitor + (string.IsNullOrEmpty(v.Relationship) ? "" : " · " + v.Relationship), Style = Ui.Style("CardTitle") }, 0, 0);
            top.Add(StatusChip(v), 1, 0);
            stack.Children.Add(top);
            stack.Children.Add(Ui.Text(v.Date + ", " + v.Time + " · " + v.Zone, "MutedText"));
            if (!string.IsNullOrWhiteSpace(v.Purpose)) stack.Children.Add(Ui.Text(v.Purpose));
            if (!string.IsNullOrWhiteSpace(v.GatePass)) stack.Children.Add(Ui.Chip("Gate pass " + v.GatePass, "#D1FAE5", "#065F46", 11));
            if (!string.IsNullOrWhiteSpace(v.PolicyFlag)) stack.Children.Add(Ui.Text(v.PolicyFlag, "MutedText"));
            if (!string.IsNullOrWhiteSpace(v.Remarks)) stack.Children.Add(Ui.Text("House master: " + v.Remarks, "MutedText"));
            return Ui.Card(stack, new Thickness(14));
        }

        private static View StatusChip(VisitSummaryDto v)
        {
            if (v.Status == "Approved" || v.Status == "AutoApprovedWeekend") return Ui.Chip(v.StatusLabel, "#D1FAE5", "#065F46");
            if (v.Status.StartsWith("Rejected") || v.Status == "Cancelled") return Ui.Chip(v.StatusLabel, "#FEE2E2", "#991B1B");
            return Ui.Chip(v.StatusLabel, "#FEF3C7", "#92400E");
        }
    }
}
