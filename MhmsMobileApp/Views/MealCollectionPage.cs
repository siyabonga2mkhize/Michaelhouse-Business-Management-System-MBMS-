using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using System;
using System.IO;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // UC16 — Meal Collection (Chef / Cafeteria Manager / Admin)
    //
    // Same steps and rules as the web Collection Terminal:
    //   1. take a photo of the student's face
    //   2. the server verifies it (POST /api/mealcollection/verify):
    //      who they are, the meal being served now, that they chose a
    //      meal for it and haven't collected it — and returns a
    //      one-time code
    //   3. staff check the result and confirm
    //      (POST /api/mealcollection/record with that code)
    // A face match alone never records a collection.
    // ============================================================
    public class MealCollectionPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();

        private readonly ContentView _statusHost = new ContentView();
        private readonly Label _todayLabel = new Label();
        private readonly ContentView _stepHost = new ContentView();
        private readonly ScrollView _scroll = new ScrollView();

        private string? _pendingToken;
        private bool _busy;

        public MealCollectionPage()
        {
            Title = "Meal Collection";

            _todayLabel.Style = Ui.Style("MonoCaps");

            var root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 32), Spacing = 14 };
            root.Children.Add(Ui.PageHeader("Meal Collection", "Collection terminal", "Service"));
            root.Children.Add(_todayLabel);
            root.Children.Add(_statusHost);
            root.Children.Add(_stepHost);

            _scroll.Content = root;
            Content = _scroll;

            Responsive.Adapt(this, root, bottom: 32);

            ShowStart();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshStatusAsync();
        }

        // "Now serving" banner — the meal is decided by the server from
        // school time (or a manual opening by the Cafeteria Manager)
        private async Task RefreshStatusAsync()
        {
            var status = await _api.GetCollectionStatusAsync();

            if (!status.Ok)
            {
                _statusHost.Content = Ui.Banner(BannerKind.Warning, "Can't load the collection status", status.Error);
                return;
            }

            _todayLabel.Text = "Kitchen staff only · " + status.TodayCount + " meal" + (status.TodayCount == 1 ? "" : "s") + " collected today";

            if (status.CurrentSlot != null)
            {
                _statusHost.Content = Ui.Banner(status.IsManualOpening ? BannerKind.Warning : BannerKind.Success,
                    "Now serving: " + status.CurrentSlot, status.CurrentSlotHours);
            }
            else
            {
                _statusHost.Content = Ui.Banner(BannerKind.Info, "No meal is being served right now",
                    status.CollectionHours + "\nThe Cafeteria Manager can open collection outside these hours on the website's Collection Terminal.");
            }
        }

        // ============================================================
        // STEP 1 — take a photo
        // ============================================================

        private void ShowStart(string? note = null)
        {
            _pendingToken = null;

            var stack = new VerticalStackLayout { Spacing = 14 };
            stack.Children.Add(new Label
            {
                Text = "Take a photo of the student's face",
                Style = Ui.Style("CardTitle"),
                HorizontalTextAlignment = TextAlignment.Center
            });
            stack.Children.Add(new Label
            {
                Text = "Hold the phone so the face fills the middle of the picture, in good light.",
                Style = Ui.Style("MutedText"),
                HorizontalTextAlignment = TextAlignment.Center
            });

            if (!string.IsNullOrEmpty(note))
            {
                stack.Children.Add(Ui.Banner(BannerKind.Success, null, note));
            }

            var take = new Button { Text = "Take Photo", Style = Ui.Style("PrimaryButton") };
            take.Clicked += async (s, e) => await TakePhotoAsync();
            stack.Children.Add(take);

            if (!PhotoHelper.CanTakePhoto)
            {
                take.IsEnabled = false;
                stack.Children.Add(Ui.Banner(BannerKind.Warning, null, "This device has no camera the app can use."));
            }

            _stepHost.Content = Ui.Section("Face scan", stack, null, new Thickness(20));
        }

        private async Task TakePhotoAsync()
        {
            if (_busy) return;
            _busy = true;

            try
            {
                byte[]? photo;
                try
                {
                    photo = await PhotoHelper.TakePhotoAsync();
                }
                catch (PermissionException)
                {
                    ShowError("Camera permission needed",
                        "Allow Michaelhouse to use the camera (Android Settings → Apps → Michaelhouse → Permissions), then try again.");
                    return;
                }
                catch (FeatureNotSupportedException)
                {
                    ShowError("No camera", "This device has no camera the app can use.");
                    return;
                }
                catch (Exception ex)
                {
                    ShowError("Camera problem", ex.Message);
                    return;
                }

                if (photo == null) return;   // camera closed without a photo

                await VerifyAsync(photo);
            }
            finally
            {
                _busy = false;
            }
        }

        // ============================================================
        // STEP 2 — verify on the server
        // ============================================================

        private async Task VerifyAsync(byte[] photo)
        {
            var stack = new VerticalStackLayout { Spacing = 14 };
            stack.Children.Add(PhotoPreview(photo));
            stack.Children.Add(new ActivityIndicator { IsRunning = true });
            stack.Children.Add(new Label { Text = "Verifying...", Style = Ui.Style("CardTitle"), HorizontalOptions = LayoutOptions.Center });
            _stepHost.Content = Ui.Card(stack, new Thickness(20));

            var result = await _api.VerifyFaceAsync(photo);
            ShowResult(result, photo);
        }

        // Height follows the screen width (bigger on tablets), within limits
        private View PhotoPreview(byte[] photo, double fraction = 0.6)
        {
            var preview = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new Rectangle(),
                Content = new Image
                {
                    Source = ImageSource.FromStream(() => new MemoryStream(photo)),
                    Aspect = Aspect.AspectFill
                }
            };
            Responsive.Square(preview, this, fraction, 140, 380, aspect: 1.0);
            return preview;
        }

        // ============================================================
        // STEP 3 — result; confirm when ready (web: showResult)
        // ============================================================

        // note: optional green line at the top, e.g. "✓ Amina collected Dinner at 18:05."
        private void ShowResult(CollectionResultDto r, byte[]? photo, string? note = null)
        {
            bool good = r.IsReady || r.IsCollected;
            _pendingToken = r.IsReady ? r.Token : null;

            var stack = new VerticalStackLayout { Spacing = 10 };

            if (!string.IsNullOrEmpty(note))
            {
                stack.Children.Add(Ui.Banner(BannerKind.Success, null, note));
            }

            if (photo != null && !r.IsCollected)
            {
                stack.Children.Add(PhotoPreview(photo, 0.35));
            }

            stack.Children.Add(new Label
            {
                Text = (good ? "✓ " : "✕ ") + (r.Title ?? ""),
                FontFamily = "MontserratBold",
                FontSize = 13,
                CharacterSpacing = 1,
                TextTransform = TextTransform.Uppercase,
                TextColor = good ? Ui.Color("Success") : Ui.Color("Danger")
            });

            if (!string.IsNullOrEmpty(r.StudentName))
            {
                stack.Children.Add(new Label { Text = r.StudentName, Style = Ui.Style("PageTitle") });
            }
            if (!string.IsNullOrEmpty(r.StudentNumber))
            {
                stack.Children.Add(Ui.Mono("Student no. " + r.StudentNumber, caps: true));
            }

            stack.Children.Add(Ui.Text(r.IsReady ? "Ready for collection." : r.Message));

            // Dietary warnings — shown; staff decide
            if (r.HasDietaryConflict)
            {
                stack.Children.Add(Ui.Banner(BannerKind.Warning, "⚠ Dietary conflict — please review before confirming.",
                    "• " + string.Join("\n• ", r.DietaryConflicts)));
            }

            // The meal, when the server found one
            if (!string.IsNullOrEmpty(r.MealName))
            {
                stack.Children.Add(MealBox(r));
            }

            if (r.IsReady)
            {
                stack.Children.Add(Ui.Text("Confirm within 2 minutes, or scan again.", "MutedText"));

                var confirm = new Button { Text = "Confirm Collection", Style = Ui.Style("PrimaryButton") };
                confirm.Clicked += async (s, e) => await ConfirmAsync(confirm);
                stack.Children.Add(confirm);

                var cancel = new Button { Text = "Cancel", Style = Ui.Style("SecondaryButton") };
                cancel.Clicked += (s, e) => ShowStart();
                stack.Children.Add(cancel);
            }
            else if (r.IsCollected)
            {
                var next = new Button { Text = "Next Student", Style = Ui.Style("PrimaryButton") };
                next.Clicked += async (s, e) => await TakePhotoAsync();
                stack.Children.Add(next);
            }
            else
            {
                var again = new Button { Text = "Try Again", Style = Ui.Style("PrimaryButton") };
                again.Clicked += async (s, e) => await TakePhotoAsync();
                stack.Children.Add(again);

                var cancel = new Button { Text = "Cancel", Style = Ui.Style("SecondaryButton") };
                cancel.Clicked += (s, e) => ShowStart();
                stack.Children.Add(cancel);
            }

            var card = Ui.Card(stack, new Thickness(18));
            card.Stroke = good ? Ui.Color("Success") : Ui.Color("Primary");
            card.StrokeThickness = 2;
            _stepHost.Content = card;

            _ = _scroll.ScrollToAsync(_stepHost, ScrollToPosition.Start, true);
        }

        private static View MealBox(CollectionResultDto r)
        {
            var stack = new VerticalStackLayout { Spacing = 4 };
            stack.Children.Add(new Label { Text = r.MealDate ?? r.MealSlot, Style = Ui.Style("FieldLabel") });
            stack.Children.Add(new Label { Text = r.MealName, Style = Ui.Style("CardTitle") });

            void Info(string label, string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var text = new FormattedString();
                text.Spans.Add(new Span { Text = label + ": ", FontFamily = "MontserratBold" });
                text.Spans.Add(new Span { Text = value });
                stack.Children.Add(new Label { FormattedText = text, FontSize = 13, TextColor = Ui.Color("TextBody") });
            }

            Info("Contains", r.Allergens);
            Info("Student allergies", r.StudentAllergies);
            Info("Dietary preference", r.DietaryPreference);
            Info("Medical dietary", r.MedicalDietaryRestrictions);
            Info("Dietary notes", r.DietaryNotes);

            return new Border
            {
                BackgroundColor = Ui.Color("PageBackground"),
                Stroke = Ui.Color("CardBorder"),
                StrokeShape = new Rectangle(),
                Padding = new Thickness(12),
                Content = stack
            };
        }

        // ============================================================
        // STEP 4 — record
        // ============================================================

        private async Task ConfirmAsync(Button confirm)
        {
            if (_busy || _pendingToken == null) return;
            _busy = true;
            confirm.IsEnabled = false;
            confirm.Text = "Recording...";

            var token = _pendingToken;
            _pendingToken = null;   // single use
            var result = await _api.RecordCollectionAsync(token);
            _busy = false;

            if (result.IsCollected)
            {
                // Web: "✓ Name collected Dinner at 18:05."
                ShowResult(result, null,
                    "✓ " + result.StudentName + " collected " + result.MealSlot + " at " + result.CollectedAt + ".");
                await RefreshStatusAsync();
            }
            else
            {
                ShowResult(result, null);
            }
        }

        private void ShowError(string title, string message)
        {
            ShowResult(new CollectionResultDto { Outcome = "Error", Title = title, Message = message }, null);
        }
    }
}
