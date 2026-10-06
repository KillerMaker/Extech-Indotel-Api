using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Email;

namespace Exatech_Indotel_API.Services.Email
{
    public interface IEmailSenderService
    {
        Task SendEmail(Client data, EventType eventType, IEnumerable<string>? copies = null);
        Task SendEmail<T>(T data, EventType eventType, Func<EmailTemplate,T, string> fillTemplate, IEnumerable<string>? copies = null);
    }
}
