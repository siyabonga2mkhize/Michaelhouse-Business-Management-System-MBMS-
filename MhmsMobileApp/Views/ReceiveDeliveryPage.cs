using MhmsMobileApp.Models;
using MhmsMobileApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MhmsMobileApp.Views
{
    // ============================================================
    // Record Stock Delivery — receive one purchase order
    //
    // Same form and rules as the web page IngredientDelivery/Record:
    //   1. (optional) photo of the supplier's invoice — the server reads
    //      it and matches the items to ingredients (nothing is saved)
    //   2. check every item: quantity received, "Received — record this
    //      item", the ingredient for unmatched invoice items, "Not on the
    //      order — I confirm", "Accept the extra" with a note
    //   3. Confirm delivery & update stock
    //      (IngredientDeliveryService.Record checks everything again)
    // ============================================================
    public class ReceiveDeliveryPage : ContentPage
    {
        private readonly ApiService _api = new ApiService();
        private readonly int _orderId;

        private DeliveryFormDto? _form;
        private readonly List<LineEditor> _editors = new List<LineEditor>();

        private readonly VerticalStackLayout _root = new VerticalStackLayout { Padding = new Thickness(16, 20, 16, 24), Spacing = 14 };
        private readonly ScrollView _scroll = new ScrollView();
        private readonly ContentView _errorHost = new ContentView();
        private readonly Button _confirm = new Button { Text = "Confirm delivery & update stock", Style = Ui.Style("PrimaryButton") };
        private readonly Border _confirmBar = new Border { IsVisible = false };

        private Entry? _invoiceNumber;
        private CheckBox? _hasInvoiceDate;
        private DatePicker? _invoiceDate;
        private Editor? _notes;
        private bool _busy;

        // One item on the form and the controls that edit it
        private class LineEditor
        {
            public DeliveryFormLineDto Line = new DeliveryFormLineDto();
            public Entry Quantity = new Entry();
            public CheckBox Include = new CheckBox();
        }

        public ReceiveDeliveryPage(int orderId)
        {
            _orderId = orderId;
            Title = "Receive Delivery";

            _root.Children.Add(new ActivityIndicator { IsRunning = true, Margin = new Thickness(0, 40, 0, 0) });
            _scroll.Content = _root;

            _confirm.Clicked += async (s, e) => await ConfirmAsync();
            _confirmBar.BackgroundColor = Ui.Color("White");
            _confirmBar.Stroke = Ui.Color("CardBorder");
            _confirmBar.StrokeThickness = 1;
            _confirmBar.Padding = new Thickness(16, 12, 16, 16);
            _confirmBar.Content = _confirm;
            _confirm.MaximumWidthRequest = 560;

            var page = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
            page.Add(_scroll, 0, 0);
            page.Add(_confirmBar, 0, 1);
            Content = page;

            Responsive.Adapt(this, _root);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_form == null) await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var form = await _api.GetDeliveryFormAsync(_orderId);
            if (!form.Ok)
            {
                _root.Children.Clear();
                _root.Children.Add(Ui.Banner(BannerKind.Warning, "Order not available", form.Error));
                return;
            }

            Render(form);
        }

        // ============================================================
        // RENDER
        // ============================================================

        private void Render(DeliveryFormDto form)
        {
            _form = form;
            _editors.Clear();
            _root.Children.Clear();
            _errorHost.Content = null;

            _root.Children.Add(Ui.PageHeader("Order " + form.PoNumber, form.Supplier, "Record Delivery"));
            _root.Children.Add(Ui.Text("Check every item before confirming. Stock only changes when you confirm.", "MutedText"));
            _root.Children.Add(_errorHost);

            foreach (var m in form.Messages)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Info, null, m));
            }

            _root.Children.Add(form.FromScan ? InvoiceAttachedCard(form) : ScanCard(form));
            _root.Children.Add(InvoiceCard(form));

            _root.Children.Add(new Label { Text = "3 · Items received", Style = Ui.Style("FieldLabel"), Margin = new Thickness(0, 6, 0, 0) });
            if (form.Lines.Count == 0)
            {
                _root.Children.Add(Ui.Banner(BannerKind.Info, null, "Nothing is outstanding on this order."));
            }

            // One card per item; side by side on wide screens
            var lines = new AdaptiveGrid(340);
            foreach (var line in form.Lines)
            {
                lines.Children.Add(LineCard(form, line));
            }
            _root.Children.Add(lines);

            _notes = new Editor { Text = form.Notes, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 70, MaxLength = 1000, Placeholder = "Optional" };
            _root.Children.Add(Section("Notes", Ui.InputBox(_notes)));

            _confirmBar.IsVisible = true;
        }

        // Card with the web's black header bar
        private static Border Section(string title, View content)
        {
            return Ui.Section(title, content);
        }

        // ── 1. Invoice photo ──
        private View ScanCard(DeliveryFormDto form)
        {
            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(Ui.Text(form.ScanAvailable
                ? "Optional. The items are read from the invoice and matched to ingredients for you to check. Nothing is saved until you confirm."
                : "Optional. Invoice reading isn't set up on this server, but the invoice will still be attached to the delivery.", "MutedText"));

            var take = new Button { Text = "Take Photo of Invoice", Style = Ui.Style("SecondaryButton") };
            take.Clicked += async (s, e) => await ScanAsync(take);
            if (!PhotoHelper.CanTakePhoto)
            {
                take.IsEnabled = false;
                stack.Children.Add(Ui.Text("This device has no camera the app can use.", "MutedText"));
            }
            stack.Children.Add(take);

            return Ui.Section("1 · Scan the supplier's invoice", stack);
        }

        private static View InvoiceAttachedCard(DeliveryFormDto form)
        {
            var text = "Invoice attached: " + form.InvoiceFileName;
            if (!string.IsNullOrEmpty(form.DetectedSupplier)) text += "\nInvoice says: " + form.DetectedSupplier;
            return Ui.Banner(BannerKind.Success, null, text);
        }

        private async Task ScanAsync(Button take)
        {
            if (_busy || _form == null) return;
            _busy = true;

            try
            {
                byte[]? photo;
                try
                {
                    photo = await PhotoHelper.TakePhotoAsync(PhotoHelper.DocumentSize);
                }
                catch (PermissionException)
                {
                    ShowError("Allow Michaelhouse to use the camera (Android Settings → Apps → Michaelhouse → Permissions), then try again.");
                    return;
                }
                catch (Exception ex)
                {
                    ShowError("Camera problem: " + ex.Message);
                    return;
                }

                if (photo == null) return;   // camera closed without a photo

                take.IsEnabled = false;
                take.Text = "Reading the invoice...";

                var scanned = await _api.ScanInvoiceAsync(_orderId, photo);

                take.IsEnabled = true;
                take.Text = "Take Photo of Invoice";

                if (!scanned.Ok)
                {
                    ShowError(scanned.Error);
                    return;
                }

                Render(scanned);
                await _scroll.ScrollToAsync(0, 0, true);
            }
            finally
            {
                _busy = false;
            }
        }

        // ── 2. Invoice number and date ──
        private View InvoiceCard(DeliveryFormDto form)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };

            stack.Children.Add(new Label { Text = "Invoice number", Style = Ui.Style("FieldLabel") });
            _invoiceNumber = new Entry { Text = form.InvoiceNumber, MaxLength = 60, FontSize = 16 };
            stack.Children.Add(Ui.InputBox(_invoiceNumber));

            DateTime date;
            bool hasDate = DateTime.TryParseExact(form.InvoiceDate ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

            _invoiceDate = new DatePicker
            {
                Date = hasDate ? date : DateTime.Today,
                MaximumDate = DateTime.Today.AddDays(1),
                Format = "dd MMM yyyy",
                FontSize = 16,
                IsEnabled = hasDate
            };
            var dateRow = Ui.CheckRow("Invoice date", hasDate, on => { if (_invoiceDate != null) _invoiceDate.IsEnabled = on; }, out var hasDateBox);
            _hasInvoiceDate = hasDateBox;
            stack.Children.Add(dateRow);
            stack.Children.Add(Ui.InputBox(_invoiceDate));

            return Ui.Section("2 · Invoice details", stack);
        }

        // ── 3. One item ──
        private View LineCard(DeliveryFormDto form, DeliveryFormLineDto line)
        {
            var editor = new LineEditor { Line = line };
            _editors.Add(editor);

            var stack = new VerticalStackLayout { Spacing = 8 };

            if (!string.IsNullOrEmpty(line.InvoiceDescription))
            {
                stack.Children.Add(Ui.Mono("Invoice · " + line.InvoiceDescription + " · " + Num(line.InvoiceQuantity) + " " + line.InvoiceUnit, caps: true));
            }
            if (!string.IsNullOrEmpty(line.MatchMessage))
            {
                stack.Children.Add(Ui.Banner(line.Matched ? BannerKind.Success : BannerKind.Danger, null, line.MatchMessage));
            }
            if (!string.IsNullOrEmpty(line.UnitMessage))
            {
                stack.Children.Add(Ui.Banner(BannerKind.Warning, null, line.UnitMessage));
            }

            var quantityLabel = new Label { Style = Ui.Style("FieldLabel") };
            void SetQuantityLabel() => quantityLabel.Text = "Quantity received (" + (string.IsNullOrEmpty(line.Unit) ? "kg / L" : line.Unit) + ")";

            if (line.IsOnOrder)
            {
                stack.Children.Add(new Label { Text = line.Ingredient, Style = Ui.Style("CardTitle") });
                stack.Children.Add(Ui.Mono("Ordered " + Num(line.Ordered) + " " + line.Unit + " · outstanding " + Num(line.Outstanding) + " " + line.Unit, caps: true));
            }
            else
            {
                // Unmatched invoice item: choose the ingredient (web: "Matched ingredient")
                stack.Children.Add(new Label { Text = "Matched ingredient", Style = Ui.Style("FieldLabel") });
                var options = form.IngredientOptions;
                var picker = new Picker { Title = "— Choose the ingredient —", FontSize = 16 };
                foreach (var o in options) picker.Items.Add(o.Name + " (" + o.Unit + ")");
                picker.SelectedIndex = line.IngredientId.HasValue ? options.FindIndex(o => o.Id == line.IngredientId.Value) : -1;
                picker.SelectedIndexChanged += (s, e) =>
                {
                    if (picker.SelectedIndex < 0) return;
                    var chosen = options[picker.SelectedIndex];
                    line.IngredientId = chosen.Id;
                    line.Ingredient = chosen.Name;
                    line.Unit = chosen.Unit;
                    SetQuantityLabel();
                };
                stack.Children.Add(Ui.InputBox(picker));
            }

            SetQuantityLabel();
            stack.Children.Add(quantityLabel);
            editor.Quantity = new Entry { Text = Num(line.Quantity), Keyboard = Keyboard.Numeric, FontFamily = "MonoSemiBold", FontSize = 18, Placeholder = "0" };
            stack.Children.Add(Ui.InputBox(editor.Quantity));

            var card = Ui.Card(stack);

            stack.Children.Add(Ui.CheckRow("Received — record this item", line.Include, on =>
            {
                line.Include = on;
                card.Opacity = on ? 1 : 0.6;
            }, out editor.Include));
            card.Opacity = line.Include ? 1 : 0.6;

            if (!line.IsOnOrder && form.PurchaseOrderId.HasValue)
            {
                stack.Children.Add(Ui.CheckRow("Not on the order — I confirm it was delivered", line.NotOnOrderConfirmed,
                    on => line.NotOnOrderConfirmed = on, out _));
            }

            // "More than ordered?" — accept the extra with a note
            var extra = new VerticalStackLayout { Spacing = 6, IsVisible = line.AcceptExtra };
            extra.Children.Add(Ui.CheckRow("Accept the extra", line.AcceptExtra, on => line.AcceptExtra = on, out _));
            var note = new Entry { Text = line.Note, MaxLength = 300, Placeholder = "Note (required to accept extra)", FontSize = 15 };
            note.TextChanged += (s, e) => line.Note = e.NewTextValue;
            extra.Children.Add(Ui.InputBox(note));

            var more = new Label { Text = "More than ordered? ▾", FontFamily = "MontserratBold", FontSize = 11, CharacterSpacing = 1, TextTransform = TextTransform.Uppercase, TextColor = Ui.Color("Primary"), Padding = new Thickness(0, 6) };
            more.OnTap(() =>
            {
                extra.IsVisible = !extra.IsVisible;
                more.Text = "More than ordered? " + (extra.IsVisible ? "▴" : "▾");
            });
            stack.Children.Add(more);
            stack.Children.Add(extra);

            return card;
        }

        // ============================================================
        // CONFIRM
        // ============================================================

        private async Task ConfirmAsync()
        {
            if (_busy || _form == null) return;

            var problems = new List<string>();
            var request = new DeliveryRequestDto
            {
                Token = _form.Token,
                PurchaseOrderId = _form.PurchaseOrderId,
                InvoiceNumber = string.IsNullOrWhiteSpace(_invoiceNumber?.Text) ? null : _invoiceNumber!.Text.Trim(),
                InvoiceDate = _hasInvoiceDate?.IsChecked == true ? _invoiceDate?.Date.Date : null,
                InvoiceFilePath = _form.InvoiceFilePath,
                InvoiceFileName = _form.InvoiceFileName,
                Notes = _notes?.Text
            };

            // Only items ticked "Received" with an ingredient (web: unticked
            // or unmatched items are left out)
            foreach (var e in _editors.Where(x => x.Line.Include))
            {
                var line = e.Line;
                string name = line.Ingredient ?? line.InvoiceDescription ?? "An item";

                if (!line.IngredientId.HasValue)
                {
                    problems.Add(name + ": choose the ingredient, or untick \"Received\".");
                    continue;
                }

                decimal qty;
                if (!TryParseQuantity(e.Quantity.Text, out qty) || qty <= 0m)
                {
                    problems.Add(name + ": enter the quantity received, or untick \"Received\".");
                    continue;
                }

                request.Lines.Add(new DeliveryLineRequestDto
                {
                    PurchaseOrderLineId = line.PurchaseOrderLineId,
                    IngredientId = line.IngredientId.Value,
                    Quantity = qty,
                    AcceptExtra = line.AcceptExtra,
                    NotOnOrderConfirmed = line.NotOnOrderConfirmed,
                    Note = line.Note,
                    InvoiceDescription = line.InvoiceDescription,
                    InvoiceQuantity = line.InvoiceQuantity,
                    InvoiceUnit = line.InvoiceUnit
                });
            }

            if (problems.Count > 0)
            {
                ShowError(string.Join("\n", problems));
                return;
            }

            bool yes = await DisplayAlert("Confirm delivery?",
                request.Lines.Count + " item(s) will be added to stock for order " + _form.PoNumber + ".",
                "Confirm", "Cancel");
            if (!yes) return;

            _busy = true;
            _confirm.IsEnabled = false;
            _confirm.Text = "Recording...";

            var result = await _api.RecordDeliveryAsync(request);

            _busy = false;
            _confirm.IsEnabled = true;
            _confirm.Text = "Confirm delivery & update stock";

            if (!result.Ok)
            {
                ShowError(result.Error ?? "The delivery wasn't recorded.");
                return;
            }

            await DisplayAlert("Delivery recorded", result.Message, "OK");
            await Navigation.PopAsync();
        }

        // Accepts "12.5" and "12,5"
        private static bool TryParseQuantity(string? text, out decimal value)
        {
            var t = (text ?? "").Trim().Replace(',', '.');
            return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static string Num(decimal? value) =>
            value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";

        // Web: "Not recorded: ..."
        private async void ShowError(string message)
        {
            _errorHost.Content = Ui.Banner(BannerKind.Danger, "Not recorded:", message);
            await _scroll.ScrollToAsync(0, 0, true);
        }
    }
}
