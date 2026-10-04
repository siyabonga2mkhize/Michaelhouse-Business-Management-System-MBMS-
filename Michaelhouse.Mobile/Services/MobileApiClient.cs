using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
namespace Michaelhouse.Mobile.Services;
public sealed class MobileApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public MobileApiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null) : base(message, inner) => StatusCode = statusCode;
}
public sealed class MobileApiClient
{
    readonly HttpClient h;
    readonly JsonSerializerOptions j = new() { PropertyNameCaseInsensitive = true };
    public MobileApiClient(HttpClient h) => this.h = h;
    public void SetToken(string? t) => h.DefaultRequestHeaders.Authorization = String.IsNullOrEmpty(t) ? null : new AuthenticationHeaderValue("Bearer", t);
    public Task<T?> GetAsync<T>(string p) => SendAsync<T>(() => h.GetAsync(p));
    public Task<T?> PostAsync<T>(string p, object? b) => SendAsync<T>(() => h.PostAsJsonAsync(p, b, j));
    async Task<T?> SendAsync<T>(Func<Task<HttpResponseMessage>> request)
    {
        HttpResponseMessage response;
        try { response = await request(); }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested) { throw new MobileApiException("The request timed out while contacting the server.", null, ex); }
        catch (HttpRequestException ex) { throw new MobileApiException("Unable to reach the server. Check the API URL, device network, and SSL certificate.", null, ex); }
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var detail = TryGetMessage(body);
            throw new MobileApiException($"Server returned HTTP {(int)response.StatusCode} ({response.StatusCode}).{(String.IsNullOrWhiteSpace(detail) ? "" : " " + detail)}", response.StatusCode);
        }
        return JsonSerializer.Deserialize<T>(body, j);
    }
    static string? TryGetMessage(string body)
    {
        try { using var doc = JsonDocument.Parse(body); return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null; }
        catch (JsonException) { return null; }
    }
}
public sealed class SessionService { const string Key = "mobile_token"; public string? Token { get; private set; } public async Task SaveAsync(string t) { Token = t; await SecureStorage.Default.SetAsync(Key, t); } public void Clear() { Token = null; SecureStorage.Default.Remove(Key); } }
