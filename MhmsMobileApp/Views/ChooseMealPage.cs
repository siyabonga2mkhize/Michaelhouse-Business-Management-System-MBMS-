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
    // UC13 — Choose one meal (e.g. Monday lunch)
    //
    // Shows every option on the menu for that meal, like the web
    // page: options that don't suit the student are greyed out with
    // the reason. Tapping an option saves it straight away
    // (POST /api/mealplan/mine/pick → MealPlanService.SavePicks,
    // which checks the deadline, the menu and the dietary profile).
    // ============================================================
    public class ChooseMealPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly int _mealPlanId;
        private readonly MealPlanDayDto _day;
        private readonly MealPlanSlotDto _slot;

        private readonly ContentView _errorHost = new ContentView();
        private readonly ActivityIndicator _spinner = new ActivityIndicator { IsVisible = false };
        private bool _saving;

        public ChooseMealPage(int mealPlanId, MealPlanDayDto day, MealPlanSlotDto slot)
        {
            _mealPlanId = mealPlanId;
            _day = day;
            _slot = slot;

            Title = "Choose " + slot.MealSlot;

            var root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 32), Spacing = 14 };

            root.Children.Add(Ui.PageHeader(slot.MealSlot, day.DayLabel + ", " + day.DateLabel, "My Meal Plan"));

            var tags = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
            void Tag(View chip) { chip.Margin = new Thickness(0, 2, 8, 2); tags.Children.Add(chip); }
            if (slot.IsMatchDay) Tag(Ui.Badge("Match day: " + slot.MatchDescription, Tone.Info));
            else if (slot.IsDayBeforeMatch) Tag(Ui.Badge("Pre-match: " + slot.MatchDescription, Tone.Low));
            else if (slot.IsTrainingDay) Tag(Ui.Badge(slot.MatchDescription ?? "Training", Tone.Low));
            Tag(Ui.Badge("Choose by end of " + slot.ChooseByLabel, Tone.Muted));
            root.Children.Add(tags);

            // Same notes as the web page
            if (slot.CurrentPickNoLongerSuitable)
            {
                root.Children.Add(Ui.Banner(BannerKind.Warning, null,
                    "Your earlier pick is no longer available to you (it is not on this menu, or no longer fits your dietary profile). Please choose again."));
            }
            if (slot.HasNoSuitableOption)
            {
                root.Children.Add(Ui.Banner(BannerKind.Info, null,
                    "None of this meal's menu options fit your dietary profile. You can still submit — speak to the cafeteria about an alternative."));
            }
            if (!string.IsNullOrEmpty(slot.SportsNote))
            {
                root.Children.Add(Ui.Banner(BannerKind.Info, null, slot.SportsNote));
            }

            root.Children.Add(_errorHost);
            root.Children.Add(_spinner);

            var available = slot.Options.Where(o => o.IsAvailable).ToList();
            var unavailable = slot.Options.Where(o => !o.IsAvailable).ToList();

            // Options side by side on wide screens (web: lg:grid-cols-3)
            if (available.Count > 0)
            {
                root.Children.Add(new Label { Text = "Tap a meal to choose it", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 6, 0, 0) });
                var grid = new AdaptiveGrid(280, 3);
                foreach (var opt in available) grid.Children.Add(OptionCard(opt, true));
                root.Children.Add(grid);
            }

            if (unavailable.Count > 0)
            {
                root.Children.Add(new Label { Text = "Not available to you", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 10, 0, 0) });
                var grid = new AdaptiveGrid(280, 3);
                foreach (var opt in unavailable) grid.Children.Add(OptionCard(opt, false));
                root.Children.Add(grid);
            }

            if (slot.Options.Count == 0)
            {
                root.Children.Add(Ui.Banner(BannerKind.Info, null, "There are no options on the menu for this meal."));
            }

            Content = new ScrollView { Content = root };

            Responsive.Adapt(this, root, bottom: 32);
        }

        private View OptionCard(MealPlanOptionDto opt, bool canChoose)
        {
            bool isSelected = canChoose && _slot.CurrentPickMenuItemId == opt.MenuItemId;

            var stack = new VerticalStackLayout { Spacing = 6 };

            // "Recommended for your Rugby match" (web: blue label on top)
            if (canChoose && !string.IsNullOrEmpty(opt.Recommendation))
            {
                stack.Children.Add(Ui.Chip("★ " + opt.Recommendation, "#2563EB", "#FFFFFF"));
            }

            var titleRow = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 10
            };
            titleRow.Add(new Label { Text = opt.Name, Style = Ui.Style("CardTitle") }, 0, 0);
            if (isSelected)
            {
                titleRow.Add(Ui.Chip("✓ Your pick", "#C21E2E", "#FFFFFF"), 1, 0);
            }
            stack.Children.Add(titleRow);

            if (!string.IsNullOrEmpty(opt.DietaryClassification))
            {
                stack.Children.Add(Ui.Mono(opt.DietaryClassification, caps: true));
            }

            // Web: "500 kcal · 30g pro · 60g carb · 12g fat" in mono
            var nutrition = new FormattedString();
            nutrition.Spans.Add(new Span { Text = opt.Calories.ToString("0") + " kcal · ", FontFamily = "Mono" });
            nutrition.Spans.Add(new Span { Text = opt.Protein.ToString("0") + "g pro", FontFamily = "MonoSemiBold", TextColor = Color.FromArgb("#2563EB") });
            nutrition.Spans.Add(new Span { Text = " · " + opt.Carbohydrate.ToString("0") + "g carb · " + opt.Fat.ToString("0") + "g fat", FontFamily = "Mono" });
            stack.Children.Add(new Label { FormattedText = nutrition, FontFamily = "Mono", FontSize = 11, TextColor = Ui.Color("TextSecondary") });

            if (canChoose)
            {
                var chips = TagChips(opt);
                if (chips.Children.Count > 0) stack.Children.Add(chips);
            }
            else
            {
                stack.Children.Add(new Label
                {
                    Text = "Not available: " + opt.UnavailableReason,
                    FontFamily = "MontserratBold",
                    FontSize = 11,
                    TextColor = Ui.Color("Primary")
                });
            }

            var card = new Border
            {
                Style = Ui.Style("Card"),
                Content = stack,
                MinimumHeightRequest = 64
            };

            if (isSelected)
            {
                card.Stroke = Ui.Color("Primary");
                card.StrokeThickness = 2;
                card.BackgroundColor = Color.FromArgb("#FEF2F2");
            }

            if (canChoose)
            {
                card.OnTap(async () => await ChooseAsync(opt));
            }
            else
            {
                card.Opacity = 0.55;
                card.BackgroundColor = Ui.Color("Gray100");
            }

            return card;
        }

        // Same tags as the web page, same colours
        private static FlexLayout TagChips(MealPlanOptionDto opt)
        {
            var flex = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };

            void Add(string text, string bg, string fg)
            {
                var chip = Ui.Chip(text, bg, fg, 9);
                chip.Margin = new Thickness(0, 2, 6, 2);
                flex.Children.Add(chip);
            }

            // The sport label is shown on top (Recommendation) when the
            // server sends one; older servers only send the tag
            var sportTag = opt.Tags.FirstOrDefault(t =>
                t == "Match-day recovery" || t == "Pre-match loading" || t == "Training fuel" || t == "Priority sport");
            if (sportTag != null && string.IsNullOrEmpty(opt.Recommendation)) Add(sportTag, "#DBEAFE", "#1E40AF");
            if (opt.Tags.Contains("High protein")) Add("High protein", "#DBEAFE", "#1E40AF");
            if (opt.Tags.Contains("High carb")) Add("High carb", "#FEF3C7", "#92400E");
            if (opt.Tags.Contains("Light")) Add("Light", "#FEE2E2", "#991B1B");
            if (opt.Tags.Contains("Hydration")) Add("Hydration", "#FEE2E2", "#991B1B");
            if (opt.Tags.Contains("Farm-grown")) Add("Farm-grown", "#D1FAE5", "#065F46");

            return flex;
        }

        private async Task ChooseAsync(MealPlanOptionDto opt)
        {
            if (_saving) return;

            // Tapping the current pick again: nothing to change
            if (_slot.CurrentPickMenuItemId == opt.MenuItemId)
            {
                await Navigation.PopAsync();
                return;
            }

            _saving = true;
            _errorHost.Content = null;
            _spinner.IsVisible = true;
            _spinner.IsRunning = true;

            var result = await _api.SaveMealPickAsync(new MealPickRequestDto
            {
                MealPlanId = _mealPlanId,
                Date = _day.Date,
                MealSlot = _slot.MealSlot,
                MenuItemId = opt.MenuItemId
            });

            _spinner.IsRunning = false;
            _spinner.IsVisible = false;
            _saving = false;

            if (!result.Ok)
            {
                // e.g. the deadline passed while the screen was open
                _errorHost.Content = Ui.Banner(BannerKind.Danger, "Not saved", result.Error);
                return;
            }

            // Back to the week; it reloads and shows the new pick
            await Navigation.PopAsync();
        }
    }
}
