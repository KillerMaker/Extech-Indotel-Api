using System.ComponentModel.DataAnnotations;
using System.Net;
using Exatech_Indotel_API.Models.Siuben;
using Exatech_Indotel_API.Models.Siuben.Phase2;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Services.Cache;
using Microsoft.Extensions.Options;

namespace Exatech_Indotel_API.Services.Siuben.Phase2;

public class SiubenPhase2ApiProxy : SiubenApiClient, ISiubenApiProxy
{
    public SiubenPhase2ApiProxy(IOptions<SiubenOptions> options, IHttpClientFactory factory, ICacheService cache)
        : base(options.Value.Phase2, "phase2", factory, cache) { }

    public async Task<SiubenLookupResult> GetContract(string documentNumber)
    {
        using var response = await Send(HttpMethod.Get, $"/api/data_fase2/get/{NormalizeDocument(documentNumber)}");

        if (response.StatusCode == HttpStatusCode.NotFound) return new(false, IsEligible: false, Reason: "Persona no encontrada");
            await EnsureSuccess(response);

        var result = await ReadResponse(response);

        return new(true, IsEligible: string.Equals(result.Reason, "Elegible", StringComparison.OrdinalIgnoreCase), Reason: result.Reason);
    }

    public async Task<SiubenPhase2Response> EnableBeneficiary(EnableBeneficiaryRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        using var response = await Send(HttpMethod.Put, "/api/data_fase2/HabilitarBeneficiado", request);

        await EnsureSuccess(response);

        return await ReadResponse(response);
    }

    public async Task<SiubenPhase2Response> DisableBeneficiary(DisableBeneficiaryRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        using var response = await Send(HttpMethod.Put, "/api/data_fase2/DeshabilitarBeneficiado", request);

        await EnsureSuccess(response);

        return await ReadResponse(response);
    }

    private static async Task<SiubenPhase2Response> ReadResponse(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<SiubenPhase2Response>()
        ?? throw new InvalidOperationException("Siuben phase 2 returned an empty response.");
}
