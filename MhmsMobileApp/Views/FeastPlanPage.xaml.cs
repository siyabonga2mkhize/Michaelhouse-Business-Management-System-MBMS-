using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using System;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // UC19 — Feast Plan (Cafeteria Manager / Admin)
    // GET /api/feastplan/current: the next event's plan from its
    // RSVPs — guests, dishes, timeline, ingredients.
    // ============================================================
    public partial class FeastPlanPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();

        public FeastPlanPage()
        {
            InitializeComponent();
            Responsive.Adapt(this, ContentStack);
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            LoadingSpinner.IsVisible = true;
            LoadingSpinner.IsRunning = true;
            ContentStack.IsVisible = false;
            ErrorHost.IsVisible = false;

            var plan = await _api.GetFeastPlanAsync();

            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;
            ContentStack.IsVisible = true;

            if (plan == null || !plan.Ok)
            {
                HeaderHost.Content = Ui.PageHeader("Feast Plan", null, "Hospitality & Catering");
                ErrorHost.Content = Ui.Banner(BannerKind.Warning, "Feast plan not available", plan == null ? "No response from server." : plan.Error);
                ErrorHost.IsVisible = true;
                return;
            }

            // Show the page straight away and fill it in bit by bit, so the
            // screen keeps responding while a big plan is drawn
            await RenderPlanAsync(plan);
        }

        private async Task RenderPlanAsync(FeastPlanDto plan)
        {
            var meta = plan.EventDateLabel + " · " + plan.StartTime + "–" + plan.EndTime + " · " + plan.VenueName
                       + (string.IsNullOrEmpty(plan.MenuTemplateName) ? "" : " · " + plan.MenuTemplateName);
            HeaderHost.Content = Ui.PageHeader(plan.EventName, meta, "Feast Plan");

            // ── Stat tiles ──
            var stats = new AdaptiveGrid(130, 5);
            stats.Children.Add(Ui.Stat("Guests", plan.TotalGuests.ToString(), null, "#047857"));
            stats.Children.Add(Ui.Stat("Standard", plan.StandardGuests.ToString()));
            stats.Children.Add(Ui.Stat("Vegetarian", plan.VegetarianGuests.ToString()));
            stats.Children.Add(Ui.Stat("Other diets", plan.OtherGuests.ToString()));
            stats.Children.Add(Ui.Stat("Short", plan.ShortageCount.ToString(), null, plan.ShortageCount > 0 ? "#C21E2E" : null));
            StatsHost.Content = stats;

            // ── Dietary notes ──
            DietaryHost.Content = plan.DietaryNotes != null && plan.DietaryNotes.Count > 0
                ? Ui.Banner(BannerKind.Warning, "Dietary notes", string.Join("\n", plan.DietaryNotes))
                : null;

            // ── Timeline: one card per day ──
            if (plan.Timeline == null || plan.Timeline.Count == 0)
            {
                TimelineHost.Content = Ui.Text("No timeline entries.", "MutedText");
            }
            else
            {
                var timeline = new AdaptiveGrid(300, 3);
                TimelineHost.Content = timeline;

                foreach (var day in plan.Timeline)
                {
                    var stack = new VerticalStackLayout { Spacing = 6 };
                    foreach (var d in day.Dishes)
                    {
                        var row = new Grid
                        {
                            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                            ColumnSpacing = 8
                        };
                        row.Add(Ui.Text(d.DishName), 0, 0);
                        row.Add(Ui.Mono((d.PrepMinutes + d.CookMinutes) + " min"), 1, 0);
                        stack.Children.Add(row);
                    }

                    timeline.Children.Add(Ui.Section(day.DayLabel, stack, day.CalendarDate));
                    await Task.Delay(1);   // let Android draw and handle taps
                }
            }

            // ── Dishes: 2 per row on wide screens ──
            var dishes = new AdaptiveGrid(340, 2);
            DishesHost.Content = dishes;

            foreach (var dish in plan.Dishes)
            {
                dishes.Children.Add(DishCard(dish));
                await Task.Delay(1);   // let Android draw and handle taps
            }

            // ── Total ingredients ──
            if (plan.TotalIngredients == null || plan.TotalIngredients.Count == 0)
            {
                IngredientsHost.Content = Ui.Section("Total ingredients", Ui.Text("No ingredient data available.", "MutedText"));
            }
            else
            {
                var grid = new AdaptiveGrid(300, 2) { RowSpacing = 0, ColumnSpacing = 24 };
                int n = 0;
                foreach (var ing in plan.TotalIngredients)
                {
                    grid.Children.Add(Ui.AmountRow(ing.Name, ing.TotalRequired.ToString("N2") + " " + ing.Unit, ing.IsCovered,
                        ing.IsCovered ? null : "Short " + ing.Shortfall.ToString("N2")));
                    if (++n % 20 == 0) await Task.Delay(1);
                }

                IngredientsHost.Content = Ui.Section("Total ingredients", grid,
                    plan.ShortageCount > 0 ? plan.ShortageCount + " short" : "All in stock");
            }
        }

        private static View DishCard(FeastDishDto dish)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };

            var head = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 10
            };

            var name = new VerticalStackLayout { Spacing = 4 };
            name.Children.Add(new Label { Text = dish.Section, Style = Ui.Style("FieldLabel") });
            name.Children.Add(new Label { Text = dish.DishName, Style = Ui.Style("CardTitle") });
            head.Add(name, 0, 0);

            var portions = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.End };
            portions.Children.Add(new Label { Text = dish.Portions.ToString(), Style = Ui.Style("StatValue"), TextColor = Ui.Color("Primary"), HorizontalOptions = LayoutOptions.End });
            portions.Children.Add(new Label { Text = "portions", Style = Ui.Style("MonoCaps"), HorizontalOptions = LayoutOptions.End });
            head.Add(portions, 1, 0);
            stack.Children.Add(head);

            var station = new HorizontalStackLayout { Spacing = 8 };
            station.Children.Add(Ui.StationBadge(dish.Station));
            stack.Children.Add(station);
            stack.Children.Add(Ui.Mono("Start " + dish.StartTime + " · ready " + dish.ReadyTime + " · " + (dish.PrepMinutes + dish.CookMinutes) + " min", caps: true));

            if (dish.Ingredients != null && dish.Ingredients.Count > 0)
            {
                stack.Children.Add(Ui.Divider());
                foreach (var ing in dish.Ingredients)
                {
                    stack.Children.Add(Ui.AmountRow(ing.Name, ing.TotalRequired.ToString("N2") + " " + ing.Unit, ing.IsCovered));
                }
            }

            return Ui.Card(stack);
        }
    }
}
