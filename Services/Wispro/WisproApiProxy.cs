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

            using var createResponse = await _clientsHttpClient.PostAsJsonAsync($"?name={Uri.EscapeDataString(client.Name)}", client);

            var createJsonResult = await createResponse.Content.ReadAsStringAsync();
            WisproPostResponse<WisproClient>? createResult;
            try { createResult = JsonSerializer.Deserialize<WisproPostResponse<WisproClient>>(createJsonResult); }
            catch (JsonException)
            {
                throw new ApiException(System.Net.HttpStatusCode.BadGateway, "WISPRO_INVALID_RESPONSE", "Respuesta no válida de Wispro",
                    "No se pudo interpretar la respuesta de Wispro. Verifique si el cliente fue creado antes de intentarlo de nuevo.", "wispro");
            }
            System.Diagnostics.Activity.Current?.SetTag("wispro.response.status", createResult?.Status);

            // Wispro also reports rejected operations inside an HTTP 200 response.
            if (!createResponse.IsSuccessStatusCode || createResult is null || createResult.Status < 200 || createResult.Status >= 300)
            {
                var fields = createResult?.Errors?.Keys.ToArray() ?? [];
                System.Diagnostics.Activity.Current?.SetTag("wispro.error.fields", string.Join(",", fields));
                var status = createResult?.Status >= 400 ? createResult.Status : (int)createResponse.StatusCode;
                var validation = status is 400 or 409 or 422;
                var errors = createResult?.Errors?.ToDictionary(item => item.Key, item => item.Value.ValueKind switch
                {
                    JsonValueKind.String => new[] { item.Value.GetString()! },
                    JsonValueKind.Array => item.Value.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                        .Select(value => value.GetString()!).ToArray(),
                    _ => Array.Empty<string>()
                });
                throw new ApiException(validation ? System.Net.HttpStatusCode.BadRequest : System.Net.HttpStatusCode.BadGateway,
                    "WISPRO_CREATION_REJECTED", "Wispro no pudo crear el cliente",
                    validation ? "Wispro rechazó los datos del cliente. Revise los campos indicados; es posible que alguno ya esté registrado." :
                    "No se pudo completar la creación en Wispro. Contacte al administrador para verificar la conexión y los permisos.",
                    "wispro", errors);
            }

            // Use the POST result: a search immediately after creation may not see the new client yet.
            var createdClient = createResult?.Data;
            if (string.IsNullOrWhiteSpace(createdClient?.Id) || createdClient.PublicId is null or <= 0)
                return await FindCreatedClient(client.NationalIdentificationNumber, createdClient?.Id);

            return createdClient;
        }

        private static ApiException IdentificationPending() => new(System.Net.HttpStatusCode.Conflict,
            "WISPRO_IDENTIFICATION_PENDING", "Cliente creado en Wispro; identificación pendiente",
            "Wispro informó que creó el cliente, pero aún no se pudieron obtener sus identificadores. No vuelva a crearlo. Verifique el cliente en Wispro y contacte al administrador para completar el registro local.", "wispro");

        private async Task<WisproClient> FindCreatedClient(string? documentNumber, string? createdId)
        {
            var document = documentNumber?.Replace("-", "").Trim();
            if (document?.Length != 11 || document.Any(value => value < '0' || value > '9'))
                throw IdentificationPending();

            // Bypass Redis: an earlier lookup must not determine the result of a new creation.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    if (attempt > 0) await Task.Delay(attempt == 1 ? 250 : 750, timeout.Token);
                    using var response = await _clientsHttpClient.GetAsync(
                        $"?national_identification_number_eq={Uri.EscapeDataString(document)}", timeout.Token);
                    if (!response.IsSuccessStatusCode) break;
                    var result = await response.Content.ReadFromJsonAsync<WisproGetResponse<WisproClient>>(timeout.Token);
                    if (result?.Status != 200 || result.Data is null) break;
                    if (result.Meta is JsonElement { ValueKind: JsonValueKind.Object } meta &&
                        meta.TryGetProperty("pagination", out var pagination))
                    {
                        if (pagination.ValueKind != JsonValueKind.Object ||
                            (pagination.TryGetProperty("total_pages", out var pages) &&
                                (pages.ValueKind != JsonValueKind.Number || !pages.TryGetInt32(out var totalPages) || totalPages > 1)))
                            break;
                    }
                    var candidates = result.Data.Where(candidate =>
                        candidate.NationalIdentificationNumber?.Replace("-", "").Trim() == document).ToArray();
                    if (candidates.Length > 1) break;
                    var candidate = candidates.SingleOrDefault();
                    if (candidate is not null && !string.IsNullOrWhiteSpace(candidate.Id) && candidate.PublicId is > 0)
                    {
                        if (!string.IsNullOrWhiteSpace(createdId) && candidate.Id != createdId) break;
                        return candidate;
                    }
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
            {
                // The POST already succeeded; never retry creation because lookup failed.
            }
            throw IdentificationPending();
        }

        public async Task<WisproClient?> GetClient(string? documentNumber)
        {
            documentNumber = documentNumber?.Replace("-", "");

            if (await _cache.GetAsync<WisproClient>($"wispro-{documentNumber}") is WisproClient client)
                return client;
            
            var response = await _clientsHttpClient.GetAsync($"?national_identification_number_eq={documentNumber}");

            if (!response.IsSuccessStatusCode)
                throw new AggregateException($"Call To Wispro API: client lookup failed with code: {response.StatusCode}");

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
            using var response = await _contractsHttpClient.GetAsync(query);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<WisproGetResponse<WisproContract>>()
                ?? throw new InvalidOperationException("Wispro returned an empty contract response.");
            if (result.Status != 200)
                throw new InvalidOperationException($"Wispro contract lookup failed with status {result.Status}.");

            if (result.Meta is JsonElement meta && meta.TryGetProperty("pagination", out var pagination) &&
                pagination.TryGetProperty("total_pages", out var pages) && pages.GetInt32() > 1)
                throw new InvalidOperationException("Wispro returned multiple pages of contracts; select the contract before enabling the Siuben beneficiary.");

            var returned = result.Data?.ToArray()
                ?? throw new InvalidOperationException("Wispro returned no contract data array; contract deletion cannot be confirmed.");
            var contracts = returned.Where(contract => contract.ClientId == clientId).ToArray();
            if (returned.Length > 0 && contracts.Length == 0)
                throw new InvalidOperationException("Wispro returned contracts for a different client; contract deletion cannot be confirmed.");
            if (contracts.Length > 1)
                throw new InvalidOperationException("More than one Wispro contract was found for this client; select the contract before enabling the Siuben beneficiary.");

            // A sole replacement contract takes precedence over a deleted stored contract.
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
