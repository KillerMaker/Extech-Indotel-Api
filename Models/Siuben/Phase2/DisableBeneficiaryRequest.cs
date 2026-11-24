using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben.Phase2;

public class DisableBeneficiaryRequest : IValidatableObject
{
    [Required, RegularExpression("^[0-9]{11}$")]
    [JsonPropertyName("cedula")]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [JsonPropertyName("motivoCancelacion")]
    public string CancellationReason { get; set; } = string.Empty;

    [StringLength(100)]
    [JsonPropertyName("otroMotivoCancelacion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OtherCancellationReason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        string[] reasons = ["Cancelación", "Falta de pago", "Error de inclusión", "Otro"];
        if (!reasons.Contains(CancellationReason))
            yield return new ValidationResult("Invalid cancellation reason.", [nameof(CancellationReason)]);
        if (CancellationReason == "Otro" && string.IsNullOrWhiteSpace(OtherCancellationReason))
            yield return new ValidationResult("A description is required for Otro.", [nameof(OtherCancellationReason)]);
    }
}
