using Exatech_Indotel_API.Models.Wispro;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Services.Cache;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Exatech_Indotel_API.Services.Wispro
{
    public class WisproApiProxy : IWisproApiProxy
    {
        private readonly AppOptions _appOptions;
        private readonly HttpClient _clientsHttpClient;
        private readonly HttpClient _contractsHttpClient;
        private readonly ICacheService _cache;

        public WisproApiProxy(IOptions<AppOptions>options, IHttpClientFactory httpClientFactory, ICacheService cache)
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
            client.NationalIdentificationNumber = client.NationalIdentificationNumber?.Replace("-","");

            var createResponse = await _clientsHttpClient.PostAsJsonAsync($"?name = {client.Name}", client);

            if (!createResponse.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/clients failed with code: {createResponse.StatusCode}");

            var createJsonResult = await createResponse.Content.ReadAsStringAsync();

            var createResult = JsonSerializer.Deserialize<WisproPostResponse<WisproClient>>(createJsonResult);

            var createdClient = await GetClient(client.NationalIdentificationNumber);

            return createdClient;
        }

        public async Task<WisproClient?> GetClient(string? documentNumber)
        {
            documentNumber = documentNumber?.Replace("-", "");

            if (await _cache.GetAsync<WisproClient>($"wispro-{documentNumber}") is WisproClient client)
                return client;
            
            var response = await _clientsHttpClient.GetAsync($"?national_identification_number_eq={documentNumber}");

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: /api/clients?national_identification_number_eq={documentNumber} failed with code: {response.StatusCode}");

            string json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<WisproGetResponse<WisproClient>>(json) ?? null;

            if(obj?.Data?.Any() ?? false)
            {
                var result = obj.Data.First();

                await _cache.SetAsync($"wispro-{documentNumber}", result, TimeSpan.FromMinutes(15));

                return result;
            }
                
            return null;
        }

        public async Task<WisproContract?> GetContractForClient(string documentNumber, string clientId, string? contractNumber = null)
        {
            var document = Uri.EscapeDataString(documentNumber.Replace("-", string.Empty).Trim());
            var query = $"?client_national_identification_number_eq={document}";
            if (!string.IsNullOrWhiteSpace(contractNumber))
                query += $"&public_id_eq={Uri.EscapeDataString(contractNumber)}";
            using var response = await _contractsHttpClient.GetAsync(query);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<WisproGetResponse<WisproContract>>()
                ?? throw new InvalidOperationException("Wispro returned an empty contract response.");
            if (result.Status != 200)
                throw new InvalidOperationException($"Wispro contract lookup failed with status {result.Status}.");

            if (result.Meta is JsonElement meta && meta.TryGetProperty("pagination", out var pagination) &&
                pagination.TryGetProperty("total_pages", out var pages) && pages.GetInt32() > 1)
                throw new InvalidOperationException("Wispro returned multiple pages of contracts; select the contract before enabling the Siuben beneficiary.");

            var contracts = result.Data?.Where(contract => contract.ClientId == clientId &&
                (string.IsNullOrWhiteSpace(contractNumber) || contract.PublicId.ToString(System.Globalization.CultureInfo.InvariantCulture) == contractNumber)).ToArray() ?? [];
            if (contracts.Length > 1)
                throw new InvalidOperationException("More than one Wispro contract was found for this client; select the contract before enabling the Siuben beneficiary.");

            return contracts.SingleOrDefault();
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
