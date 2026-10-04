using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;

namespace MhmsMobileApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                // QR scanner (residence sign in / sign out)
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                    // Same fonts as the web app (Montserrat body, Playfair Display headings)
                    fonts.AddFont("Montserrat-Regular.ttf", "Montserrat");
                    fonts.AddFont("Montserrat-SemiBold.ttf", "MontserratSemiBold");
                    fonts.AddFont("Montserrat-Bold.ttf", "MontserratBold");
                    fonts.AddFont("PlayfairDisplay-Bold.ttf", "PlayfairBold");
                });

            // Text boxes sit inside our own rounded border, so hide
            // Android's default underline
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
            });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
