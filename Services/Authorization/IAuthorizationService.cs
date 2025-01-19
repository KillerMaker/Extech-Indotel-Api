using Exatech_Indotel_API.Models.User;

namespace Exatech_Indotel_API.Services.Authorization
{
    public interface IAuthorizationService
    {
        Task<string> Authorize(UserLoginDto user);
    }
}
