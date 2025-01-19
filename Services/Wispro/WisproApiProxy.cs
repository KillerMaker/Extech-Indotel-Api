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
            var response = await _httpClient.PostAsJsonAsync($"?name = {client.Name}", client);

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/clients failed with code: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<WisproPostResponse<WisproClient>>(json);

            return obj?.Data; 
        }

        public async Task<WisproClient?> GetClient(string? documentNumber, string? phoneNumber, string? email)
        {
            var dictionary = new Dictionary<string, string?>
            {
                { "national_identification_number_eq", documentNumber },
                { "phone_number_cont", phoneNumber },
                { "email_eq", email }
            };

            var queryString = "?" + string.Join("&", dictionary
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
    }
}
