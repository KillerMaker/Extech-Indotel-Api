using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Utilities.Factories;
using Dapper;

namespace Exatech_Indotel_API.Repositories.EmailTemplateRepository
{
    public class EmailTemplateRepository : IEmailTemplateRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        public EmailTemplateRepository(IDatabaseConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<EmailTemplate> Get(string name)
        {
            var query = "SELECT * FROM EmailTemplate WHERE [Name] = @Name";

            var connection = _connectionFactory.GetOpenConnection();

            var template = await connection.QueryFirstAsync<EmailTemplate>(query, new { name });

            return template;
        }
    }
}
