using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Linq;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // UC15 — Kitchen Plan (Chef / Cafeteria Manager)
    // GET /api/production/current: the plan for the latest published
    // menu, built from students' submitted meal plans.
    // ============================================================
    public partial class ProductionPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();

        public ProductionPage()
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

            var plan = await _api.GetProductionPlanAsync();

            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;
            ContentStack.IsVisible = true;

            HeaderHost.Content = Ui.PageHeader("Kitchen Plan", plan != null && plan.Ok ? plan.WeekLabel : null, "Kitchen");

            if (plan == null || !plan.Ok)
            {
                ErrorHost.Content = Ui.Banner(BannerKind.Warning, "Kitchen plan not available", plan == null ? "No response from server." : plan.Error);
                ErrorHost.IsVisible = true;
                return;
            }

            // Show the page straight away and fill it in day by day, so the
            // screen keeps responding while a big week is drawn
            await RenderPlanAsync(plan);
        }

        private async Task RenderPlanAsync(ProductionPlanDto plan)
        {
            // ── Stat tiles (web: grid of stat cards) ──
            int shortItems = plan.WeekIngredients.Count(i => !i.IsCovered);
            var stats = new AdaptiveGrid(150, 3);
            stats.Children.Add(Ui.Stat("Meals this week", plan.TotalMeals.ToString()));
            stats.Children.Add(Ui.Stat("Status",
                plan.IsProductionConfirmed ? "Confirmed" : "Pending",
                plan.IsProductionConfirmed ? "Chef signed off" : "Awaiting chef sign-off",
                plan.IsProductionConfirmed ? "#047857" : "#A16207"));
            stats.Children.Add(Ui.Stat("Ingredients short", shortItems.ToString(), null, shortItems > 0 ? "#C21E2E" : "#047857"));
            StatsHost.Content = stats;

            // ── One section per day; 2 per row on wide screens ──
            var days = new AdaptiveGrid(380, 2);
            DaysHost.Content = days;

            foreach (var day in plan.Days)
            {
                var dayStack = new VerticalStackLayout { Spacing = 14 };

                foreach (var slot in day.Slots)
                {
                    dayStack.Children.Add(SlotView(slot));
                }

                if (day.Slots.Count == 0)
                {
                    dayStack.Children.Add(Ui.Text("Nothing to prepare.", "MutedText"));
                }

                days.Children.Add(Ui.Section(day.DayLabel, dayStack, day.Slots.Sum(s => s.Portions) + " portions"));

                // Let Android draw and handle taps before the next day
                await Task.Delay(1);
            }

            IngredientsHost.Content = await IngredientsSectionAsync(plan);
        }

        // "LUNCH · SERVE 12:30 · 84 PORTIONS", then the dishes
        private static View SlotView(ProductionSlotDto slot)
        {
            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(Ui.Mono(slot.MealSlot + " · serve " + slot.ServeTime + " · " + slot.Portions + " portions", caps: true));

            foreach (var task in slot.Tasks)
            {
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 10,
                    Padding = new Thickness(0, 6)
                };

                var info = new VerticalStackLayout { Spacing = 4 };
                var nameRow = new HorizontalStackLayout { Spacing = 8 };
                nameRow.Children.Add(Ui.StationBadge(task.Station));
                info.Children.Add(nameRow);
                info.Children.Add(new Label { Text = task.DishName, Style = Ui.Style("CardTitle"), FontSize = 15 });
                info.Children.Add(Ui.Mono("Start " + task.StartTime + " · ready " + task.ReadyTime + " · " + (task.PrepMinutes + task.CookMinutes) + " min"));
                row.Add(info, 0, 0);

                row.Add(new Label
                {
                    Text = task.Portions.ToString(),
                    Style = Ui.Style("StatValue"),
                    FontSize = 20,
                    TextColor = Ui.Color("Primary"),
                    VerticalOptions = LayoutOptions.Center
                }, 1, 0);

                stack.Children.Add(row);
                stack.Children.Add(Ui.Divider());
            }

            return stack;
        }

        // ── Week ingredient totals: 2 columns on wide screens ──
        private static async Task<View> IngredientsSectionAsync(ProductionPlanDto plan)
        {
            if (plan.WeekIngredients.Count == 0)
            {
                return Ui.Section("Week ingredient totals", Ui.Text("No ingredient data available.", "MutedText"));
            }

            var grid = new AdaptiveGrid(300, 2) { RowSpacing = 0, ColumnSpacing = 24 };
            int rows = 0;

            foreach (var ing in plan.WeekIngredients)
            {
                if (++rows % 20 == 0) await Task.Delay(1);

                grid.Children.Add(Ui.AmountRow(ing.Name, ing.TotalRequired.ToString("N2") + " " + ing.Unit, ing.IsCovered,
                    ing.IsCovered ? null : "Short " + ing.Shortfall.ToString("N2")));
            }

            int shortItems = plan.WeekIngredients.Count(i => !i.IsCovered);
            return Ui.Section("Week ingredient totals", grid, shortItems > 0 ? shortItems + " short" : "All in stock");
        }
    }
}
