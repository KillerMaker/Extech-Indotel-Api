using Dapper;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Utilities.Factories;

namespace Exatech_Indotel_API.Repositories.ClientRepository
{
    internal class ClientRepository : IClientRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;

        public ClientRepository(IDatabaseConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task AddContractNumber(string wisproId, string contractNumber)
        {
            var query = "UPDATE Client SET ContractNumber = @ContractNumber WHERE WisproId = @WisproId";

            using var connection = _connectionFactory.GetOpenConnection();

            await connection.ExecuteAsync(query, new { WisproId = wisproId, ContractNumber = contractNumber });
        }

        public async Task Create(Client client)
        {
            var query = @"INSERT INTO Client (
                            WisproId, 
                            Name, 
                            PobertyLevel, 
                            CreatedById, 
                            Email,
                            Street,
                            Number,
                            Phone,
                            PhoneMobile,
                            NationalIdentificationNumber,
                            City,
                            State,
                            PublicId   
                        )
                        VALUES(
                            @WisproId,
                            @Name,
                            @PobertyLevel,
                            @CreatedById,
                            @Email,
                            @Street,
                            @Number,
                            @Phone,
                            @PhoneMobile,
                            @NationalIdentificationNumber,
                            @City,
                            @State,
                            @PublicId)";

            using var connection = _connectionFactory.GetOpenConnection();

            await connection.ExecuteAsync(query, client);
        }

        public async Task<bool> Exists(string nationalIdentificationNumber)
        {
            var query = "SELECT * FROM Client WHERE NationalIdentificationNumber = @NationalIdentificationNumber";

            using var connection = _connectionFactory.GetOpenConnection();

            var client = await connection.QueryFirstOrDefaultAsync<Client>(query, new { NationalIdentificationNumber = nationalIdentificationNumber });

            return client != null;
        }

        public async Task<IEnumerable<Client>> GetClientsWithoutContract()
        {
            var query = "SELECT * FROM Client";

            using var connection = _connectionFactory.GetOpenConnection();

            return await connection.QueryAsync<Client>(query);
        }

        public async Task<Client?> GetByEmail(string email)
        {
            var query = "SELECT * FROM Client WHERE Email = @Email";

            using var connection = _connectionFactory.GetOpenConnection();

            return await connection.QueryFirstOrDefaultAsync<Client>(query, new { Email = email });
        }

        public async Task<Client?> GetByWisproId(string id)
        {
            var query = "SELECT * FROM Client WHERE WisproId = @WisproId";

            using var connection = _connectionFactory.GetOpenConnection();

            return await connection.QueryFirstOrDefaultAsync<Client>(query, new { WisproId = id });
        }
    }
}
