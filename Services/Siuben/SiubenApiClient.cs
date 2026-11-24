using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Exatech_Indotel_API.Models.Siuben;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Services.Cache;

namespace Exatech_Indotel_API.Services.Siuben;

public abstract class SiubenApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cache;
    private readonly SiubenPhaseOptions _options;
    private readonly string _tokenCacheKey;

    protected SiubenApiClient(SiubenPhaseOptions options, string phase, IHttpClientFactory factory, ICacheService cache)
    {
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException($"Siuben {phase} URL and credentials must be configured.");

        _options = options;
        _cache = cache;
        _httpClient = factory.CreateClient();
        _httpClient.BaseAddress = new Uri(url.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/");
        _tokenCacheKey = $"siuben:{phase}:{_httpClient.BaseAddress}:{options.Username}:jwt";
    }

    private Task<string> GetToken(string? rejectedToken = null) =>
        _cache.GetOrCreateAsync(_tokenCacheKey, TimeSpan.FromHours(1), Login,
            token => !string.IsNullOrWhiteSpace(token) && token != rejectedToken);

    private async Task<string> Login()
    {
        // Keep login shorter than the Redis refresh lock's 30-second lease.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", new SiubenLoginRequest
        {
            Username = _options.Username,
            Password = _options.Password
        }, timeout.Token);

        if (!response.IsSuccessStatusCode)
            throw new SiubenApiException(HttpStatusCode.BadGateway, "Siuben authentication failed.");

        var login = await response.Content.ReadFromJsonAsync<SiubenLoginResponse>(timeout.Token);

        if (string.IsNullOrWhiteSpace(login?.Token))
            throw new SiubenApiException(HttpStatusCode.BadGateway, "Siuben returned an empty authentication token.");

        return login.Token;
    }

    protected async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null)
    {
        string? rejectedToken = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await GetToken(rejectedToken);
            using var request = new HttpRequestMessage(method, path.TrimStart('/'));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Unauthorized || attempt == 1) return response;
            response.Dispose();
            rejectedToken = token;
        }
        throw new InvalidOperationException("Unreachable Siuben retry state.");
    }

    protected static string NormalizeDocument(string documentNumber)
    {
        var normalized = documentNumber.Replace("-", string.Empty).Trim();
        if (normalized.Length != 11 || normalized.Any(c => c < '0' || c > '9'))
            throw new ArgumentException("Document number must contain 11 digits.", nameof(documentNumber));
        return normalized;
    }

    protected static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string message = "Siuben request failed.";
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("extraInformation", out var info) && info.ValueKind == JsonValueKind.String)
                    message = info.GetString() ?? message;
            }
            catch (JsonException) { }
        }
        throw new SiubenApiException(response.StatusCode, message);
    }
}
