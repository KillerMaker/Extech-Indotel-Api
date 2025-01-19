using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben
{
    public class GetContractResponse
    {
        [JsonPropertyName("nivelpobreza")]
        public required int? PobertyLevel { get; set; }

        [JsonPropertyName("nocontrato")]
        public required string ContractNumber { get; set; }
    }
}
