using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Threading.Tasks;

namespace MhmsMobileApp.Views
{
    public partial class SquadPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();

        public SquadPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.EventArgs e)
        {
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            LoadingSpinner.IsVisible = true;
            LoadingSpinner.IsRunning = true;
            ContentStack.IsVisible = false;
            ErrorLabel.IsVisible = false;

            var data = await _api.GetSquadAsync();

            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;

            if (data == null || !data.Ok)
            {
                ErrorLabel.Text = data == null ? "No response." : data.Error;
                ErrorLabel.IsVisible = true;
                return;
            }

            RenderSports(data);
            ContentStack.IsVisible = true;
        }

        private void RenderSports(SquadListDto data)
        {
            SportsContainer.Children.Clear();

            if (data.Sports == null || data.Sports.Count == 0)
            {
                SportsContainer.Children.Add(new Label
                {
                    Text = "No sports registered yet.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#9ca3af")
                });
                return;
            }

            foreach (var sport in data.Sports)
            {
                SportsContainer.Children.Add(new Label
                {
                    Text = sport.SportName.ToUpper() + "  (" + sport.ActiveCount + " of " + sport.TotalCount + " active)",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#6b7280"),
                    Margin = new Thickness(0, 8, 0, 0)
                });

                foreach (var student in sport.Students)
                {
                    var frame = new Frame
                    {
                        BorderColor = Color.FromArgb("#e5e7eb"),
                        CornerRadius = 8,
                        Padding = 12,
                        BackgroundColor = student.IsActive ? Colors.White : Color.FromArgb("#fef2f2")
                    };

                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto }
                        }
                    };

                    var leftStack = new VerticalStackLayout { Spacing = 2 };

                    leftStack.Children.Add(new Label
                    {
                        Text = student.StudentName,
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#111827")
                    });

                    leftStack.Children.Add(new Label
                    {
                        Text = student.StudentNumber,
                        FontSize = 11,
                        TextColor = Color.FromArgb("#9ca3af")
                    });

                    if (!student.IsActive && !string.IsNullOrWhiteSpace(student.StatusReason))
                    {
                        leftStack.Children.Add(new Label
                        {
                            Text = student.StatusReason,
                            FontSize = 12,
                            TextColor = Color.FromArgb("#991b1b")
                        });
                    }

                    Grid.SetColumn(leftStack, 0);
                    row.Children.Add(leftStack);

                    var statusLabel = new Label
                    {
                        Text = student.IsActive ? "ACTIVE" : "OUT",
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = student.IsActive
                            ? Color.FromArgb("#065f46")
                            : Color.FromArgb("#991b1b"),
                        VerticalOptions = LayoutOptions.Center
                    };
                    Grid.SetColumn(statusLabel, 1);
                    row.Children.Add(statusLabel);

                    frame.Content = row;

                    var statusId = student.StatusId;
                    var isActive = student.IsActive;
                    var studentName = student.StudentName;

                    var tap = new TapGestureRecognizer();
                    tap.Tapped += async (s, e) => await OnStudentTapped(statusId, isActive, studentName);
                    frame.GestureRecognizers.Add(tap);

                    SportsContainer.Children.Add(frame);
                }
            }
        }

        private async Task OnStudentTapped(int statusId, bool currentlyActive, string studentName)
        {
            if (currentlyActive)
            {
                string reason = await DisplayPromptAsync(
                    "Mark Unavailable",
                    "Reason for " + studentName + ":",
                    accept: "Mark OUT",
                    cancel: "Cancel",
                    placeholder: "Injured",
                    maxLength: 200);

                if (string.IsNullOrWhiteSpace(reason)) return;

                var req = new MarkRequestDto
                {
                    StatusId = statusId,
                    IsActive = false,
                    Reason = reason
                };

                var result = await _api.MarkStatusAsync(req);
                if (result == null || !result.Ok)
                {
                    await DisplayAlert("Error", result == null ? "No response." : result.Error, "OK");
                    return;
                }

                await RefreshAsync();
            }
            else
            {
                bool confirm = await DisplayAlert(
                    "Mark Available",
                    "Mark " + studentName + " as available again?",
                    "Yes", "Cancel");

                if (!confirm) return;

                var req = new MarkRequestDto
                {
                    StatusId = statusId,
                    IsActive = true
                };

                var result = await _api.MarkStatusAsync(req);
                if (result == null || !result.Ok)
                {
                    await DisplayAlert("Error", result == null ? "No response." : result.Error, "OK");
                    return;
                }

                await RefreshAsync();
            }
        }
    }
}