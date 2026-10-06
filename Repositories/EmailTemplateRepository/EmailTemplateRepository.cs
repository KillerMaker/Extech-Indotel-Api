using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Utilities.Factories;
using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace Exatech_Indotel_API.Repositories.EmailTemplateRepository
{
    public class EmailTemplateRepository : IEmailTemplateRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        private readonly IMemoryCache _cache;
        public EmailTemplateRepository(IDatabaseConnectionFactory connectionFactory, IMemoryCache cache)
        {
            _connectionFactory = connectionFactory;
            _cache = cache;
        }

        public async Task<EmailTemplate> Get(string name)
        {
            if(_cache.Get<EmailTemplate>(name) is EmailTemplate email)
                return email;

            var query = "SELECT * FROM EmailTemplate WHERE [Name] = @Name";

            var connection = _connectionFactory.GetOpenConnection();

            var template = await connection.QueryFirstAsync<EmailTemplate>(query, new { name });

            _cache.Set(name, template, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
            });

            return template;
        }
    }
}
