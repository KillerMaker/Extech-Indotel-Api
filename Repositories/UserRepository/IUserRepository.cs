using Exatech_Indotel_API.Entities;

namespace Exatech_Indotel_API.Repositories.UserRepository
{
    public interface IUserRepository
    {
        Task<User?> GetUserByEmail(string email);
        Task<User?>CheckUserEmailAndPassword(string email, string password);
        Task<User?> GetUserById(int id);
        Task CreateUser(User user);
        Task DeleteUser(int id);
    }
}
