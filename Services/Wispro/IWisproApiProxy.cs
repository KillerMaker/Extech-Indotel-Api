using Exatech_Indotel_API.Models.Wispro;

namespace Exatech_Indotel_API.Services.Wispro
{
    public interface IWisproApiProxy
    {
        Task<WisproClient?> GetClient(string? documentNumber);

        Task<WisproClient?> CreateClient(WisproClient client);

        Task<WisproContract?> GetContractForClient(string documentNumber, string clientId, string? contractNumber = null);

        Task<IEnumerable<WisproContract>>GetContracsByDateRange(DateTime startDate, DateTime endDate);
    }
}
