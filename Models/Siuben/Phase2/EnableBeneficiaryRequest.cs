using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben.Phase2;

public class EnableBeneficiaryRequest
{
    [Required, RegularExpression("^[0-9]{11}$")]
    [JsonPropertyName("cedula")]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{10}$")]
    [JsonPropertyName("telefonoBeneficiario")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [JsonPropertyName("prestadora")]
    public string Provider { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [JsonPropertyName("numeroContrato")]
    public string ContractNumber { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("fechaContrato")]
    public DateTimeOffset? ContractDate { get; set; }
}
