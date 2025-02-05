using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Clients
{
    public class CheckClientResponse
    {
        [JsonPropertyName("existsInWispro")]
        public bool ExistsInWispro { get; set; } = false;

        [JsonPropertyName("existsInSiuben")]
        public bool ExistsInSiuben { get; set; } = false;

        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        [JsonPropertyName("contractNumber")]
        public string? ContractNumber { get; set; } = null;

        [JsonPropertyName("pobertyLevel")]
        public int PobertyLevel { get; set; }
    }
}
