using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben
{
    public class PutBeneficiaryRequest
    {
        [JsonPropertyName("noContrato")]
        public required string ContractNumber { get; set; }

        [JsonPropertyName("fechaContrato")]
        public required string DateCreated { get; set; }
    }
}
