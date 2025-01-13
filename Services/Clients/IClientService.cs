using Exatech_Indotel_API.Models.Clients;
using Exatech_Indotel_API.Models.Wispro;

namespace Exatech_Indotel_API.Services.Clients
{
    public interface IClientsService
    {
        Task<CheckClientResponse> CheckClient(string documentNumber, string? phoneNumber, string? email);

        Task<string> CreateClient(WisproClientDto client);
    }
}
