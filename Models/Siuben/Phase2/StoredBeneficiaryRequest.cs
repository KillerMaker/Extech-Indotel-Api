using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Siuben.Phase2;

public class StoredBeneficiaryRequest
{
    [Required, RegularExpression("^[0-9]{11}$")]
    [JsonPropertyName("cedula")]
    public string DocumentNumber { get; set; } = string.Empty;
}
