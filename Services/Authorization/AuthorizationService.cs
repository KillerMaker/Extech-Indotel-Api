using Exatech_Indotel_API.Models.User;
using Exatech_Indotel_API.Repositories.UserRepository;
using Exatech_Indotel_API.Utilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Exatech_Indotel_API.Services.Authorization
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly AppOptions _options;
        private readonly IUserRepository _repository;

        public AuthorizationService(IOptions<AppOptions> options, IUserRepository repository)
        {
            _options = options.Value;
            _repository = repository;
        }

        public async Task<string> Authorize(UserLoginDto user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var hashedPassword = GetHashedPassword(user.Password);

            var validUser = await _repository.CheckUserEmailAndPassword(user.Email, hashedPassword);

            if (validUser == null)
                return string.Empty;

            ClaimsIdentity claimsIdentity = new ClaimsIdentity(new Claim[]
            {
                new Claim("name", validUser?.Name ?? string.Empty),
                new Claim("email", validUser?.Email ?? string.Empty),
                new Claim("role", validUser?.Role?.RoleName ?? string.Empty),
                new Claim("userId", validUser?.UserId.ToString() ?? "0")
            });

            var token = new JwtSecurityToken(
                issuer: "http://localhost:5031",
                audience: "https://localhost:5001",
                expires: DateTime.Now.AddMinutes(30),
                claims: claimsIdentity.Claims,
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }

        private string GetHashedPassword(string rawPassword)
        {
            var hashedPassBytes = SHA256.Create()
                    .ComputeHash(Encoding.UTF8.GetBytes(rawPassword));

            StringBuilder builder = new StringBuilder();

            foreach (var item in hashedPassBytes)
                builder.Append(item.ToString("x2"));

            return builder.ToString();
        }

        
    }
}
