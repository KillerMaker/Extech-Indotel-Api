using Exatech_Indotel_API.Models.Siuben;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;

namespace Exatech_Indotel_API.Services.Siuben
{
    public class SiubenApiProxy : ISiubenApiProxy
    {
        private readonly AppOptions _appOptions;
        private readonly HttpClient _httpClient;
        public SiubenApiProxy(IOptions<AppOptions> options, IHttpClientFactory httpClientFactory)
        {
            _appOptions = options.Value;
            _httpClient = httpClientFactory.CreateClient();

            _httpClient.BaseAddress = new Uri(_appOptions.SiubenUrl);
        }

        private async Task Authorize()
        {
 
            var login = new SiubenLoginRequest
            {
                Username = _appOptions.SiubenUsername,
                Password = _appOptions.SiubenPassword
            };

            _httpClient.Timeout = TimeSpan.FromSeconds(100);
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", login);

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<SiubenLoginResponse>() ??
                throw new UnauthorizedAccessException();

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.Token}");
        }

        public async Task<GetContractResponse?> GetContract(string documentNumber)
        {
            await Authorize();

            var response = await _httpClient.GetAsync($"/api/Data/get/{documentNumber}");

            if (response.StatusCode.Equals(HttpStatusCode.NotFound))
                return null;

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Siuben API: /api/Data/get/ failed with code: {response.StatusCode}");

            return await response.Content.ReadFromJsonAsync<GetContractResponse>() ??
                throw new ArgumentNullException();
        }

        public async Task PutContract(string documentNumber, PutContractRequest request)
        {
            await Authorize();

            var response = await _httpClient.PutAsJsonAsync($"/api/Data/update/{documentNumber}", request);

            response.EnsureSuccessStatusCode();

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Siuben API: /api/Data/update/ failed with code: {response.StatusCode}");
        }
    }
}
