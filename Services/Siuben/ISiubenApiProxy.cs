using Exatech_Indotel_API.Models.Siuben;

namespace Exatech_Indotel_API.Services.Siuben
{
    public interface ISiubenApiProxy
    {
        Task<GetBeneficiaryResponse?> GetContract(string documentNumber);
        Task PutContract(string documentNumber, PutBeneficiaryRequest request);
    }
}
