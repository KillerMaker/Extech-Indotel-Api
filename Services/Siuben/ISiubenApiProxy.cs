using Exatech_Indotel_API.Models.Siuben;

namespace Exatech_Indotel_API.Services.Siuben
{
    public interface ISiubenApiProxy
    {
        Task<GetContractResponse> GetContract(string documentNumber);
        Task PutContract(string documentNumber, PutContractRequest request);
    }
}
