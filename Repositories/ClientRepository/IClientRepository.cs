
using Exatech_Indotel_API.Entities;

namespace Exatech_Indotel_API.Repositories.ClientRepository
{
    public interface IClientRepository
    {
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
