using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;

namespace MhmsMobileApp.Views
{
    public partial class FeastPlanPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();

        public FeastPlanPage()
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

            var plan = await _api.GetFeastPlanAsync();

            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;

            if (plan == null || !plan.Ok)
            {
                ErrorLabel.Text = plan == null ? "No response from server." : plan.Error;
                ErrorLabel.IsVisible = true;
                return;
            }

            // Show the page straight away and fill it in bit by bit, so the
            // screen keeps responding while a big plan is drawn
            ContentStack.IsVisible = true;
            await RenderPlanAsync(plan);
        }

        private async Task RenderPlanAsync(FeastPlanDto plan)
        {
            EventNameLabel.Text = plan.EventName;
            EventMetaLabel.Text = plan.EventDateLabel + "\n" +
                                  plan.StartTime + " - " + plan.EndTime + "\n" +
                                  plan.VenueName +
                                  (string.IsNullOrEmpty(plan.MenuTemplateName)
                                      ? "" : "\n" + plan.MenuTemplateName);

            GuestsLabel.Text = plan.TotalGuests.ToString();
            StdLabel.Text = plan.StandardGuests.ToString();
            VegLabel.Text = plan.VegetarianGuests.ToString();
            OthLabel.Text = plan.OtherGuests.ToString();
            ShortLabel.Text = plan.ShortageCount.ToString();
            ShortLabel.TextColor = plan.ShortageCount > 0
                ? Color.FromArgb("#991b1b")
                : Color.FromArgb("#111827");

            // Dietary notes
            if (plan.DietaryNotes != null && plan.DietaryNotes.Count > 0)
            {
                DietaryFrame.IsVisible = true;
                DietaryLabel.Text = string.Join("\n", plan.DietaryNotes);
            }
            else
            {
                DietaryFrame.IsVisible = false;
            }

            // Timeline
            TimelineContainer.Children.Clear();

            if (plan.Timeline == null || plan.Timeline.Count == 0)
            {
                TimelineContainer.Children.Add(new Label
                {
                    Text = "No timeline entries.",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#9ca3af")
                });
            }
            else
            {
                foreach (var day in plan.Timeline)
                {
                    var frame = new Border
                    {
                        Stroke = Color.FromArgb("#e5e7eb"),
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        Padding = 10,
                        BackgroundColor = Colors.White
                    };

                    var stack = new VerticalStackLayout { Spacing = 4 };

                    var header = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto }
                        }
                    };

                    var dayLabel = new Label
                    {
                        Text = day.DayLabel.ToUpper(),
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#6b7280")
                    };
                    Grid.SetColumn(dayLabel, 0);
                    header.Children.Add(dayLabel);

                    var dateLabel = new Label
                    {
                        Text = day.CalendarDate,
                        FontSize = 11,
                        TextColor = Color.FromArgb("#374151"),
                        FontAttributes = FontAttributes.Bold
                    };
                    Grid.SetColumn(dateLabel, 1);
                    header.Children.Add(dateLabel);

                    stack.Children.Add(header);

                    foreach (var d in day.Dishes)
                    {
                        stack.Children.Add(new Label
                        {
                            Text = "  " + d.DishName + "  (" + (d.PrepMinutes + d.CookMinutes) + " min)",
                            FontSize = 12,
                            TextColor = Color.FromArgb("#374151")
                        });
                    }

                    frame.Content = stack;
                    TimelineContainer.Children.Add(frame);
                    await Task.Delay(1);   // let Android draw and handle taps
                }
            }

            // Dishes
            DishesContainer.Children.Clear();

            foreach (var dish in plan.Dishes)
            {
                var frame = new Border
                    {
                        Stroke = Color.FromArgb("#e5e7eb"),
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Padding = 0,
                    BackgroundColor = Colors.White
                };

                var outerStack = new VerticalStackLayout();

                // Header
                var headerGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    Padding = new Thickness(12, 10, 12, 10),
                    BackgroundColor = Color.FromArgb("#f9fafb")
                };

                var nameStack = new VerticalStackLayout { Spacing = 2 };

                nameStack.Children.Add(new Label
                {
                    Text = dish.Section.ToUpper(),
                    FontSize = 9,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#6b7280")
                });

                nameStack.Children.Add(new Label
                {
                    Text = dish.DishName,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#111827")
                });

                Grid.SetColumn(nameStack, 0);
                headerGrid.Children.Add(nameStack);

                var portionsStack = new VerticalStackLayout
                {
                    HorizontalOptions = LayoutOptions.End
                };

                portionsStack.Children.Add(new Label
                {
                    Text = dish.Portions.ToString(),
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#C21E2E"),
                    HorizontalOptions = LayoutOptions.End
                });

                portionsStack.Children.Add(new Label
                {
                    Text = "portions",
                    FontSize = 9,
                    TextColor = Color.FromArgb("#9ca3af"),
                    HorizontalOptions = LayoutOptions.End
                });

                Grid.SetColumn(portionsStack, 1);
                headerGrid.Children.Add(portionsStack);

                outerStack.Children.Add(headerGrid);

                // Station + times
                var metaStack = new VerticalStackLayout
                {
                    Spacing = 4,
                    Padding = new Thickness(12, 8, 12, 8),
                    BackgroundColor = Color.FromArgb("#fafafa")
                };

                var stationText = string.IsNullOrEmpty(dish.Station) ? "Line" : dish.Station;

                var stationLabel = new Label
                {
                    Text = stationText.ToUpper(),
                    FontSize = 9,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    BackgroundColor = StationColour(stationText),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalOptions = LayoutOptions.Start
                };
                metaStack.Children.Add(stationLabel);

                metaStack.Children.Add(new Label
                {
                    Text = "Start " + dish.StartTime + "  |  Ready " + dish.ReadyTime +
                           "  |  " + (dish.PrepMinutes + dish.CookMinutes) + " min",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#6b7280")
                });

                outerStack.Children.Add(metaStack);

                // Ingredients
                if (dish.Ingredients != null && dish.Ingredients.Count > 0)
                {
                    var ingStack = new VerticalStackLayout
                    {
                        Spacing = 3,
                        Padding = new Thickness(12, 8, 12, 10)
                    };

                    foreach (var ing in dish.Ingredients)
                    {
                        var row = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitionCollection
                            {
                                new ColumnDefinition { Width = GridLength.Star },
                                new ColumnDefinition { Width = GridLength.Auto }
                            }
                        };

                        row.Children.Add(new Label
                        {
                            Text = ing.Name,
                            FontSize = 11,
                            TextColor = Color.FromArgb("#4b5563")
                        });

                        var amountText = ing.TotalRequired.ToString("N2") + " " + ing.Unit;
                        var amountLabel = new Label
                        {
                            Text = amountText,
                            FontSize = 11,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = ing.IsCovered
                                ? Color.FromArgb("#065f46")
                                : Color.FromArgb("#991b1b")
                        };
                        Grid.SetColumn(amountLabel, 1);
                        row.Children.Add(amountLabel);

                        ingStack.Children.Add(row);
                    }

                    outerStack.Children.Add(ingStack);
                }

                frame.Content = outerStack;
                DishesContainer.Children.Add(frame);
                await Task.Delay(1);   // let Android draw and handle taps
            }

            // Total ingredients
            IngredientsContainer.Children.Clear();

            if (plan.TotalIngredients == null || plan.TotalIngredients.Count == 0)
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
                foreach (var ing in plan.TotalIngredients)
                {
                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto }
                        },
                        Padding = new Thickness(0, 4, 0, 4)
                    };

                    row.Children.Add(new Label
                    {
                        Text = ing.Name,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#374151")
                    });

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
                    if (IngredientsContainer.Children.Count % 20 == 0) await Task.Delay(1);
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