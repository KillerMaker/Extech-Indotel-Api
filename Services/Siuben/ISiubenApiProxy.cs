using Exatech_Indotel_API.Models.Siuben;

namespace Exatech_Indotel_API.Services.Siuben;

public interface ISiubenApiProxy
{
    Task<SiubenLookupResult> GetContract(string documentNumber);
}
