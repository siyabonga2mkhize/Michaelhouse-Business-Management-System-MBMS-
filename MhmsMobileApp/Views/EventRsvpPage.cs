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
    // UC18 — RSVP for one event (logged-in student, parent or staff)
    //
    // Same form and wording as the web page Rsvp/Event:
    //   • attending or not
    //   • dietary needs — students: their profile (read only);
    //     parents / staff: for this event, meals that clash are
    //     blocked straight away (POST .../meal-options)
    //   • their meal, when the event offers a choice
    //   • guests (parents / staff, when the event allows)
    //   • comments
    // Everything is checked again on the server
    // (EventRsvpService.SubmitInviteeResponse).
    // ============================================================
    public class EventRsvpPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly int _eventId;

        private EventRsvpDetailDto? _detail;

        // What the person has entered
        private bool? _attending;
        private int? _menuItemId;
        private DietaryFormDto _dietary = new DietaryFormDto();
        private readonly List<GuestDto> _guests = new List<GuestDto>();
        private List<RsvpMealDto> _meals = new List<RsvpMealDto>();

        // Screen parts
        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 16, 16, 24), Spacing = 14 };
        private readonly ScrollView _scroll = new ScrollView();
        private readonly ContentView _errorHost = new ContentView();
        private readonly VerticalStackLayout _attendingSection = new VerticalStackLayout { Spacing = 14, IsVisible = false };
        private readonly ContentView _mealHost = new ContentView();
        private readonly VerticalStackLayout _guestList = new VerticalStackLayout { Spacing = 10 };
        private readonly Label _guestCountLabel = new Label();
        private readonly Editor _comments = new Editor { Placeholder = "Optional", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 80, MaxLength = 500 };
        private readonly Button _submit = new Button { Style = Ui.Style("PrimaryButton") };
        private readonly Border _submitBar = new Border { IsVisible = false };
        private Border? _yesTile, _noTile;

        private CancellationTokenSource? _mealCheck;

        public EventRsvpPage(int eventId)
        {
            _eventId = eventId;
            Title = "RSVP";

            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _scroll.Content = _root;

            _submit.Clicked += OnSubmitClicked;
            _submitBar.BackgroundColor = Ui.Color("White");
            _submitBar.Stroke = Ui.Color("CardBorder");
            _submitBar.StrokeThickness = 1;
            _submitBar.Padding = new Thickness(16, 12, 16, 16);
            _submitBar.Content = _submit;

            var page = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
            page.Add(_scroll, 0, 0);
            page.Add(_submitBar, 0, 1);
            Content = page;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_detail == null) await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var detail = await _api.GetMyEventAsync(_eventId);

            _root.Children.Clear();

            if (!detail.Ok)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Event not available", detail.Error));
                return;
            }

            _detail = detail;
            _meals = detail.Meals;

            // Pre-fill from their earlier answer (web: PopulateInvitee)
            if (detail.Response != null)
            {
                _attending = detail.Response.Attending;
                _menuItemId = detail.Response.MenuItemId;
                _comments.Text = detail.Response.Comments;
                _guests.AddRange(detail.Response.Guests);
            }
            if (detail.Dietary != null) _dietary = detail.Dietary;

            Render(detail);
        }

        // ============================================================
        // RENDER
        // ============================================================

        private void Render(EventRsvpDetailDto d)
        {
            _root.Children.Add(Hero(d));

            if (!d.IsOpen)
            {
                var text = (d.ClosedReason ?? "This event is not currently accepting RSVPs.");
                if (d.Response != null) text += "\n\nYour response: " + (d.Response.Attending ? "Attending" : "Not attending");
                text += "\n\nContact the Michaelhouse cafeteria if you need to make changes.";
                _root.Children.Add(Ui.Banner(BannerKind.Info, "🔒  RSVPs Closed", text));
                return;
            }

            if (!d.Invitee.IsInvited)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Info, "🔒  Not invited", d.Invitee.NotInvitedReason));
                return;
            }

            _root.Children.Add(_errorHost);
            _root.Children.Add(AttendingCard(d));

            _attendingSection.Children.Add(DietaryCard(d));
            if (d.OffersMealChoice)
            {
                _attendingSection.Children.Add(_mealHost);
                RenderMeals();
            }
            if (d.Invitee.CanBringGuests && d.MaxGuests > 0)
            {
                _attendingSection.Children.Add(GuestsCard(d));
            }
            _attendingSection.IsVisible = _attending == true;
            _root.Children.Add(_attendingSection);

            _root.Children.Add(Section("Anything else?", Ui.InputBox(_comments)));

            _submit.Text = d.Response != null ? "Update RSVP" : "Submit RSVP";
            _submitBar.IsVisible = true;
        }

        // Event name, date, time and venue (web: .rv-hero)
        private static View Hero(EventRsvpDetailDto d)
        {
            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(new Label
            {
                Text = "You're invited",
                FontFamily = "MontserratBold",
                FontSize = 11,
                CharacterSpacing = 2,
                TextTransform = TextTransform.Uppercase,
                TextColor = Color.FromArgb("#FFE4E6")
            });
            stack.Children.Add(new Label { Text = d.EventName, FontFamily = "PlayfairBold", FontSize = 26, TextColor = Colors.White });
            stack.Children.Add(new Label
            {
                Text = d.DateLabel + "\n" + d.TimeLabel + (string.IsNullOrEmpty(d.VenueName) ? "" : " · " + d.VenueName) +
                       (d.IsOpen && d.RsvpDeadline != null ? "\nRSVP by " + d.RsvpDeadline : ""),
                FontSize = 14,
                TextColor = Colors.White
            });

            return new Border
            {
                BackgroundColor = Ui.Color("Primary"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(18, 18),
                Content = stack
            };
        }

        private static Border Section(string title, View content, string? help = null)
        {
            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(new Label { Text = title, Style = Ui.Style("FieldLabel") });
            if (!string.IsNullOrEmpty(help)) stack.Children.Add(Ui.Text(help, "MutedText"));
            stack.Children.Add(content);
            return Ui.Card(stack);
        }

        // ── Attending? ──
        private View AttendingCard(EventRsvpDetailDto d)
        {
            var help = "Responding as " + d.Invitee.Type.ToLower() + ".";
            if (d.Response != null)
                help += " You've answered: " + (d.Response.Attending ? "Attending" : "Not attending") + ". You can change it until RSVPs close.";

            _yesTile = Ui.Tile("Yes", "🎉");
            _noTile = Ui.Tile("No", "😔");
            _yesTile.OnTap(() => SetAttending(true));
            _noTile.OnTap(() => SetAttending(false));
            Ui.SetTileSelected(_yesTile, _attending == true);
            Ui.SetTileSelected(_noTile, _attending == false);

            var tiles = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
            tiles.Add(_yesTile, 0, 0);
            tiles.Add(_noTile, 1, 0);

            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(new Label { Text = d.Invitee.Name + ", will you be attending?", Style = Ui.Style("CardTitle") });
            stack.Children.Add(Ui.Text(help, "MutedText"));
            stack.Children.Add(tiles);
            return Ui.Card(stack);
        }

        private void SetAttending(bool attending)
        {
            _attending = attending;
            if (_yesTile != null) Ui.SetTileSelected(_yesTile, attending);
            if (_noTile != null) Ui.SetTileSelected(_noTile, !attending);
            _attendingSection.IsVisible = attending;
        }

        // ── Dietary needs ──
        private View DietaryCard(EventRsvpDetailDto d)
        {
            if (d.IsStudent)
            {
                // Students: the kitchen uses their profile
                var info = new VerticalStackLayout { Spacing = 6 };
                info.Children.Add(Line("Preference", d.ProfileSummary.Preference));
                info.Children.Add(Line("Allergies", d.ProfileSummary.Allergies));
                info.Children.Add(Line("Medical", d.ProfileSummary.Medical));
                info.Children.Add(Ui.Text("Something wrong? Update your dietary profile on the Michaelhouse website.", "MutedText"));
                return Section("Your dietary requirements", info,
                    "The kitchen uses your dietary profile — you don't need to enter anything here.");
            }

            // Parents / staff: for this event only
            var form = new VerticalStackLayout { Spacing = 6 };

            form.Children.Add(SubLabel("Meal preference"));
            foreach (var o in d.Options.Preferences)
            {
                var radio = new RadioButton
                {
                    Content = o.Label,
                    GroupName = "pref",
                    Value = o.Code,
                    IsChecked = string.Equals(_dietary.DietaryPreference ?? "None", o.Code, StringComparison.OrdinalIgnoreCase),
                    FontSize = 15,
                    MinimumHeightRequest = 48
                };
                radio.CheckedChanged += (s, e) =>
                {
                    if (!e.Value) return;
                    _dietary.DietaryPreference = o.Code;
                    DietaryChanged();
                };
                form.Children.Add(radio);
            }
            form.Children.Add(TextBox("If other, please describe", _dietary.DietaryPreferenceOther, 200, t => { _dietary.DietaryPreferenceOther = t; DietaryChanged(); }));

            form.Children.Add(SubLabel("Allergies"));
            foreach (var o in d.Options.Allergies)
            {
                form.Children.Add(Ui.CheckRow(o.Label, _dietary.SelectedAllergies.Contains(o.Code),
                    on => { Toggle(_dietary.SelectedAllergies, o.Code, on); DietaryChanged(); }, out _));
            }
            form.Children.Add(TextBox("Other allergies, comma-separated", _dietary.OtherAllergies, 300, t => { _dietary.OtherAllergies = t; DietaryChanged(); }));

            form.Children.Add(SubLabel("Medical dietary restrictions"));
            foreach (var o in d.Options.MedicalRestrictions)
            {
                form.Children.Add(Ui.CheckRow(o.Label, _dietary.SelectedMedicalRestrictions.Contains(o.Code),
                    on => { Toggle(_dietary.SelectedMedicalRestrictions, o.Code, on); DietaryChanged(); }, out _));
            }
            form.Children.Add(TextBox("If other, please describe", _dietary.MedicalRestrictionOther, 200, t => { _dietary.MedicalRestrictionOther = t; DietaryChanged(); }));

            form.Children.Add(SubLabel("Anything else the kitchen should know?"));
            var notes = new Editor { Text = _dietary.DietaryNotes, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70, MaxLength = 500 };
            notes.TextChanged += (s, e) => _dietary.DietaryNotes = e.NewTextValue;
            form.Children.Add(Ui.InputBox(notes));

            return Section("Your dietary requirements", form, "For this event only. Meals you can't eat are blocked below.");
        }

        private static Label SubLabel(string text) =>
            new Label { Text = text, FontFamily = "MontserratBold", FontSize = 14, TextColor = Ui.Color("TextPrimary"), Margin = new Thickness(0, 10, 0, 0) };

        private static Label Line(string label, string? value)
        {
            var text = new FormattedString();
            text.Spans.Add(new Span { Text = label + ": ", FontFamily = "MontserratBold" });
            text.Spans.Add(new Span { Text = string.IsNullOrWhiteSpace(value) ? "None" : value });
            return new Label { FormattedText = text, FontSize = 14, TextColor = Ui.Color("Gray600") };
        }

        private static View TextBox(string placeholder, string? value, int maxLength, Action<string> changed)
        {
            var entry = new Entry { Placeholder = placeholder, Text = value, MaxLength = maxLength, FontSize = 15 };
            entry.TextChanged += (s, e) => changed(e.NewTextValue);
            return Ui.InputBox(entry);
        }

        private static void Toggle(List<string> list, string code, bool on)
        {
            if (on && !list.Contains(code)) list.Add(code);
            if (!on) list.Remove(code);
        }

        // Ask the server which meals suit what's been entered (web: the
        // live MealOptions check), after a short pause in typing
        private async void DietaryChanged()
        {
            if (_detail == null || !_detail.OffersMealChoice) return;

            _mealCheck?.Cancel();
            var cts = _mealCheck = new CancellationTokenSource();

            try { await Task.Delay(500, cts.Token); }
            catch (TaskCanceledException) { return; }

            var result = await _api.GetEventMealOptionsAsync(_eventId, _dietary);
            if (cts.IsCancellationRequested || !result.Ok) return;

            _meals = result.Meals;
            RenderMeals();
        }

        // ── Meal ──
        private void RenderMeals()
        {
            var stack = new VerticalStackLayout { Spacing = 10 };

            if (!_meals.Any(m => m.IsSuitable))
            {
                stack.Children.Add(Ui.Banner(BannerKind.Warning, null,
                    "None of this event's meals suit your dietary needs. Please contact the cafeteria."));
            }

            // A pick that no longer suits is cleared
            if (_menuItemId.HasValue && !_meals.Any(m => m.MenuItemId == _menuItemId.Value && m.IsSuitable))
            {
                _menuItemId = null;
            }

            foreach (var meal in _meals)
            {
                stack.Children.Add(MealCard(meal));
            }

            _mealHost.Content = Section("Your meal", stack);
        }

        private View MealCard(RsvpMealDto meal)
        {
            bool selected = meal.IsSuitable && _menuItemId == meal.MenuItemId;

            var stack = new VerticalStackLayout { Spacing = 6 };

            var title = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            title.Add(new Label { Text = meal.Name, Style = Ui.Style("CardTitle") }, 0, 0);
            if (selected) title.Add(Ui.Chip("✓ Your meal", "#C21E2E", "#FFFFFF"), 1, 0);
            stack.Children.Add(title);

            if (meal.SuitableFor.Count > 0)
            {
                var tags = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
                foreach (var tag in meal.SuitableFor)
                {
                    var chip = Ui.Chip(tag, "#D1FAE5", "#065F46", 10);
                    chip.Margin = new Thickness(0, 2, 6, 2);
                    tags.Children.Add(chip);
                }
                stack.Children.Add(tags);
            }

            stack.Children.Add(new Label
            {
                Text = string.IsNullOrEmpty(meal.Allergens) ? "No listed allergens" : "Contains: " + meal.Allergens,
                Style = Ui.Style("MutedText")
            });

            if (!meal.IsSuitable)
            {
                stack.Children.Add(new Label { Text = "Not suitable: " + meal.UnsuitableReason, FontSize = 12, TextColor = Ui.Color("Danger") });
            }

            var card = new Border { Style = Ui.Style("Card"), Padding = new Thickness(14), Content = stack };

            if (meal.IsSuitable)
            {
                Ui.SetTileSelected(card, selected);
                card.OnTap(() =>
                {
                    _menuItemId = meal.MenuItemId;
                    RenderMeals();
                });
            }
            else
            {
                card.Opacity = 0.55;
                card.BackgroundColor = Ui.Color("Gray100");
            }

            return card;
        }

        // ── Guests (parents / staff) ──
        private View GuestsCard(EventRsvpDetailDto d)
        {
            var minus = new Button { Text = "–", Style = Ui.Style("SecondaryButton"), WidthRequest = 56, FontSize = 22, Padding = 0 };
            var plus = new Button { Text = "+", Style = Ui.Style("SecondaryButton"), WidthRequest = 56, FontSize = 22, Padding = 0 };
            _guestCountLabel.FontFamily = "MontserratBold";
            _guestCountLabel.FontSize = 22;
            _guestCountLabel.TextColor = Ui.Color("TextPrimary");
            _guestCountLabel.VerticalOptions = LayoutOptions.Center;
            _guestCountLabel.HorizontalTextAlignment = TextAlignment.Center;
            _guestCountLabel.WidthRequest = 50;

            minus.Clicked += (s, e) => { if (_guests.Count > 0) { _guests.RemoveAt(_guests.Count - 1); RenderGuests(); } };
            plus.Clicked += (s, e) => { if (_guests.Count < d.MaxGuests) { _guests.Add(new GuestDto()); RenderGuests(); } };

            var counter = new HorizontalStackLayout { Spacing = 12 };
            counter.Children.Add(minus);
            counter.Children.Add(_guestCountLabel);
            counter.Children.Add(plus);

            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(new Label { Text = "How many guests are you bringing?", Style = Ui.Style("CardTitle"), FontSize = 15 });
            stack.Children.Add(counter);
            stack.Children.Add(Ui.Text("Up to " + d.MaxGuests + ". Not including yourself.", "MutedText"));
            stack.Children.Add(_guestList);

            RenderGuests();
            return Section("Guests", stack);
        }

        private void RenderGuests()
        {
            if (_detail == null) return;
            _guestCountLabel.Text = _guests.Count.ToString();
            _guestList.Children.Clear();

            for (int i = 0; i < _guests.Count; i++)
            {
                _guestList.Children.Add(GuestCard(i, _guests[i]));
            }
        }

        private View GuestCard(int index, GuestDto guest)
        {
            var d = _detail!;
            var stack = new VerticalStackLayout { Spacing = 8 };
            stack.Children.Add(new Label { Text = "Guest " + (index + 1), Style = Ui.Style("FieldLabel") });

            // Dietary group
            var prefs = d.Options.Preferences;
            var prefPicker = new Picker { Title = "Dietary group", FontSize = 15 };
            foreach (var o in prefs) prefPicker.Items.Add(o.Label);
            prefPicker.SelectedIndex = Math.Max(0, prefs.FindIndex(o => string.Equals(o.Code, guest.DietaryPreference ?? "None", StringComparison.OrdinalIgnoreCase)));
            prefPicker.SelectedIndexChanged += (s, e) =>
            {
                if (prefPicker.SelectedIndex >= 0) guest.DietaryPreference = prefs[prefPicker.SelectedIndex].Code;
            };
            stack.Children.Add(Ui.InputBox(prefPicker));

            // Their meal
            if (d.OffersMealChoice && _meals.Count > 0)
            {
                var meals = _meals;
                var mealPicker = new Picker { Title = "— Choose their meal —", FontSize = 15 };
                foreach (var m in meals) mealPicker.Items.Add(m.Name);
                mealPicker.SelectedIndex = guest.MenuItemId.HasValue ? meals.FindIndex(m => m.MenuItemId == guest.MenuItemId.Value) : -1;
                mealPicker.SelectedIndexChanged += (s, e) =>
                {
                    guest.MenuItemId = mealPicker.SelectedIndex >= 0 ? meals[mealPicker.SelectedIndex].MenuItemId : (int?)null;
                };
                stack.Children.Add(Ui.InputBox(mealPicker));
            }

            stack.Children.Add(TextBox("Their dietary requirement (allergies etc.)", guest.DietaryNotes, 200, t => guest.DietaryNotes = t));

            return new Border
            {
                BackgroundColor = Ui.Color("PageBackground"),
                Stroke = Ui.Color("CardBorder"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12),
                Content = stack
            };
        }

        // ============================================================
        // SUBMIT
        // ============================================================

        private async void OnSubmitClicked(object? sender, EventArgs e)
        {
            if (_detail == null) return;

            if (_attending == null)
            {
                ShowErrors(new List<string> { "Please choose Yes or No." });
                return;
            }

            var request = new InviteeRsvpRequestDto
            {
                Attending = _attending.Value,
                MenuItemId = _attending.Value ? _menuItemId : null,
                Comments = _comments.Text,
                Dietary = _detail.IsStudent ? null : _dietary,
                Guests = _attending.Value ? _guests.ToList() : new List<GuestDto>()
            };

            _submit.IsEnabled = false;
            var label = _submit.Text;
            _submit.Text = "Sending...";

            var result = await _api.SubmitMyRsvpAsync(_eventId, request);

            _submit.IsEnabled = true;
            _submit.Text = label;

            if (!result.Ok)
            {
                ShowErrors(result.Errors.Count > 0 ? result.Errors : new List<string> { result.Error ?? "Your RSVP wasn't saved." });
                return;
            }

            await DisplayAlert("Thank you", result.Message, "OK");
            await Navigation.PopAsync();
        }

        // Web: "Please check your RSVP:" with the list of problems
        private async void ShowErrors(List<string> errors)
        {
            _errorHost.Content = Ui.Banner(BannerKind.Danger, "Please check your RSVP:", "• " + string.Join("\n• ", errors));
            await _scroll.ScrollToAsync(0, 0, true);
        }
    }
}
