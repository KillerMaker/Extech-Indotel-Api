using System.Net;
using Exatech_Indotel_API.Models.Siuben;
using Exatech_Indotel_API.Utilities;
using Exatech_Indotel_API.Services.Cache;
using Microsoft.Extensions.Options;

namespace Exatech_Indotel_API.Services.Siuben.Phase1;

public class SiubenPhase1ApiProxy : SiubenApiClient, ISiubenApiProxy
{
    public SiubenPhase1ApiProxy(IOptions<SiubenOptions> options, IHttpClientFactory factory, ICacheService cache)
        : base(options.Value.Phase1, "phase1", factory, cache) { }

    public async Task<SiubenLookupResult> GetContract(string documentNumber)
    {
        using var response = await Send(HttpMethod.Get, $"/api/Data/get/{NormalizeDocument(documentNumber)}");

        if (response.StatusCode == HttpStatusCode.NotFound) return new(false);
            await EnsureSuccess(response);

        var beneficiary = await response.Content.ReadFromJsonAsync<GetBeneficiaryResponse>()
            ?? throw new InvalidOperationException("Siuben phase 1 returned an empty response.");

        return new(beneficiary.PobertyLevel is not null, beneficiary.PobertyLevel, beneficiary.ContractNumber);
    }

    public async Task PutContract(string documentNumber, PutBeneficiaryRequest request)
    {
        using var response = await Send(HttpMethod.Put, $"/api/Data/update/{NormalizeDocument(documentNumber)}", request);

        await EnsureSuccess(response);
    }
}
