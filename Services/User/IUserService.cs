using Exatech_Indotel_API.Models.User;

namespace Exatech_Indotel_API.Services.User
{
    public interface IUserService
    {
        Task CreateUser(CreateUserRequest user);
    }
}
