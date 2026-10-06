using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.User
{
    public class CreateUserRequest
    {
        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("password")]
        public required string Password { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("phone")]
        public required string Phone { get; set; }

        [JsonPropertyName("roleId")]
        public required int RoleId { get; set; }
    }
}
