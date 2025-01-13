using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Wispro
{
    public class WisproClient
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("public_id")]
        public string? PublicId { get; set; } = string.Empty;

        [JsonPropertyName("custom_id")]
        public string? CustomId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string? Address { get; set; } = string.Empty;

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

        [JsonPropertyName("details")]
        public string? Details { get; set; } = string.Empty;

        [JsonPropertyName("colector_id")]
        public string? CollectorId { get; set; }

        [JsonPropertyName("seller_id")]
        public string? SellerId { get; set; }

        [JsonPropertyName("neighborhood_id")]
        public string? NeighborhoodId { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }
    }
}
