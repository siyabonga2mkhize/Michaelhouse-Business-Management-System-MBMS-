using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // UC13 — My Meal Plan (logged-in student)
    //
    // Same content and rules as the web page StudentMealPlan/Index:
    // status, deadline note, dietary profile, every day's meals.
    // Choosing happens on ChooseMealPage; the server checks every
    // pick (deadline, menu, dietary profile) — MealPlanService.
    // ============================================================
    public partial class MealPlanPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private MealPlanDto? _plan;
        private bool _loading;

        public MealPlanPage()
        {
            InitializeComponent();
            Responsive.Adapt(this, Root);
        }

        // Reload every time the screen is shown, so a pick made on
        // ChooseMealPage appears straight away
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

            if (_plan == null)
            {
                LoadingSpinner.IsVisible = true;
                LoadingSpinner.IsRunning = true;
            }

            var plan = await _api.GetMyMealPlanAsync();
            _loading = false;

            Root.Children.Clear();

            if (plan == null || !plan.Ok)
            {
                SubmitBar.IsVisible = false;
                Root.Children.Add(Ui.PageHeader("My Meal Plan", null, "Cafeteria"));
                Root.Children.Add(Ui.Banner(BannerKind.Warning, "Meal plan not available",
                    plan == null ? "No response from server." : plan.Error));
                return;
            }

            _plan = plan;
            Render(plan);
        }

        // ============================================================
        // RENDER
        // ============================================================

        private void Render(MealPlanDto plan)
        {
            // ── Header: week, status, progress ──
            Root.Children.Add(Ui.PageHeader("My Meal Plan", plan.WeekLabel, "Cafeteria"));
            Root.Children.Add(StatusRow(plan));

            // ── Status banners (same wording as the web page) ──
            if (plan.Status == "SentBack" && !string.IsNullOrWhiteSpace(plan.DietitianComment))
            {
                Root.Children.Add(Ui.Banner(BannerKind.Warning, "The Dietitian sent your plan back",
                    plan.DietitianComment + "\nUpdate your picks below and submit again."));
            }

            if (plan.Status == "Approved")
            {
                Root.Children.Add(Ui.Banner(BannerKind.Success, "Your plan has been approved",
                    string.IsNullOrWhiteSpace(plan.DietitianComment) ? "You're all set for the week." : plan.DietitianComment));
            }

            if (plan.Status == "Submitted" || plan.Status == "SubmittedToDietitian")
            {
                Root.Children.Add(Ui.Banner(BannerKind.Success, "Your meal plan has been submitted",
                    "Submitted" + (plan.SubmittedAt != null ? " on " + plan.SubmittedAt : "") +
                    " — your choices go straight to the kitchen."));
            }

            var deadline = "For example, Wednesday's meals lock at the start of Tuesday.";
            if (plan.LockedSlotCount > 0) deadline += " " + plan.LockedSlotCount + " meal(s) this week are already locked.";
            if (plan.IsSubmitted) deadline += " You can still change open meals after submitting.";
            Root.Children.Add(Ui.Banner(BannerKind.Info,
                "Choose or change a meal until the end of the day before it is served", deadline));

            var profile = ProfileCard(plan);
            if (profile != null) Root.Children.Add(profile);

            // ── One card per day; 2–3 per row on wide screens ──
            var days = new AdaptiveGrid(360, 3);
            foreach (var day in plan.Days)
            {
                days.Children.Add(DayCard(day));
            }
            Root.Children.Add(days);

            // ── Submit bar ──
            int stillToChoose = plan.OpenSlotCount - plan.OpenSlotsChosen;
            SubmitBar.IsVisible = plan.IsEditable && plan.CanSubmit;
            SubmitInfoLabel.Text = plan.OpenSlotsChosen + " of " + plan.OpenSlotCount + " open meals chosen" +
                                   (stillToChoose > 0 ? " · " + stillToChoose + " still to choose" : "");
        }

        private View StatusRow(MealPlanDto plan)
        {
            Tone tone;
            switch (plan.Status)
            {
                case "Submitted":
                case "SubmittedToDietitian":
                case "Approved":
                    tone = Tone.Ok; break;
                case "SentBack":
                    tone = Tone.Bad; break;
                default:
                    tone = Tone.Muted; break;
            }

            double progress = plan.OpenSlotCount == 0 ? 1 : (double)plan.OpenSlotsChosen / plan.OpenSlotCount;

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                RowSpacing = 8
            };

            var progressText = new Label
            {
                Text = plan.OpenSlotsChosen + " of " + plan.OpenSlotCount + " open meals chosen",
                Style = Ui.Style("MonoCaps"),
                TextColor = Ui.Color("TextPrimary"),
                VerticalOptions = LayoutOptions.Center
            };
            grid.Add(progressText, 0, 0);
            grid.Add(Ui.Badge(plan.StatusText, tone), 1, 0);

            var bar = new ProgressBar { Progress = progress, ProgressColor = Ui.Color("Primary") };
            grid.Add(bar, 0, 1);
            Grid.SetColumnSpan(bar, 2);

            return Ui.Card(grid);
        }

        // "How your options are chosen" — tap to show / hide
        private View? ProfileCard(MealPlanDto plan)
        {
            var lines = new[]
            {
                Line("Dietary preference", plan.DietaryPreference, null),
                Line("Allergies", plan.Allergies, " — these meals are excluded"),
                Line("Medical dietary restrictions", plan.MedicalDietaryRestrictions, null),
                Line("Medical conditions", plan.MedicalConditions, null),
                Line("Sports", plan.Sports, " — options are ordered for your training and matches")
            }.Where(l => l != null).ToList();

            if (lines.Count == 0) return null;

            var details = new VerticalStackLayout { Spacing = 6, IsVisible = false, Margin = new Thickness(0, 10, 0, 0) };
            details.Children.Add(Ui.Text(
                "You see every option on this week's menu. Options that don't suit you are greyed out with the reason and can't be chosen.",
                "MutedText"));
            foreach (var l in lines) details.Children.Add(l!);
            details.Children.Add(Ui.Text("Update your dietary profile on the Michaelhouse website.", "MutedText"));

            var arrow = new Label { Text = "▾", FontSize = 18, TextColor = Ui.Color("TextSecondary"), VerticalOptions = LayoutOptions.Center };
            var header = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            header.Add(new Label { Text = "How your options are chosen", Style = Ui.Style("FieldLabel"), TextColor = Ui.Color("TextPrimary"), VerticalOptions = LayoutOptions.Center }, 0, 0);
            header.Add(arrow, 1, 0);

            var stack = new VerticalStackLayout();
            stack.Children.Add(header);
            stack.Children.Add(details);

            var card = Ui.Card(stack);
            card.OnTap(() =>
            {
                details.IsVisible = !details.IsVisible;
                arrow.Text = details.IsVisible ? "▴" : "▾";
            });
            return card;
        }

        private static Label? Line(string label, string? value, string? suffix)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var text = new FormattedString();
            text.Spans.Add(new Span { Text = label + ": ", FontFamily = "MontserratBold" });
            text.Spans.Add(new Span { Text = value + (suffix ?? "") });

            return new Label { FormattedText = text, FontSize = 13, TextColor = Ui.Color("TextBody") };
        }

        private View DayCard(MealPlanDayDto day)
        {
            var stack = new VerticalStackLayout { Spacing = 0 };

            // Day header: the web's black bar
            var header = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                Padding = new Thickness(16, 12),
                BackgroundColor = Ui.Color("SectionBar")
            };
            header.Add(new Label
            {
                Text = day.DayLabel + " · " + day.DateLabel,
                Style = Ui.Style("SectionBarText")
            }, 0, 0);
            header.Add(new Label
            {
                Text = day.Slots.Count(s => s.HasPick) + "/" + day.Slots.Count + " chosen",
                Style = Ui.Style("MonoCaps"),
                TextColor = Color.FromArgb("#B3FFFFFF"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);
            stack.Children.Add(header);

            for (int i = 0; i < day.Slots.Count; i++)
            {
                if (i > 0) stack.Children.Add(Ui.Divider());
                stack.Children.Add(SlotRow(day, day.Slots[i]));
            }

            return Ui.Card(stack, new Thickness(0));
        }

        // One big tappable row per meal
        private View SlotRow(MealPlanDayDto day, MealPlanSlotDto slot)
        {
            var info = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

            // Slot name + match / training tag
            var top = new HorizontalStackLayout { Spacing = 8 };
            top.Children.Add(new Label { Text = slot.MealSlot, Style = Ui.Style("FieldLabel"), VerticalOptions = LayoutOptions.Center });
            var sportTag = SportTag(slot);
            if (sportTag != null) top.Children.Add(sportTag);
            info.Children.Add(top);

            // What's chosen
            string pickText;
            Color pickColour = Ui.Color("TextPrimary");
            string font = "MontserratSemiBold";

            if (slot.IsLocked)
            {
                pickText = !string.IsNullOrEmpty(slot.CurrentPickName)
                    ? slot.CurrentPickName!
                    : "No meal chosen — selection has closed.";
                if (string.IsNullOrEmpty(slot.CurrentPickName)) { pickColour = Ui.Color("TextSecondary"); font = "Montserrat"; }
            }
            else if (slot.CurrentPickNoLongerSuitable)
            {
                pickText = "Choose again — your earlier pick is no longer available to you";
                pickColour = Color.FromArgb("#9A3412");
            }
            else if (slot.CurrentPickMenuItemId.HasValue)
            {
                pickText = slot.CurrentPickName ?? "";
            }
            else if (slot.HasNoSuitableOption)
            {
                pickText = "No suitable option on the menu";
                pickColour = Ui.Color("TextSecondary");
                font = "Montserrat";
            }
            else
            {
                pickText = "Choose a meal";
                pickColour = Ui.Color("Primary");
                font = "MontserratBold";
            }

            info.Children.Add(new Label { Text = pickText, FontFamily = font == "MontserratSemiBold" ? "PlayfairBold" : font, FontSize = 15, TextColor = pickColour });

            info.Children.Add(Ui.Mono(slot.IsLocked ? "Locked" : "Choose by end of " + slot.ChooseByLabel, caps: true));

            if (slot.IsLocked && slot.CurrentPickNoLongerSuitable && !string.IsNullOrEmpty(slot.CurrentPickName))
            {
                info.Children.Add(new Label
                {
                    Text = "This meal no longer fits your dietary profile. Tell the kitchen staff when you collect it.",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#9A3412")
                });
            }

            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                Padding = new Thickness(16, 14),
                MinimumHeightRequest = 72,
                ColumnSpacing = 12
            };
            row.Add(info, 0, 0);

            // Right side: lock, or an arrow showing the row can be tapped
            row.Add(new Label
            {
                Text = slot.IsLocked ? "🔒" : "›",
                FontSize = slot.IsLocked ? 18 : 30,
                TextColor = slot.IsLocked ? Ui.Color("TextMuted") : Ui.Color("Primary"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            if (!slot.IsLocked)
            {
                row.OnTap(async () => await OpenChooser(day, slot));
            }
            else
            {
                row.BackgroundColor = Ui.Color("Gray100");
            }

            return row;
        }

        private static View? SportTag(MealPlanSlotDto slot)
        {
            if (slot.IsMatchDay) return Ui.Badge("Match day", Tone.Info);
            if (slot.IsDayBeforeMatch) return Ui.Badge("Pre-match", Tone.Low);
            if (slot.IsTrainingDay) return Ui.Badge("Training", Tone.Low);
            return null;
        }

        private async Task OpenChooser(MealPlanDayDto day, MealPlanSlotDto slot)
        {
            if (_plan == null) return;
            await Navigation.PushAsync(new ChooseMealPage(_plan.MealPlanId, day, slot));
        }

        // ============================================================
        // SUBMIT — same confirmation as the web page
        // ============================================================

        private async void OnSubmitClicked(object sender, EventArgs e)
        {
            if (_plan == null) return;

            bool yes = await DisplayAlert("Submit your meal plan?",
                "Your choices go to the kitchen. You can still change open meals until the day before each is served.",
                "Submit", "Cancel");
            if (!yes) return;

            SubmitButton.IsEnabled = false;
            SubmitButton.Text = "Submitting...";

            var result = await _api.SubmitMealPlanAsync(_plan.MealPlanId);

            SubmitButton.IsEnabled = true;
            SubmitButton.Text = "Submit Meal Plan";

            if (!result.Ok)
            {
                await DisplayAlert("Not submitted yet", result.Error, "OK");
                return;
            }

            await DisplayAlert("Submitted", result.Message, "OK");
            await LoadAsync();
        }
    }
}
