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
        private readonly IMemoryCache _cache;

        public SiubenApiProxy(IOptions<AppOptions> options, IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _appOptions = options.Value;
            _httpClient = httpClientFactory.CreateClient();

            _httpClient.BaseAddress = new Uri(_appOptions.SiubenUrl);
            _cache = cache;
        }

        private async Task Authorize(bool refresh = false)
        {
            if(!refresh && _cache.Get<string>("jwt") is string cachedToken)
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {cachedToken}");

                return;
            }

            var login = new SiubenLoginRequest
            {
                Username = _appOptions.SiubenUsername,
                Password = _appOptions.SiubenPassword
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", login);

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<SiubenLoginResponse>() ??
                throw new UnauthorizedAccessException();

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.Token}");

            _cache.Set("jwt", token.Token, TimeSpan.FromMinutes(45));
        }

        public async Task<GetBeneficiaryResponse?> GetContract(string documentNumber)
        {
            documentNumber = documentNumber.Replace("-", string.Empty);

            HttpResponseMessage response;

            bool refreshToken = false;

            do
            {
                await Authorize(refreshToken);

                response = await _httpClient.GetAsync($"/api/Data/get/{documentNumber}");

                if (response.StatusCode.Equals(HttpStatusCode.Unauthorized))
                {
                    refreshToken = true;
                    continue;
                } 

                break;
            }
            while (refreshToken);

            if (response.StatusCode.Equals(HttpStatusCode.NotFound))
                return null;

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Siuben API: /api/Data/get/ failed with code: {response.StatusCode}");

            return await response.Content.ReadFromJsonAsync<GetBeneficiaryResponse>() ??
                throw new ArgumentNullException();
        }

        public async Task PutContract(string documentNumber, PutBeneficiaryRequest request)
        {
            documentNumber = documentNumber.Replace("-", string.Empty);

            HttpResponseMessage response;

            bool refreshToken = false;

            do
            {
                await Authorize(refreshToken);

                response = await _httpClient.PutAsJsonAsync($"/api/Data/update/{documentNumber}", request);

                if (response.StatusCode.Equals(HttpStatusCode.Unauthorized))
                {
                    refreshToken = true;
                    continue;
                }
                    
                break;
            }
            while(refreshToken);

            response.EnsureSuccessStatusCode();

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Siuben API: /api/Data/update/ failed with code: {response.StatusCode}");
        }
    }
}
