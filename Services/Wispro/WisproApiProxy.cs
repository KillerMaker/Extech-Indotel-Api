using Exatech_Indotel_API.Models.Wispro;
using Exatech_Indotel_API.Repositories.ClientRepository;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Services.Wispro
{
    public class WisproApiProxy : IWisproApiProxy
    {
        private readonly AppOptions _appOptions;
        private readonly HttpClient _httpClient;

        public WisproApiProxy(IOptions<AppOptions>options, IHttpClientFactory httpClientFactory)
        {
            _appOptions = options.Value;
            _httpClient = httpClientFactory.CreateClient();

            _httpClient.BaseAddress = new Uri(_appOptions.WisproUrl);
            _httpClient.DefaultRequestHeaders.Add("Authorization", _appOptions.WisproApiKey);
        }

        public async Task<WisproClient?> CreateClient(WisproClient client)
        {
            var createResponse = await _httpClient.PostAsJsonAsync($"/clients?name = {client.Name}", client);

            if (!createResponse.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/clients failed with code: {createResponse.StatusCode}");

            var createJsonResult = await createResponse.Content.ReadAsStringAsync();

            var createResult = JsonSerializer.Deserialize<WisproPostResponse<WisproClient>>(createJsonResult);

            var createdClient = await GetClient(createResult?.Data?.NationalIdentificationNumber, null, null);

            return createdClient;
        }

        public async Task<WisproClient?> GetClient(string? documentNumber, string? phoneNumber, string? email)
        {
            var dictionary = new Dictionary<string, string?>
            {
                { "national_identification_number_eq", documentNumber },
                { "phone_number_cont", phoneNumber },
                { "email_eq", email }
            };

            var queryString = "/clients?" + string.Join("&", dictionary
                .Where(x => x.Value is not null)
                .Select(x => $"{x.Key}={x.Value}"));
            
            var response = await _httpClient.GetAsync(queryString);

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/clients?{queryString} failed with code: {response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<WisproGetResponse<WisproClient>>(json) ?? null;

            if(obj?.Data?.Any() ?? false)
                return obj.Data.First();

            return null;
        }

        public async Task<IEnumerable<WisproContract>> GetContracsByDateRange(DateTime startDate, DateTime endDate)
        {
            var response =  await _httpClient
                .GetAsync($"/contracts?created_at_before={endDate.ToString("yyyy-MM-dd")}&created_at_after={startDate.ToString("yyyy-MM-dd")}");

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/contracts failed with code: {response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<WisproGetResponse<WisproContract>>(json) ?? null;

            return obj?.Data ?? new List<WisproContract>();
        }
    }
}
