using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;

namespace MhmsMobileApp.Views
{
    public partial class ProductionPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();

        public ProductionPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, EventArgs e)
        {
            LoadingSpinner.IsVisible = true;
            LoadingSpinner.IsRunning = true;
            ContentStack.IsVisible = false;
            ErrorLabel.IsVisible = false;

            var plan = await _api.GetProductionPlanAsync();

            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;

            if (plan == null || !plan.Ok)
            {
                ErrorLabel.Text = plan == null ? "No response from server." : plan.Error;
                ErrorLabel.IsVisible = true;
                return;
            }

            // Show the page straight away and fill it in day by day, so the
            // screen keeps responding while a big week is drawn
            ContentStack.IsVisible = true;
            await RenderPlanAsync(plan);
        }

        private async Task RenderPlanAsync(ProductionPlanDto plan)
        {
            WeekLabel.Text = plan.WeekLabel;

            if (plan.IsProductionConfirmed)
            {
                StatusLabel.Text = "Production Confirmed";
                StatusLabel.TextColor = Color.FromArgb("#065f46");
            }
            else
            {
                StatusLabel.Text = "Awaiting Chef Sign-Off";
                StatusLabel.TextColor = Color.FromArgb("#92400e");
            }

            DaysContainer.Children.Clear();

            foreach (var day in plan.Days)
            {
                var dayStack = new VerticalStackLayout { Spacing = 8 };

                dayStack.Children.Add(new Label
                {
                    Text = day.DayLabel,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#C21E2E")
                });

                foreach (var slot in day.Slots)
                {
                    // Border, not Frame: much cheaper to draw on Android
                    var slotFrame = new Border
                    {
                        Stroke = Color.FromArgb("#e5e7eb"),
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        Padding = 12,
                        BackgroundColor = Colors.White
                    };

                    var slotStack = new VerticalStackLayout { Spacing = 6 };

                    slotStack.Children.Add(new Label
                    {
                        Text = slot.MealSlot.ToUpper() + "  -  " + slot.ServeTime + "  -  " + slot.Portions + " portions",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#6b7280")
                    });

                    foreach (var task in slot.Tasks)
                    {
                        var taskGrid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitionCollection
                            {
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = GridLength.Star },
                                new ColumnDefinition { Width = GridLength.Auto }
                            },
                            Padding = new Thickness(0, 4, 0, 4)
                        };

                        var stationLabel = new Label
                        {
                            Text = task.Station.ToUpper(),
                            FontSize = 9,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            BackgroundColor = StationColour(task.Station),
                            Padding = new Thickness(6, 2, 6, 2),
                            VerticalOptions = LayoutOptions.Center
                        };
                        Grid.SetColumn(stationLabel, 0);
                        taskGrid.Children.Add(stationLabel);

                        var infoStack = new VerticalStackLayout
                        {
                            Spacing = 2,
                            Margin = new Thickness(10, 0, 10, 0)
                        };

                        infoStack.Children.Add(new Label
                        {
                            Text = task.DishName,
                            FontSize = 14,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#111827")
                        });

                        infoStack.Children.Add(new Label
                        {
                            Text = "Start " + task.StartTime +
                                   "  |  Ready " + task.ReadyTime +
                                   "  |  " + (task.PrepMinutes + task.CookMinutes) + " min",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#9ca3af")
                        });

                        Grid.SetColumn(infoStack, 1);
                        taskGrid.Children.Add(infoStack);

                        var portionsLabel = new Label
                        {
                            Text = task.Portions + "",
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#C21E2E"),
                            VerticalOptions = LayoutOptions.Center
                        };
                        Grid.SetColumn(portionsLabel, 2);
                        taskGrid.Children.Add(portionsLabel);

                        slotStack.Children.Add(taskGrid);

                        slotStack.Children.Add(new BoxView
                        {
                            HeightRequest = 1,
                            Color = Color.FromArgb("#f3f4f6"),
                            Margin = new Thickness(0, 2, 0, 2)
                        });
                    }

                    slotFrame.Content = slotStack;
                    dayStack.Children.Add(slotFrame);
                }

                DaysContainer.Children.Add(dayStack);

                // Let Android draw and handle taps before the next day
                await Task.Delay(1);
            }

            IngredientsContainer.Children.Clear();

            if (plan.WeekIngredients.Count == 0)
            {
                IngredientsContainer.Children.Add(new Label
                {
                    Text = "No ingredient data available.",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#9ca3af")
                });
            }
            else
            {
                int rows = 0;
                foreach (var ing in plan.WeekIngredients)
                {
                    if (++rows % 20 == 0) await Task.Delay(1);

                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto }
                        },
                        Padding = new Thickness(0, 4, 0, 4)
                    };

                    var nameLabel = new Label
                    {
                        Text = ing.Name,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#374151")
                    };
                    Grid.SetColumn(nameLabel, 0);
                    row.Children.Add(nameLabel);

                    var amountText = ing.TotalRequired.ToString("N2") + " " + ing.Unit;
                    if (!ing.IsCovered)
                    {
                        amountText += "  (short " + ing.Shortfall.ToString("N2") + ")";
                    }

                    var amountLabel = new Label
                    {
                        Text = amountText,
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = ing.IsCovered
                            ? Color.FromArgb("#065f46")
                            : Color.FromArgb("#991b1b")
                    };
                    Grid.SetColumn(amountLabel, 1);
                    row.Children.Add(amountLabel);

                    IngredientsContainer.Children.Add(row);
                }
            }
        }

        private static Color StationColour(string station)
        {
            switch (station)
            {
                case "Grill": return Color.FromArgb("#991b1b");
                case "Stove": return Color.FromArgb("#1e40af");
                case "Oven": return Color.FromArgb("#92400e");
                case "Cold Prep": return Color.FromArgb("#065f46");
                default: return Color.FromArgb("#4b5563");
            }
        }
    }
}