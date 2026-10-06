using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // UC18 — My Events (logged-in student, parent or staff member)
    //
    // Same list as the web page Rsvp/MyEvents: events they're
    // invited to with RSVP open or scheduled, plus closed ones they
    // answered. Tap an event to RSVP or change the answer.
    // ============================================================
    public partial class RsvpPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private bool _loading;

        public RsvpPage()
        {
            InitializeComponent();
            Responsive.Adapt(this, Root);
        }

        // Reload every time, so an answer given on EventRsvpPage shows
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private async void OnRefreshing(object sender, EventArgs e)
        {
            await LoadAsync();
            Refresher.IsRefreshing = false;
        }

        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;

            var result = await _api.GetMyEventsAsync();
            _loading = false;

            Root.Children.Clear();
            Root.Children.Add(Ui.PageHeader("My Event Invitations",
                "Events you're invited to. Open one to RSVP or change your answer while RSVPs are open.", "Invitations"));

            if (!result.Ok)
            {
                Root.Children.Add(Ui.Banner(BannerKind.Warning, "Events not available", result.Error));
                return;
            }

            if (result.Events.Count == 0)
            {
                Root.Children.Add(Ui.Banner(BannerKind.Info, null, "You have no event invitations right now."));
                return;
            }

            // 2–3 per row on wide screens
            var grid = new AdaptiveGrid(320);
            foreach (var evt in result.Events)
            {
                grid.Children.Add(EventCard(evt));
            }
            Root.Children.Add(grid);
        }

        private View EventCard(MyEventSummaryDto evt)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };

            // RSVP status + their answer (web: square badges)
            var badges = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
            var status = Ui.Badge(evt.StatusLabel, evt.IsOpen ? Tone.Ok : evt.IsScheduled ? Tone.Low : Tone.Muted);
            status.Margin = new Thickness(0, 2, 6, 2);
            badges.Children.Add(status);

            if (evt.Answer != null)
            {
                var answer = Ui.Badge(evt.Answer, Tone.Info);
                answer.Margin = new Thickness(0, 2, 6, 2);
                badges.Children.Add(answer);
            }
            stack.Children.Add(badges);

            stack.Children.Add(new Label { Text = evt.EventName, Style = Ui.Style("CardTitle"), FontSize = 20 });
            stack.Children.Add(Ui.Mono(evt.DateLabel, caps: true));
            stack.Children.Add(Ui.Mono(evt.TimeLabel + (string.IsNullOrEmpty(evt.VenueName) ? "" : " · " + evt.VenueName), caps: true));

            // Same button wording as the web page
            var button = new Button
            {
                Text = evt.IsOpen ? (evt.HasResponded ? "View / change RSVP" : "RSVP now") : "View",
                Style = Ui.Style(evt.IsOpen && !evt.HasResponded ? "PrimaryButton" : "SecondaryButton"),
                Margin = new Thickness(0, 6, 0, 0)
            };
            button.Clicked += async (s, e) => await Navigation.PushAsync(new EventRsvpPage(evt.EventId));
            stack.Children.Add(button);

            var card = Ui.Card(stack);
            card.OnTap(async () => await Navigation.PushAsync(new EventRsvpPage(evt.EventId)));
            return card;
        }
    }
}
