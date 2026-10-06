using System.Text.Json.Serialization;

namespace Exatech_Indotel_API.Models.Wispro
{
    public class WisproPostResponse<T> where T : class
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("meta")]
        public object? Meta { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
