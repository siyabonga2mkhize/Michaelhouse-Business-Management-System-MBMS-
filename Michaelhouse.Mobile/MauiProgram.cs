using Microsoft.Extensions.Logging;
using Michaelhouse.Mobile.Services;
using ZXing.Net.Maui.Controls;
namespace Michaelhouse.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseBarcodeReader().ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));
        var apiBaseAddress = "https://10.0.2.2:44321/api/mobile/";
#if DEBUG && ANDROID
        // `adb reverse tcp:65133 tcp:65133` exposes the local IIS Express HTTP
        // binding as the Android device's localhost address during development.
        // The hostname must match the IIS Express localhost binding.
        apiBaseAddress = "http://localhost:65133/api/mobile/";
#endif
        builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiBaseAddress), Timeout = TimeSpan.FromSeconds(120) });
        builder.Services.AddSingleton<MobileApiClient>();
        builder.Services.AddSingleton<SessionService>();
        return builder.Build();
    }
}
