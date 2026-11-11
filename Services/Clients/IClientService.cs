using Exatech_Indotel_API.Models.Client;
using Exatech_Indotel_API.Models.Clients;

namespace Exatech_Indotel_API.Services.Clients
{
    public interface IClientsService
    {
        Task<ClientDetailsResponse?> GetByDocumentNumber(string documentNumber);

        Task<CheckClientResponse> CheckClient(string documentNumber, string? phoneNumber, string? email, string locality);

        Task<CreateClientResponse?> CreateClient(CreateClientRequest client);
    }
}
