using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben
{
    public class SiubenLoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
