using Exatech_Indotel_API.Entities;

namespace Exatech_Indotel_API.Repositories.EmailTemplateRepository
{
    public interface IEmailTemplateRepository
    {
        Task<EmailTemplate> Get(string name);
    }
}
