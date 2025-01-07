using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Clients
{
    public class CheckClientResponse
    {
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        [JsonPropertyName("contractNumber")]
        public string? ContractNumber { get; set; } = null;
    }
}
