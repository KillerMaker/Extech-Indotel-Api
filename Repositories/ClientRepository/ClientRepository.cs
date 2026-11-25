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

        public async Task<Models.Client.ClientListResponse> GetPage(Models.Client.ClientListRequest request)
        {
            const string filter = @" WHERE
                (@Search IS NULL OR CHARINDEX(@Search, Name) > 0 OR CHARINDEX(@Search, Email) > 0
                    OR CHARINDEX(@DocumentSearch, REPLACE(NationalIdentificationNumber, '-', '')) > 0
                    OR CHARINDEX(@Search, ContractNumber) > 0 OR CHARINDEX(@Search, WisproId) > 0)
                AND (@Locality IS NULL OR Locality = @Locality)
                AND (@Status IS NULL OR
                    CASE WHEN SiubenActivated = 1 AND SiubenDisabled = 1 THEN 'inconsistent'
                        WHEN SiubenActivated = 1 THEN 'enabled' WHEN SiubenDisabled = 1 THEN 'disabled'
                        ELSE 'pending' END = @Status)
                AND (@HasContract IS NULL OR
                    CASE WHEN NULLIF(LTRIM(RTRIM(ContractNumber)), '') IS NULL THEN 0 ELSE 1 END = @HasContract)";
            using var connection = _connectionFactory.GetOpenConnection();
            using var result = await connection.QueryMultipleAsync(
                "SELECT COUNT(*) FROM Client" + filter + "; SELECT * FROM Client" + filter +
                " ORDER BY ClientId DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
                new {
                    Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
                    DocumentSearch = request.Search?.Trim().Replace("-", ""),
                    Locality = string.IsNullOrWhiteSpace(request.Locality) ? null : request.Locality.Trim(),
                    Status = string.IsNullOrEmpty(request.Status) ? null : request.Status,
                    request.HasContract, Offset = ((long)request.Page - 1) * request.PageSize, request.PageSize
                });
            var total = await result.ReadSingleAsync<int>();
            var items = (await result.ReadAsync<Entities.Client>()).ToArray();
            return new(items, total, request.Page, request.PageSize);
        }

        public async Task AddContractNumber(string wisproId, string contractNumber)
        {
            var query = "UPDATE Client SET ContractNumber = @ContractNumber WHERE WisproId = @WisproId";

            using var connection = _connectionFactory.GetOpenConnection();

            await connection.ExecuteAsync(query, new { WisproId = wisproId, ContractNumber = contractNumber });
        }

        public async Task MarkSiubenDisabled(string wisproId, string contractNumber)
        {
            const string query = "UPDATE Client SET ContractNumber = @ContractNumber, SiubenActivated = 0, SiubenDisabled = 1 WHERE WisproId = @WisproId";
            using var connection = _connectionFactory.GetOpenConnection();
            await connection.ExecuteAsync(query, new { WisproId = wisproId, ContractNumber = contractNumber });
        }

        public async Task MarkSiubenActivated(string wisproId, string contractNumber)
        {
            const string query = "UPDATE Client SET ContractNumber = @ContractNumber, SiubenActivated = 1, SiubenDisabled = 0 WHERE WisproId = @WisproId";
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
                            Locality,
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
                            @Locality,
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

        public async Task<IEnumerable<Client>> GetClientsForContractSynchronization()
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

        public async Task<Client?> GetByDocumentNumber(string documentNumber)
        {
            const string query = "SELECT * FROM Client WHERE REPLACE(NationalIdentificationNumber, '-', '') = @DocumentNumber";

            using var connection = _connectionFactory.GetOpenConnection();

            return await connection.QueryFirstOrDefaultAsync<Client>(query,
                new { DocumentNumber = documentNumber.Replace("-", string.Empty).Trim() });
        }
    }
}
