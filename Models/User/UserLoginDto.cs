using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.User
{
    public class UserLoginDto
    {
        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("password")]
        public required string Password { get; set;}
    }
}
