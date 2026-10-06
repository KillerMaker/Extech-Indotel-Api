using Dapper;
using Exatech_Indotel_API.Entities;
using Exatech_Indotel_API.Utilities.Factories;

namespace Exatech_Indotel_API.Repositories.UserRepository
{
    internal class UserRepository : IUserRepository
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        public UserRepository(IDatabaseConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }
        public async Task<User?> CheckUserEmailAndPassword(string email, string password)
        {
            var query = @"SELECT
                            UserId,
                            Email,
                            Password,
                            Name,
                            Phone,
                            UR.RoleId,
                            RoleName,
                            RoleDescription
                          FROM AppUser AU 
                          INNER JOIN UserRole UR ON AU.RoleId = UR.RoleId
                          WHERE AU.Email = @email AND AU.Password = @password";

            var connection = _connectionFactory.GetOpenConnection();

            var users = await connection.QueryAsync<User, Role, User>(query, (user, role) => { 
                user.Role = role;
                return user;
            }, 
            new { Email = email, Password = password },
            splitOn: "RoleId");

            return users.FirstOrDefault();
        }

        public Task CreateUser(User user)
        {
            var query = @"INSERT INTO AppUser (
                            Email,
                            Password,
                            Name,
                            Phone,
                            RoleId
                          )
                          VALUES (
                            @Email,
                            @Password,
                            @Name,
                            @Phone,
                            @RoleId
                          )";

            var connection = _connectionFactory.GetOpenConnection();

            return connection.ExecuteAsync(query, user);
        }

        public Task DeleteUser(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            var query = @"SELECT
                            Id,
                            Email,
                            Password,
                            Name,
                            Phone,
                            RoleId
                          FROM APP_USER AU 
                          INNER JOIN USER_ROLE UR ON AU.RoleId = UR.Id
                          WHERE AU.Email = @email";

            var connection = _connectionFactory.GetOpenConnection();

            var user = await connection.QueryFirstAsync<User>(query, new { Email = email });

            return user;
        }

        public async Task<User?> GetUserById(int id)
        {
            var query = @"SELECT
                            Id,
                            Email,
                            Password,
                            Name,
                            Phone,
                            RoleId
                          FROM APP_USER AU 
                          INNER JOIN USER_ROLE UR ON AU.RoleId = UR.Id
                          WHERE AU.Id = @Id";

            var connection = _connectionFactory.GetOpenConnection();

            var user = await connection.QueryFirstAsync<User>(query, new { Id = id });

            return user;
        }
    }
}
