using Exatech_Indotel_API.Models.Wispro;

namespace Exatech_Indotel_API.Services.Wispro
{
    public interface IWisproApiProxy
    {
        Task<WisproClient?> GetClient(string? documentNumber, string? phoneNumber, string? email);

        Task<WisproClient?> CreateClient(WisproClient client);
    }
}
