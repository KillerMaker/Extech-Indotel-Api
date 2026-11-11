using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben.Phase2;

public class SynchronizeBeneficiaryRequest : StoredBeneficiaryRequest
{
    [JsonPropertyName("motivoCancelacion")]
    public string CancellationReason { get; set; } = "Cancelación";

    [JsonPropertyName("otroMotivoCancelacion")]
    public string? OtherCancellationReason { get; set; }
}
