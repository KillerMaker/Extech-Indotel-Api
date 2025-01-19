using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Wispro
{
    public class WisproClient
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; } = string.Empty;

        [JsonPropertyName("street")]
        public string Street { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string? Phone { get; set; } = string.Empty;

        [JsonPropertyName("phone_mobile")]
        public string? PhoneMobile { get; set; } = string.Empty;

        [JsonPropertyName("phone_mobile_verified")]
        public bool? PhoneMobileVerified { get; set; }

        [JsonPropertyName("national_identification_number")]
        public string? NationalIdentificationNumber { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string? City { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string? State { get; set; } = string.Empty;
    }
}
