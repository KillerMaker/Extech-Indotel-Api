using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Models.Email;

namespace Exatech_Indotel_API.Services.Email
{
    public interface IEmailSenderService
    {
        Task SendEmail(Client data, EventType eventType);
    }
}
