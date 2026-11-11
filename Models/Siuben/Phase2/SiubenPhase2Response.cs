using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben.Phase2;

public class SiubenPhase2Response
{
    [JsonPropertyName("razon")]
    public required string Reason { get; set; }
}
