using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Utilities.Factories;
using Dapper;
using Exatech_Indotel_API.Services.Cache;

namespace Exatech_Indotel_API.Repositories.EmailTemplateRepository
{
    public class EmailTemplateRepository : IEmailTemplateRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        private readonly ICacheService _cache;
        public EmailTemplateRepository(IDatabaseConnectionFactory connectionFactory, ICacheService cache)
        {
            _connectionFactory = connectionFactory;
            _cache = cache;
        }

        public async Task<EmailTemplate> Get(string name)
        {
            if (await _cache.GetAsync<EmailTemplate>($"email-template:{name}") is EmailTemplate email)
                return email;

            var query = "SELECT * FROM EmailTemplate WHERE [Name] = @Name";

            using var connection = _connectionFactory.GetOpenConnection();

            var template = await connection.QueryFirstAsync<EmailTemplate>(query, new { name });

            await _cache.SetAsync($"email-template:{name}", template, TimeSpan.FromHours(12));

            return template;
        }
    }
}
