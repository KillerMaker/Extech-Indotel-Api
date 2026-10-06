using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Client
{
    public class CreateClientResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("wisproId")]
        public string WisproId { get; set; } = string.Empty;

        [JsonPropertyName("publicId")]
        public int? PublicId { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("nationalIdentificationNumber")]
        public string NationalIdentificationNumber { get; set; } = string.Empty;

        [JsonPropertyName("street")]
        public string Street { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("phoneMobile")]
        public string PhoneMobile { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("pobertyLevel")]
        public int PobertyLevel { get; set; }
    }
}
