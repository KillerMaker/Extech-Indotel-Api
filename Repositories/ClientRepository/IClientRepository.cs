
using Exatech_Indotel_API.Entities;

namespace Exatech_Indotel_API.Repositories.ClientRepository
{
    public interface IClientRepository
    {
        async Task<Models.Client.ClientListResponse> GetPage(Models.Client.ClientListRequest request) =>
            request.Apply(await GetClientsForContractSynchronization());

        Task<Client?> GetByWisproId(string id);
        Task<Client?> GetByDocumentNumber(string documentNumber);
        Task<Client?> GetByEmail(string email);
        Task<IEnumerable<Client>> GetClientsForContractSynchronization();
        Task Create(Client client);
        Task MarkSiubenDisabled(string wisproId, string contractNumber);
        Task MarkSiubenActivated(string wisproId, string contractNumber);
        Task AddContractNumber(string wisproId, string contractNumber);

        Task<bool> Exists(string nationalIdentificationNumber);
    }
}
