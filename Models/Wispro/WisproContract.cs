using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Wispro
{
    public class WisproContract
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("public_id")]
        public required int PublicId { get; set; }

        [JsonPropertyName("client_id")]
        public required string ClientId { get; set; }

        [JsonPropertyName("plan_id")]
        public required string PlanId { get; set; }

        [JsonPropertyName("created_at")]
        public required string CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
