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
                "Events you're invited to. Open one to RSVP or change your answer while RSVPs are open."));

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

            foreach (var evt in result.Events)
            {
                Root.Children.Add(EventCard(evt));
            }
        }

        private View EventCard(MyEventSummaryDto evt)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };

            stack.Children.Add(new Label { Text = evt.EventName, FontFamily = "PlayfairBold", FontSize = 20, TextColor = Ui.Color("TextPrimary") });
            stack.Children.Add(new Label
            {
                Text = evt.DateLabel + "\n" + evt.TimeLabel + (string.IsNullOrEmpty(evt.VenueName) ? "" : " · " + evt.VenueName),
                Style = Ui.Style("BodyText")
            });

            // RSVP status + their answer (web: .me-badge)
            var badges = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
            var status = evt.IsOpen ? Ui.Chip(evt.StatusLabel, "#D1FAE5", "#065F46")
                       : evt.IsScheduled ? Ui.Chip(evt.StatusLabel, "#FEF3C7", "#92400E")
                       : Ui.Chip(evt.StatusLabel, "#E5E7EB", "#374151");
            status.Margin = new Thickness(0, 2, 6, 2);
            badges.Children.Add(status);

            if (evt.Answer != null)
            {
                var answer = Ui.Chip(evt.Answer, "#DBEAFE", "#1E40AF");
                answer.Margin = new Thickness(0, 2, 6, 2);
                badges.Children.Add(answer);
            }
            stack.Children.Add(badges);

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
