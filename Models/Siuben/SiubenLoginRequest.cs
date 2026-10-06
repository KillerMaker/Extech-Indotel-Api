using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben
{
    public class SiubenLoginRequest
    {
        [JsonPropertyName("name")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}
