using Dapper;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Utilities.Factories;

namespace Exatech_Indotel_API.Repositories.UserRepository
{
    internal class UserRepository : IUserRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        private const string SelectUser = @"SELECT
                            AU.UserId,
                            AU.Email,
                            AU.Password,
                            AU.Name,
                            AU.Phone,
                            AU.RoleId,
                            UR.RoleId,
                            UR.RoleName,
                            UR.RoleDescription
                          FROM dbo.AppUser AU
                          INNER JOIN dbo.UserRole UR ON AU.RoleId = UR.RoleId";

        public UserRepository(IDatabaseConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private async Task<User?> FindUser(string predicate, object parameters)
        {
            using var connection = _connectionFactory.GetOpenConnection();
            var users = await connection.QueryAsync<User, Role, User>(SelectUser + predicate,
                (user, role) => { user.Role = role; user.RoleId = role.RoleId; return user; },
                parameters, splitOn: "RoleId");
            return users.FirstOrDefault();
        }

        public Task<User?> CheckUserEmailAndPassword(string email, string password) =>
            FindUser(" WHERE AU.Email = @Email AND AU.Password = @Password", new { Email = email, Password = password });

        public async Task CreateUser(User user)
        {
            const string query = @"INSERT INTO dbo.AppUser (Email, Password, Name, Phone, RoleId)
                                  VALUES (@Email, @Password, @Name, @Phone, @RoleId)";
            using var connection = _connectionFactory.GetOpenConnection();
            await connection.ExecuteAsync(query, user);
        }

        public Task DeleteUser(int id)
        {
            throw new NotImplementedException();
        }

        public Task<User?> GetUserByEmail(string email) =>
            FindUser(" WHERE AU.Email = @Email", new { Email = email });

        public Task<User?> GetUserById(int id) =>
            FindUser(" WHERE AU.UserId = @Id", new { Id = id });
    }
}
