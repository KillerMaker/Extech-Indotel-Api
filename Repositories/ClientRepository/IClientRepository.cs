
using Exatech_Indotel_API.Entities;

namespace Exatech_Indotel_API.Repositories.ClientRepository
{
    public interface IClientRepository
    {
        Task<Client?> GetByWisproId(string id);
        Task<Client?> GetByEmail(string email);
        Task<IEnumerable<Client>> GetAll();
        Task Create(Client client);
        Task AddContractNumber(string wisproId, string contractNumber);
    }
}
