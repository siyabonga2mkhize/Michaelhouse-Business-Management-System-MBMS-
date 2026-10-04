using MhmsMobileApp.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MhmsMobileApp.Services
{
    // ============================================================
    // Talks to the Michaelhouse web app.
    //
    // Every call runs entirely on a background thread (sending,
    // waiting, reading and parsing the JSON) and only the finished
    // result comes back to the page. Nothing here may block the
    // UI thread — on Android that shows "App isn't responding".
    // ============================================================
    public class ApiService
    {
        // One HttpClient for the whole app, so the login cookies
        // (forms-auth + session) are sent by every page
        private static readonly HttpClient SharedHttp = CreateHttpClient();

        private readonly HttpClient _http = SharedHttp;

        // Raised when the server says we are not logged in
        // (session timed out, or logged out elsewhere)
        public static event EventHandler? SessionExpired;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static HttpClient CreateHttpClient()
        {
            string baseUrl;

#if ANDROID
            baseUrl = "https://10.0.2.2:44321";
#else
            baseUrl = "https://localhost:44321";
#endif

#if ANDROID
            // .NET's own handler (not Android's native one), so the Host
            // header set below is really sent
            var handler = new SocketsHttpHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                // Forms auth answers "not logged in" with a redirect to
                // the HTML login page; we want to see that, not follow it
                AllowAutoRedirect = false,
                // If the website isn't running, say so quickly instead of
                // waiting for the whole request timeout
                ConnectTimeout = TimeSpan.FromSeconds(15)
            };

            // On Android, accept the dev HTTPS certificate (self-signed).
            // On Windows, the dev cert is already trusted by the system.
            handler.SslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;
#else
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                // Forms auth answers "not logged in" with a redirect to
                // the HTML login page; we want to see that, not follow it
                AllowAutoRedirect = false
            };
#endif

            var client = new HttpClient(new LoginRequiredHandler(handler))
            {
                BaseAddress = new Uri(baseUrl),
                // The first request after F5 starts the web app (and Entity
                // Framework), which can take a minute on a slow PC
                Timeout = TimeSpan.FromMinutes(3)
            };

#if ANDROID
            // The emulator reaches the PC's IIS Express at 10.0.2.2, but IIS
            // Express only accepts requests addressed to "localhost"
            // ("Bad Request - Invalid Hostname"), so say that's who we're calling
            client.DefaultRequestHeaders.Host = "localhost:44321";
#endif

            return client;
        }

        // Turns "401" or "redirect to /Account/Login" into a clear error
        // and tells the app to show the login screen again
        private class LoginRequiredHandler : DelegatingHandler
        {
            public LoginRequiredHandler(HttpMessageHandler inner) : base(inner) { }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                var location = response.Headers.Location?.OriginalString ?? "";
                bool loginRequired = response.StatusCode == HttpStatusCode.Unauthorized
                    || ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400
                        && location.IndexOf("/Account/Login", StringComparison.OrdinalIgnoreCase) >= 0);

                if (loginRequired)
                {
                    response.Dispose();
                    SessionExpired?.Invoke(null, EventArgs.Empty);
                    throw new HttpRequestException("Your session has expired. Please log in again.");
                }

                return response;
            }
        }

        // ============================================================
        // HELPERS — all work happens on a background thread
        // ============================================================

        private Task<T> GetAsync<T>(string url, Func<string, T> onError) =>
            SendForJsonAsync(() => _http.GetAsync(url), onError);

        private Task<T> PostAsync<T>(string url, object data, Func<string, T> onError) =>
            SendForJsonAsync(() => _http.PostAsync(url, JsonBody(data)), onError);

        private static StringContent JsonBody(object data) =>
            new StringContent(JsonSerializer.Serialize(data, JsonOpts), Encoding.UTF8, "application/json");

        // Sends a request and reads the JSON reply; any failure becomes
        // an "Ok = false" result with the error message
        private static Task<T> SendForJsonAsync<T>(Func<Task<HttpResponseMessage>> send, Func<string, T> onError)
        {
            return Task.Run(async () =>
            {
                try
                {
                    using (var response = await send().ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return JsonSerializer.Deserialize<T>(json, JsonOpts) ?? onError("No response from server.");
                    }
                }
                catch (TaskCanceledException)
                {
                    return onError("The server took too long to answer. Please try again.");
                }
                catch (Exception ex)
                {
                    return onError(ex.Message);
                }
            });
        }

        // ============================================================
        // Is the website up? GET /api/ping — used by the login screen
        // while the web app is starting
        // ============================================================
        public Task<bool> PingAsync(TimeSpan timeout)
        {
            return Task.Run(async () =>
            {
                try
                {
                    using (var cts = new CancellationTokenSource(timeout))
                    using (var response = await _http.GetAsync("api/ping", cts.Token).ConfigureAwait(false))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        // ============================================================
        // Login — POST /api/auth/login, POST /api/auth/logout
        // ============================================================
        public Task<CurrentUserDto> LoginAsync(string email, string password) =>
            PostAsync("api/auth/login", new LoginRequestDto { Email = email, Password = password },
                error => new CurrentUserDto { Ok = false, Error = "Could not reach the server: " + error });

        public Task LogoutAsync()
        {
            return Task.Run(async () =>
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await _http.PostAsync("api/auth/logout", JsonBody(new { }), cts.Token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Logging out locally still works if the server is unreachable
                }
            });
        }

        // ============================================================
        // UC13 — logged-in student's meal plan
        //   GET  /api/mealplan/mine
        //   POST /api/mealplan/mine/pick
        //   POST /api/mealplan/mine/submit
        // ============================================================
        public Task<MealPlanDto> GetMyMealPlanAsync() =>
            GetAsync("api/mealplan/mine", error => new MealPlanDto { Ok = false, Error = error });

        public Task<ApiResultDto> SaveMealPickAsync(MealPickRequestDto data) =>
            PostAsync("api/mealplan/mine/pick", data, error => new ApiResultDto { Ok = false, Error = error });

        public Task<ApiResultDto> SubmitMealPlanAsync(int mealPlanId) =>
            PostAsync("api/mealplan/mine/submit", new MealPlanSubmitRequestDto { MealPlanId = mealPlanId },
                error => new ApiResultDto { Ok = false, Error = error });

        // ============================================================
        // UC18 — logged-in RSVP ("My Events")
        //   GET  /api/my-events
        //   GET  /api/my-events/{id}
        //   POST /api/my-events/{id}/meal-options
        //   POST /api/my-events/{id}
        // ============================================================
        public Task<MyEventsDto> GetMyEventsAsync() =>
            GetAsync("api/my-events", error => new MyEventsDto { Ok = false, Error = error });

        public Task<EventRsvpDetailDto> GetMyEventAsync(int eventId) =>
            GetAsync($"api/my-events/{eventId}", error => new EventRsvpDetailDto { Ok = false, Error = error });

        public Task<MealOptionsDto> GetEventMealOptionsAsync(int eventId, DietaryFormDto dietary) =>
            PostAsync($"api/my-events/{eventId}/meal-options", new MealOptionsRequestDto { Dietary = dietary },
                error => new MealOptionsDto { Ok = false, Error = error });

        public Task<RsvpSubmitResultDto> SubmitMyRsvpAsync(int eventId, InviteeRsvpRequestDto data) =>
            PostAsync($"api/my-events/{eventId}", data, error => new RsvpSubmitResultDto { Ok = false, Error = error });

        // ============================================================
        // Record Stock Delivery (Cafeteria Manager / Admin)
        //   GET  /api/cafeteria-inventory/open-orders
        //   GET  /api/cafeteria-inventory/delivery-form?orderId=5
        //   POST /api/cafeteria-inventory/scan          (multipart)
        //   POST /api/cafeteria-inventory/deliveries
        // ============================================================
        public Task<OpenOrdersDto> GetOpenOrdersAsync() =>
            GetAsync("api/cafeteria-inventory/open-orders", error => new OpenOrdersDto { Ok = false, Error = error });

        public Task<DeliveryFormDto> GetDeliveryFormAsync(int orderId) =>
            GetAsync($"api/cafeteria-inventory/delivery-form?orderId={orderId}",
                error => new DeliveryFormDto { Ok = false, Error = error });

        // Reads the invoice photo on the server; nothing is saved until the
        // delivery is recorded
        public Task<DeliveryFormDto> ScanInvoiceAsync(int orderId, byte[] photoJpeg) =>
            SendForJsonAsync(() =>
                {
                    var form = new MultipartFormDataContent();
                    var file = new ByteArrayContent(photoJpeg);
                    file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    form.Add(file, "invoice", "invoice-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jpg");
                    form.Add(new StringContent(orderId.ToString()), "orderId");
                    return _http.PostAsync("api/cafeteria-inventory/scan", form);
                },
                error => new DeliveryFormDto { Ok = false, Error = error });

        public Task<DeliveryResultDto> RecordDeliveryAsync(DeliveryRequestDto data) =>
            PostAsync("api/cafeteria-inventory/deliveries", data, error => new DeliveryResultDto { Ok = false, Error = error });

        // ============================================================
        // Squad (not shown in the app for now)
        // ============================================================
        public Task<SquadListDto> GetSquadAsync() =>
            GetAsync("api/squad", error => new SquadListDto { Ok = false, Error = error });

        public Task<MarkResponseDto> MarkStatusAsync(MarkRequestDto data) =>
            PostAsync("api/squad/mark", data, error => new MarkResponseDto { Ok = false, Error = error });

        // ============================================================
        // UC15 — GET /api/production/current
        // ============================================================
        public Task<ProductionPlanDto> GetProductionPlanAsync() =>
            GetAsync("api/production/current", error => new ProductionPlanDto { Ok = false, Error = error });

        // ============================================================
        // UC19 — GET /api/feastplan/current
        // ============================================================
        public Task<FeastPlanDto> GetFeastPlanAsync() =>
            GetAsync("api/feastplan/current", error => new FeastPlanDto { Ok = false, Error = error });

        // ============================================================
        // UC16 — face meal collection (Chef / Cafeteria Manager / Admin)
        //   GET  /api/mealcollection/status
        //   POST /api/mealcollection/verify  { imageBase64 }
        //   POST /api/mealcollection/record  { token }
        // ============================================================
        public Task<CollectionStatusDto> GetCollectionStatusAsync() =>
            GetAsync("api/mealcollection/status", error => new CollectionStatusDto { Ok = false, Error = error });

        public Task<CollectionResultDto> VerifyFaceAsync(byte[] photoJpeg) =>
            PostAsync("api/mealcollection/verify",
                new CollectionVerifyRequestDto { ImageBase64 = Convert.ToBase64String(photoJpeg) },
                error => new CollectionResultDto { Ok = false, Title = "Not verified", Message = error, Error = error });

        public Task<CollectionResultDto> RecordCollectionAsync(string token) =>
            PostAsync("api/mealcollection/record", new CollectionRecordRequestDto { Token = token },
                error => new CollectionResultDto { Ok = false, Title = "Not recorded", Message = error, Error = error });
    }
}
