using Exatech_Indotel_API.Models.Wispro;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Services.Wispro
{
    public class WisproApiProxy : IWisproApiProxy
    {
        private readonly AppOptions _appOptions;
        private readonly HttpClient _clientsHttpClient;
        private readonly HttpClient _contractsHttpClient;
        private readonly IMemoryCache _cache;

        public WisproApiProxy(IOptions<AppOptions>options, IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _appOptions = options.Value;

            _contractsHttpClient = httpClientFactory.CreateClient();
            _clientsHttpClient = httpClientFactory.CreateClient();

            _clientsHttpClient.BaseAddress = new Uri(_appOptions.WisproClientsUrl);
            _contractsHttpClient.BaseAddress = new Uri(_appOptions.WisproContractsUrl);

            _clientsHttpClient.DefaultRequestHeaders.Add("Authorization", _appOptions.WisproApiKey);
            _contractsHttpClient.DefaultRequestHeaders.Add("Authorization", _appOptions.WisproApiKey);

            _cache = cache;
        }

        public async Task<WisproClient?> CreateClient(WisproClient client)
        {
            var createResponse = await _clientsHttpClient.PostAsJsonAsync($"?name = {client.Name}", client);

            if (!createResponse.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/clients failed with code: {createResponse.StatusCode}");

            var createJsonResult = await createResponse.Content.ReadAsStringAsync();

            var createResult = JsonSerializer.Deserialize<WisproPostResponse<WisproClient>>(createJsonResult);

            var createdClient = await GetClient(client.NationalIdentificationNumber, null, null);

            return createdClient;
        }

        public async Task<WisproClient?> GetClient(string? documentNumber, string? phoneNumber, string? email)
        {
            if(_cache.Get<WisproClient>($"wispro-{documentNumber}") is WisproClient client)
                return client;

            var dictionary = new Dictionary<string, string?>
            {
                { "national_identification_number_eq", documentNumber },
                { "phone_number_cont", phoneNumber },
                { "email_eq", email }
            };

            var queryString ="?"+ string.Join("&", dictionary
                .Where(x => x.Value is not null)
                .Select(x => $"{x.Key}={x.Value}"));
            
            var response = await _clientsHttpClient.GetAsync(queryString);

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/clients?{queryString} failed with code: {response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<WisproGetResponse<WisproClient>>(json) ?? null;

            if(obj?.Data?.Any() ?? false)
            {
                var result = obj.Data.First();

                _cache.Set($"wispro-{documentNumber}", result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
                });

                return result;
            }
                
            return null;
        }

        public async Task<IEnumerable<WisproContract>> GetContracsByDateRange(DateTime startDate, DateTime endDate)
        {
            var queryString = $"?created_at_before={endDate.ToString("yyyy-MM-ddT23:59:59Z")}&created_at_after={startDate.ToString("yyyy-MM-ddT00:00:00Z")}";

            var response =  await _contractsHttpClient.GetAsync(queryString);

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/contracts failed with code: {response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<WisproGetResponse<WisproContract>>(json) ?? null;

            return obj?.Data ?? new List<WisproContract>();
        }
    }
}
